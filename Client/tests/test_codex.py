import asyncio
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import uuid

from swarm_client.codex import CodexAdapter
from swarm_client.codex_rpc import CodexError, CodexRpc
from fixtures.codex_daemon import CodexDaemon


class CodexTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.daemon = await CodexDaemon(self.root).start()
        self.env = patch.dict(os.environ, {"CODEX_HOME": str(self.root)})
        self.env.start()
        self.rpc = CodexRpc()
        self.adapter = CodexAdapter(self.rpc)
        self.session = {"harness_session_id": str(uuid.uuid4()), "workspace": str(self.root)}
        await self.adapter.attach(self.session)
        self.events = []

    async def asyncTearDown(self):
        await self.adapter.close()
        await self.daemon.close()
        self.env.stop()
        self.temp.cleanup()

    async def emit(self, event):
        self.events.append(event)

    def request(self, **kwargs):
        return {"requestId": str(uuid.uuid4()), "prompt": "hello `$(literal)` 🌍", "senderId": "owner",
                "delivery": "queue", **kwargs}

    def calls(self):
        return [json.loads(line) for line in (self.root / "calls.jsonl").read_text().splitlines()]

    async def wait_events(self, status):
        for _ in range(100):
            if any(e["status"] == status for e in self.events):
                return
            await asyncio.sleep(0.02)
        self.fail(str(self.events))

    async def test_busy_queue_preserves_prompt_and_settings_and_captures_fast_completion(self):
        (self.root / "busy").touch()
        request = self.request(model="test-model", reasoning="high")
        self.assertEqual(await self.adapter.submit(self.session, request, self.emit), {"status": "queued"})
        await asyncio.sleep(0.1)
        self.assertFalse(any(c["method"] in {"thread/queue/add", "turn/start", "turn/steer",
                                           "thread/settings/update"} for c in self.calls()))
        (self.root / "busy").unlink()
        await self.wait_events("completed")
        self.assertIn(request["prompt"], self.events[-1]["output"])
        self.assertEqual(self.events[-1]["turnId"], self.events[0]["turnId"])
        settings = next(c["params"] for c in self.calls() if c["method"] == "thread/settings/update")
        self.assertEqual(settings["model"], "test-model")
        self.assertEqual(settings["effort"], "high")
        self.assertFalse(any(c["method"] in {"turn/start", "thread/start"} for c in self.calls()))

    async def test_inject_only_targets_expected_active_turn(self):
        with self.assertRaisesRegex(CodexError, "no_active_turn"):
            await self.adapter.submit(self.session, self.request(delivery="inject"), self.emit)
        (self.root / "busy").touch()
        result = await self.adapter.submit(self.session, self.request(delivery="inject"), self.emit)
        self.assertEqual(result, {"status": "injected", "turnId": "local-turn"})
        steer = next(c for c in self.calls() if c["method"] == "turn/steer")
        self.assertEqual(steer["params"]["expectedTurnId"], "local-turn")
        with self.assertRaisesRegex(CodexError, "inject_cannot_change"):
            await self.adapter.submit(self.session, self.request(delivery="inject", model="test-model"), self.emit)

    async def test_validation_and_disconnect_cancel_queued_work(self):
        for kwargs in [{"model": "invented"}, {"reasoning": "invented"}]:
            with self.assertRaisesRegex(CodexError, "unsupported_model"):
                await self.adapter.submit(self.session, self.request(**kwargs), self.emit)
        with self.assertRaisesRegex(CodexError, "workspace_mismatch"):
            await self.adapter.attach({**self.session, "workspace": "/another/workspace"})
        await self.adapter.submit(self.session, self.request(), self.emit, is_current=lambda: False)
        await self.wait_events("failed")
        self.assertFalse(any(c["method"] == "thread/queue/add" for c in self.calls()))

    async def test_start_race_never_steers_or_replays(self):
        (self.root / "race").touch()
        await self.adapter.submit(self.session, self.request(), self.emit)
        await self.wait_events("unknown")
        methods = [c["method"] for c in self.calls()]
        self.assertEqual(methods.count("thread/queue/add"), 1)
        self.assertNotIn("turn/steer", methods)

    async def test_native_cli_can_consume_the_prompt_before_add_replies(self):
        (self.root / "cli-before-add-reply").touch()
        await self.adapter.submit(self.session, self.request(), self.emit)
        await self.wait_events("completed")
        self.assertEqual(self.events[-1]["turnId"], "cli-early")
        methods = [c["method"] for c in self.calls()]
        self.assertEqual(methods.count("thread/queue/add"), 1)
        self.assertNotIn("thread/queue/start", methods)
        self.assertNotIn("Local progress", self.events[-1]["output"])

    async def test_native_cli_start_race_is_correlated_instead_of_reported_unknown(self):
        (self.root / "cli-start-race").touch()
        await self.adapter.submit(self.session, self.request(), self.emit)
        await self.wait_events("completed")
        self.assertEqual(self.events[-1]["turnId"], "cli-turn")
        self.assertFalse(any(e["status"] == "unknown" for e in self.events))
        methods = [c["method"] for c in self.calls()]
        self.assertEqual(methods.count("thread/queue/add"), 1)
        self.assertEqual(methods.count("thread/queue/start"), 1)

    async def test_missed_notifications_recover_completed_output_by_exact_request_id(self):
        (self.root / "cli-start-race").touch()
        (self.root / "history-only").touch()
        request = self.request()
        await self.adapter.submit(self.session, request, self.emit)
        await self.wait_events("completed")
        self.assertEqual(self.events[-1]["turnId"], "cli-turn")
        self.assertIn(request["prompt"], self.events[-1]["output"])
        self.assertNotIn("Local progress", self.events[-1]["output"])
        self.assertFalse(self.rpc.requests)

    async def test_lost_add_response_is_reconciled_without_republishing_the_prompt(self):
        (self.root / "add-response-error").touch()
        await self.adapter.submit(self.session, self.request(), self.emit)
        await self.wait_events("completed")
        self.assertEqual(self.events[-1]["turnId"], "cli-add")
        methods = [c["method"] for c in self.calls()]
        self.assertEqual(methods.count("thread/queue/add"), 1)
        self.assertNotIn("thread/queue/start", methods)

    async def test_unknown_delivery_never_adopts_an_unrelated_completed_turn(self):
        (self.root / "race").touch()
        self.daemon.history[self.session["harness_session_id"]] = [{"id": "unrelated", "status": "completed", "items": [
            {"type": "userMessage", "id": "someone", "clientId": str(uuid.uuid4()), "content": []},
            {"type": "agentMessage", "id": "other-answer", "text": "UNRELATED ANSWER"}]}]
        await self.adapter.submit(self.session, self.request(), self.emit)
        await self.wait_events("unknown")
        self.assertFalse(any(e["status"] == "completed" for e in self.events))
        self.assertNotIn("UNRELATED ANSWER", json.dumps(self.events))

    async def test_atomic_start_retries_busy_without_requeueing(self):
        (self.root / "busy-race").touch()
        await self.adapter.submit(self.session, self.request(), self.emit)
        await self.wait_events("completed")
        methods = [c["method"] for c in self.calls()]
        self.assertEqual(methods.count("thread/queue/add"), 1)
        self.assertEqual(methods.count("thread/queue/start"), 2)
        self.assertNotIn("turn/steer", methods)

    async def test_injected_turn_returns_only_new_output(self):
        (self.root / "busy").touch()
        thread = self.session["harness_session_id"]
        self.rpc.notification("item/completed", {"threadId": thread, "turnId": "local-turn",
            "item": {"type": "agentMessage", "id": "old", "text": "prior local output"}})
        await self.adapter.submit(self.session, self.request(delivery="inject"), self.emit)
        self.rpc.notification("item/completed", {"threadId": thread, "turnId": "local-turn",
            "item": {"type": "agentMessage", "id": "new", "text": "new answer"}})
        self.rpc.notification("turn/completed", {"threadId": thread,
            "turn": {"id": "local-turn", "status": "completed"}})
        await self.wait_events("completed")
        self.assertEqual(self.events[-1]["output"], "new answer")

    async def test_local_commentary_is_not_automatically_shared(self):
        thread = self.session["harness_session_id"]
        for item in [
            {"id": "progress", "phase": "commentary", "text": "Detailed local progress"},
            {"id": "answer", "phase": "final_answer", "text": "Shared result"},
        ]:
            self.rpc.notification("item/completed", {"threadId": thread, "turnId": "turn",
                "item": {"type": "agentMessage", **item}})
        self.assertEqual(self.rpc.turns[(thread, "turn")].output_since(set()), "Shared result")

    async def test_unloaded_thread_is_not_silently_resumed_in_another_server(self):
        before = len([c for c in self.calls() if c["method"] == "thread/resume"])
        (self.root / "unloaded").touch()
        with self.assertRaisesRegex(CodexError, "not_loaded_on_daemon"):
            await self.adapter.attach(self.session)
        after = len([c for c in self.calls() if c["method"] == "thread/resume"])
        self.assertEqual(before, after)

    async def test_custom_socket_and_absent_daemon(self):
        rpc = CodexRpc(str(self.daemon.socket))
        try:
            await rpc.connect()
            self.assertTrue((await rpc.call("model/list", {}))["data"])
        finally:
            await rpc.close()
        rpc = CodexRpc(str(self.root / "does-not-exist.sock"))
        with self.assertRaisesRegex(CodexError, "check_daemon_socket"):
            await rpc.connect()
        self.assertIsNone(rpc.http)

    async def test_requests_for_the_same_thread_run_in_fifo_order(self):
        (self.root / "busy").touch()
        await self.adapter.submit(self.session, self.request(prompt="first"), self.emit)
        await self.adapter.submit(dict(self.session), self.request(prompt="second"), self.emit)
        (self.root / "busy").unlink()
        for _ in range(100):
            if len([event for event in self.events if event["status"] == "completed"]) == 2:
                break
            await asyncio.sleep(0.02)
        completed = [event["output"] for event in self.events if event["status"] == "completed"]
        self.assertEqual(len(completed), 2, self.events)
        self.assertTrue(completed[0].endswith("first"), completed)
        self.assertTrue(completed[1].endswith("second"), completed)
