import asyncio
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import uuid

from swarm_client.codex import CodexAdapter, CodexError, CodexRpc


class CodexTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.env = patch.dict(os.environ, {
            "PATH": str(Path(__file__).parent / "fixtures") + os.pathsep + os.environ["PATH"],
            "SWARM_TEST_CODEX_ROOT": str(self.root)})
        self.env.start()
        self.rpc = CodexRpc()
        self.adapter = CodexAdapter(self.rpc)
        self.session = {"harness_session_id": str(uuid.uuid4()), "workspace": str(self.root)}
        await self.adapter.attach(self.session)
        self.events = []

    async def asyncTearDown(self):
        await self.adapter.close()
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
        await self.adapter.submit(self.session, self.request(), self.emit, alive=lambda: False)
        await self.wait_events("failed")
        self.assertFalse(any(c["method"] == "thread/queue/add" for c in self.calls()))

    async def test_start_race_never_steers_or_replays(self):
        (self.root / "race").touch()
        await self.adapter.submit(self.session, self.request(), self.emit)
        await self.wait_events("unknown")
        methods = [c["method"] for c in self.calls()]
        self.assertEqual(methods.count("thread/queue/add"), 1)
        self.assertNotIn("turn/steer", methods)

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
