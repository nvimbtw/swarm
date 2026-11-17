import asyncio
import json
import os
import uuid
from types import SimpleNamespace
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import AsyncMock

import aiohttp

from swarm_client.cli import rpc
from swarm_client.storage import backend_origin
from swarm_client.backend import BackendError, BackendLink
from swarm_client.service import Connector
from swarm_client.workspace_tools import WORKSPACE_TOOLS
from fixtures.codex_daemon import CodexDaemon


ROOT = Path(__file__).resolve().parents[2]
FIXTURE = ROOT / "Backend/Swarm.Server.Tests/bin/Debug/net10.0/Swarm.Server.Tests.dll"


class ConfigurationTests(unittest.TestCase):
    def test_credentials_require_trusted_origin(self):
        self.assertEqual(backend_origin("http://localhost:5142/"), "http://localhost:5142")
        self.assertEqual(backend_origin("https://swarm.example"), "https://swarm.example")
        for value in ["http://192.168.1.2:5142", "https://user:secret@example.com",
                      "https://example.com/path", "https://example.com?token=secret"]:
            with self.assertRaises(ValueError):
                backend_origin(value)


class IncomingRequestTests(unittest.IsolatedAsyncioTestCase):
    async def test_results_keep_prompt_context_when_backend_has_no_public_receipt(self):
        class Socket:
            closed = False
            def __init__(self): self.messages = []
            async def send_str(self, value): self.messages.append(json.loads(value))
        ws = Socket()
        link = BackendLink.__new__(BackendLink)
        link.ws, link.send_lock = ws, asyncio.Lock()
        request = {"requestId": str(uuid.uuid4()), "sessionId": str(uuid.uuid4()), "prompt": "Run tests\nThen explain"}
        update = {"status": "completed", "output": "All tests passed."}
        await link.publish_agent_update(ws, request, update)
        self.assertIn("> Run tests\n> Then explain", ws.messages[-1]["payload"]["output"])
        await link.publish_agent_update(ws, {**request, "promptMessageId": "789"}, update)
        self.assertEqual(ws.messages[-1]["payload"]["output"], "All tests passed.")
        self.assertEqual(update["output"], "All tests passed.")

    async def test_duplicate_requests_share_one_submission(self):
        class Adapter:
            calls = 0
            async def submit(self, *args):
                self.calls += 1
                await asyncio.sleep(0.01)
                return {"status": "queued"}
        class Socket:
            closed = False
            messages = []
            async def send_str(self, text):
                self.messages.append(json.loads(text))
        adapter, ws = Adapter(), Socket()
        link = BackendLink.__new__(BackendLink)
        link.origin, link.ws = "https://test.example", ws
        link.send_lock, link.handled = asyncio.Lock(), {}
        link.service = SimpleNamespace(adapters={None: adapter}, storage=SimpleNamespace(
            credentials={link.origin: {"ownerId": "owner"}}))
        session_id, request_id = str(uuid.uuid4()), str(uuid.uuid4())
        link.sessions = {session_id: {"registration": {"control": True, "mode": "solo"}}}
        message = {"id": request_id, "type": "agent.submit", "payload": {
            "requestId": request_id, "sessionId": session_id, "senderId": "owner",
            "prompt": "hello", "delivery": "queue"}}
        await asyncio.gather(link.agent_request(ws, message), link.agent_request(ws, message))
        self.assertEqual(adapter.calls, 1)
        self.assertEqual(len(ws.messages), 2)
        self.assertEqual(ws.messages[0], ws.messages[1])


class SessionToolRequestTests(unittest.IsolatedAsyncioTestCase):
    async def test_ambiguous_native_binding_is_rejected(self):
        sessions = {str(i): {"workspace": "/project", "harness_session_id": "native",
                             "state": "registered", "registration": {"sessionId": str(i)}} for i in range(2)}
        link = SimpleNamespace(sessions=sessions, request=AsyncMock())
        connector = Connector(None, None)
        with self.assertRaisesRegex(BackendError, "ambiguous"):
            await connector.session_tool(link, {"kind": "discord.read", "channelId": "123",
                "harness_session_id": "native", "workspace": "/project"})
        link.request.assert_not_awaited()

    async def test_uncertain_send_is_not_retried_or_rebound(self):
        link = SimpleNamespace(sessions={"one": {"state": "registered", "registration": {"sessionId": "one"}}},
            request=AsyncMock(side_effect=BackendError("connection_lost")))
        connector = Connector(None, None)
        with self.assertRaisesRegex(BackendError, "chat_delivery_unknown_check_discord"):
            await connector.session_tool(link, {"session_id": "one", "kind": "discord.send",
                "channelId": "123", "content": "result", "sessionId": "forged"})
        link.request.assert_awaited_once_with("discord.send", {
            "channelId": "123", "content": "result", "sessionId": "one"})

    async def test_workspace_fields_cannot_override_session_project_or_author(self):
        link = SimpleNamespace(sessions={"one": {"state": "registered", "registration": {"sessionId": "one"}}},
            request=AsyncMock(return_value={"payload": {"item": {"id": 1}}}))
        connector = Connector(None, None)
        result = await connector.session_tool(link, {"operation": "workspace", "session_id": "one",
            "kind": "workspace.initiative.create", "title": "Epic", "sessionId": "forged",
            "projectId": "other", "authorMemberId": 999})
        self.assertEqual(result["item"]["id"], 1)
        link.request.assert_awaited_once_with("workspace.initiative.create", {"sessionId": "one", "title": "Epic"})

    async def test_uncertain_workspace_write_is_not_retried(self):
        link = SimpleNamespace(sessions={"one": {"state": "registered", "registration": {"sessionId": "one"}}},
            request=AsyncMock(side_effect=BackendError("connection_lost")))
        connector = Connector(None, None)
        with self.assertRaisesRegex(BackendError, "workspace_delivery_unknown_read_before_retry"):
            await connector.session_tool(link, {"session_id": "one", "kind": "workspace.document.create", "title": "Design"})
        self.assertEqual(link.request.await_count, 1)

    async def test_auto_binding_rejects_the_same_thread_in_multiple_backends(self):
        connector = Connector(None, None)
        session = {"harness_session_id": "native", "registration": {"harness": "codex"}}
        connector.links = {"first": SimpleNamespace(sessions={"one": session}),
                           "second": SimpleNamespace(sessions={"two": session})}
        with self.assertRaisesRegex(BackendError, "ambiguous"):
            await connector.dispatch({"operation": "mcp-status", "auto": True, "harness_session_id": "native"})


@unittest.skipUnless(FIXTURE.exists(), "Build Backend/Swarm.Server.Tests first")
class ConnectorIntegrationTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.home = Path(self.temp.name) / "state"
        self.backend = await asyncio.create_subprocess_exec(
            "dotnet", str(FIXTURE), "--serve", stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        self.origin = (await asyncio.wait_for(self.backend.stdout.readline(), 15)).decode().strip()
        self.assertTrue(self.origin.startswith("http://127.0.0.1:"), self.origin)
        self.daemon = await CodexDaemon(Path(self.temp.name)).start()
        environment = {**os.environ, "CODEX_HOME": self.temp.name}
        self.service = await asyncio.create_subprocess_exec(
            sys.executable, "-m", "swarm_client.cli", "--home", str(self.home), "serve",
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=environment)
        ready = await asyncio.wait_for(self.service.stdout.readline(), 10)
        self.assertEqual(json.loads(ready)["status"], "listening")
        self.http = aiohttp.ClientSession()

    async def asyncTearDown(self):
        await self.http.close()
        for process in (self.service, self.backend):
            if process.returncode is None:
                process.terminate()
            try:
                await asyncio.wait_for(process.wait(), 10)
            except TimeoutError:
                process.kill()
                await process.wait()
            errors = (await process.stderr.read()).decode()
            self.assertNotIn("Traceback", errors)
        await self.daemon.close()
        self.temp.cleanup()

    async def call(self, operation, **kwargs):
        return await rpc(self.home, {"operation": operation, "backend": self.origin, **kwargs})

    async def pair(self):
        challenge = await self.call("pair-start")
        pairing_id = challenge["discord_command"].split(":")[-1]
        async with self.http.post(self.origin + "/test/approve/" + pairing_id) as response:
            code = (await response.json())["code"]
        result = await self.call("pair-complete", code=code)
        self.assertEqual(result["status"], "authenticated")
        self.assertNotIn("token", json.dumps(result))

    async def eventually(self, predicate):
        for _ in range(120):
            status = await self.call("status")
            if predicate(status):
                return status
            await asyncio.sleep(0.1)
        self.fail(f"State did not converge: {status}")

    async def test_two_sessions_pairing_reconnect_and_independent_disconnect(self):
        async with self.http.get(self.origin + "/ws/sessions") as response:
            self.assertEqual(response.status, 401)
        self.assertEqual((await self.call("sessions"))["status"], "pairing_required")
        await self.pair()
        self.assertEqual(stat.S_IMODE((self.home / "credentials.json").stat().st_mode), 0o600)
        self.assertEqual(stat.S_IMODE((self.home / "connector.sock").stat().st_mode), 0o600)
        for index in range(2):
            result = await self.call("connect", name=f"agent-{index}", project="swarm", harness="codex",
                                     mode="solo", workspace=f"/project/{index}", harness_session_id=f"thread-{index}")
            self.assertIn(result["status"], ("pending", "registered"))
        status = await self.eventually(lambda s: len(s["backends"][0]["sessions"]) == 2 and
                                      all(x["state"] == "registered" for x in s["backends"][0]["sessions"]))
        sessions = status["backends"][0]["sessions"]
        reused = await self.call("connect", name="agent-0", project="swarm", harness="codex", mode="solo",
                                 workspace="/project/0", harness_session_id="thread-0")
        self.assertEqual(reused["session"]["registration"]["sessionId"], sessions[0]["registration"]["sessionId"])
        remote = await self.call("sessions")
        self.assertEqual(len(remote["payload"]), 2)
        self.assertEqual(len({s["connectionId"] for s in remote["payload"]}), 1)
        self.assertNotIn("workspace", json.dumps(remote))
        self.assertEqual(remote["payload"][0]["identity"]["ownerId"], "discord-test-owner")
        old_connection = remote["payload"][0]["connectionId"]
        async with self.http.post(self.origin + "/test/drop-connections") as response:
            self.assertEqual(response.status, 200)
        for _ in range(120):
            await asyncio.sleep(0.1)
            remote = await self.call("sessions")
            if remote.get("payload") and len(remote["payload"]) == 2 and remote["payload"][0]["connectionId"] != old_connection:
                break
        else:
            self.fail("Transport reconnect did not restore both sessions")
        # Re-pair swaps the WebSocket and re-registers retained local sessions.
        await self.pair()
        await self.eventually(lambda s: all(x["state"] == "registered" for x in s["backends"][0]["sessions"]))
        await self.call("disconnect", session_id=sessions[0]["registration"]["sessionId"])
        remote = await self.call("sessions")
        self.assertEqual(len(remote["payload"]), 1)
        self.assertEqual(remote["payload"][0]["name"], "agent-1")
        # A backend restart loses in-memory credentials and requires pairing again.
        self.backend.terminate()
        await self.backend.wait()
        self.backend = await asyncio.create_subprocess_exec(
            "dotnet", str(FIXTURE), "--serve", "--test-url", self.origin,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        self.assertEqual((await asyncio.wait_for(self.backend.stdout.readline(), 15)).decode().strip(), self.origin)
        await self.eventually(lambda s: s["backends"][0]["state"] == "pairing_required")
        await self.pair()
        await self.eventually(lambda s: s["backends"][0]["state"] == "connected" and
                              s["backends"][0]["sessions"][0]["state"] == "registered")
        # A second service must not remove the original service's socket.
        duplicate = await asyncio.create_subprocess_exec(
            sys.executable, "-m", "swarm_client.cli", "--home", str(self.home), "serve", stdout=subprocess.PIPE)
        output, _ = await duplicate.communicate()
        self.assertNotEqual(duplicate.returncode, 0)
        self.assertEqual(json.loads(output)["status"], "error")
        self.assertEqual((await self.call("status"))["status"], "running")
        self.service.terminate()
        await self.service.wait()
        async with self.http.get(self.origin + "/diagnostics") as response:
            diagnostics = await response.json()
        self.assertEqual(len(diagnostics["activeConnections"]), 0)

    async def test_discord_routing_queue_inject_access_and_results_share_one_socket(self):
        await self.pair()
        sessions = []
        for index, mode in enumerate(["solo", "swarm"]):
            connected = await self.call("connect", name=f"controlled-{index}", project="swarm",
                harness="codex", mode=mode, workspace=self.temp.name,
                harness_session_id=str(uuid.uuid4()), control=True)
            self.assertNotEqual(connected["status"], "error", connected)
            sessions.append(connected["session"]["registration"]["sessionId"])
        await self.eventually(lambda s: all(x["state"] == "registered" for x in s["backends"][0]["sessions"]))
        remote = await self.call("sessions")
        self.assertEqual(len({s["connectionId"] for s in remote["payload"]}), 1)
        async with self.http.get(self.origin + "/test/agent/other-user") as response:
            self.assertEqual([s["sessionId"] for s in await response.json()], [sessions[1]])
        async def send(sender, session, **extra):
            async with self.http.post(self.origin + "/test/agent/" + sender, json={
                "sessionId": session, "prompt": "Hello from Discord 🌍", **extra}) as response:
                return response.status, await response.json()
        status, result = await send("other-user", sessions[0])
        self.assertEqual(status, 400)
        self.assertIn("not_allowed", result["error"])
        busy = Path(self.temp.name) / "busy"
        busy.touch()
        queued = []
        for sender, session in [("discord-test-owner", sessions[0]), ("other-user", sessions[1])]:
            status, result = await send(sender, session, model="test-model", reasoning="high")
            self.assertEqual(status, 200, result)
            self.assertEqual(result["status"], "queued", result)
            queued.append(result["requestId"])
        status, injected = await send("other-user", sessions[1], delivery="inject")
        self.assertEqual(injected["status"], "injected", injected)
        # Local CLI operations remain responsive while two requests wait for idle.
        self.assertEqual(len((await self.call("sessions"))["payload"]), 2)
        busy.unlink()
        for _ in range(100):
            async with self.http.get(self.origin + "/test/agent-events") as response:
                events = await response.json()
            completed = [x for x in events if x["update"]["status"] == "completed"]
            if len(completed) == 2:
                break
            await asyncio.sleep(0.05)
        else:
            self.fail(str(events))
        self.assertEqual({x["update"]["requestId"] for x in completed}, set(queued))
        for event in completed:
            self.assertEqual(event["destination"]["channelId"], 123)
            self.assertIn("Hello from Discord 🌍", event["update"]["output"])
        # Disconnecting a registration cancels its not-yet-delivered work.
        busy.touch()
        _, waiting = await send("discord-test-owner", sessions[0])
        await self.call("disconnect", session_id=sessions[0])
        busy.unlink()
        for _ in range(100):
            async with self.http.get(self.origin + "/test/agent-events") as response:
                events = await response.json()
            cancelled = [x for x in events if x["update"]["requestId"] == waiting["requestId"]]
            if cancelled:
                break
            await asyncio.sleep(0.02)
        self.assertEqual(cancelled[-1]["update"]["status"], "failed")
        self.assertEqual(cancelled[-1]["update"]["error"], "request_cancelled")

    async def test_cli_defaults_to_verified_control_and_reports_readiness(self):
        await self.pair()
        thread_id = str(uuid.uuid4())
        async def cli(*extra, thread_env=thread_id):
            env = dict(os.environ)
            env.pop("CODEX_THREAD_ID", None)
            if thread_env:
                env["CODEX_THREAD_ID"] = thread_env
            process = await asyncio.create_subprocess_exec(sys.executable, "-m", "swarm_client.cli",
                "--home", str(self.home), "connect", "--backend", self.origin,
                "--workspace", self.temp.name, "--harness", "codex", "--project", "swarm",
                *extra, stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=env)
            output, error = await process.communicate()
            self.assertFalse(error, error.decode())
            return process.returncode, json.loads(output)
        code, controlled = await cli()
        self.assertEqual(code, 0, controlled)
        self.assertTrue(controlled["delivery"]["ready"])
        self.assertTrue(controlled["mcp"]["ready"], controlled)
        self.assertTrue(controlled["session"]["registration"]["control"])
        self.assertEqual(controlled["session"]["harness_session_id"], thread_id)
        code, missing_id = await cli(thread_env=None)
        self.assertNotEqual(code, 0)
        self.assertEqual(missing_id["error"], "codex_control_requires_thread_id")
        code, metadata = await cli("--registration-only")
        self.assertEqual(code, 0, metadata)
        self.assertFalse(metadata["delivery"]["ready"])
        self.assertEqual(metadata["delivery"]["state"], "registration_only")
        self.assertEqual(metadata["session"]["registration"]["sessionId"],
                         controlled["session"]["registration"]["sessionId"])

    async def test_workspace_mcp_crud_uses_the_attached_project_without_discord(self):
        from mcp import ClientSession, StdioServerParameters
        from mcp.client.stdio import stdio_client

        await self.pair()
        thread_id = str(uuid.uuid4())
        connected = await self.call("connect", name="workspace-agent", project="swarm", harness="codex",
            mode="solo", workspace=self.temp.name, harness_session_id=thread_id, control=True)
        self.assertTrue(connected["mcp"]["ready"], connected)
        connection_before = (await self.call("sessions"))["payload"][0]["connectionId"]
        params = StdioServerParameters(command=sys.executable, args=["-m", "swarm_client.cli",
            "--home", str(self.home), "mcp", "--backend", self.origin,
            "--workspace", self.temp.name, "--harness-session-id", thread_id])
        async with stdio_client(params) as (reader, writer):
            async with ClientSession(reader, writer) as mcp:
                await mcp.initialize()

                async def tool(name, **arguments):
                    result = await mcp.call_tool(name, arguments)
                    self.assertFalse(result.isError, result)
                    return json.loads(result.content[0].text)

                epic = (await tool("initiative_create", title="Agent workspace", brief_markdown="Big goal"))["item"]
                feature = (await tool("work_item_create", title="Search features", type="feature",
                                      initiative_id=epic["id"]))["item"]
                issue = (await tool("work_item_create", title="Fix filters", type="issue",
                                    initiative_id=epic["id"]))["item"]
                children = await tool("work_item_list", initiative_id=epic["id"], status="open", type="feature")
                self.assertEqual([item["id"] for item in children["items"]], [feature["id"]])
                self.assertEqual(children["items"][0]["initiativeTitle"], epic["title"])
                self.assertEqual((await tool("initiative_list", query="workspace"))["total"], 1)
                epic = (await tool("initiative_update", id=epic["id"], expected_version=1,
                                   brief_markdown="Updated scope"))["item"]
                self.assertEqual((await tool("initiative_get", id=epic["id"]))["item"]["version"], 2)
                document = (await tool("document_create", title="API design", markdown="# Initial"))["item"]
                document = (await tool("document_update", id=document["id"], expected_version=1,
                                       markdown="# Revised 🌍"))["item"]
                self.assertEqual((await tool("document_get", id=document["id"]))["item"]["markdown"], "# Revised 🌍")
                self.assertEqual((await tool("document_list", query="API"))["total"], 1)
                stale = await mcp.call_tool("document_update", {"id": document["id"], "expected_version": 1,
                                                              "markdown": "Overwrite"})
                self.assertTrue(stale.isError)
                self.assertIn("version_conflict_read_again", stale.content[0].text)
                invalid = await mcp.call_tool("work_item_create", {"title": "Bad", "type": "epic"})
                self.assertTrue(invalid.isError)
                blocked = await mcp.call_tool("initiative_delete", {"id": epic["id"], "expected_version": 2})
                self.assertTrue(blocked.isError)
                self.assertIn("initiative_has_work_items", blocked.content[0].text)
                feature = (await tool("work_item_update", id=feature["id"], expected_version=1,
                                      status="verification"))["item"]
                self.assertEqual((await tool("work_item_get", id=feature["id"]))["item"]["status"], "verification")
                for item in (feature, issue):
                    await tool("work_item_delete", id=item["id"], expected_version=item["version"])
                await tool("initiative_delete", id=epic["id"], expected_version=epic["version"])
                await tool("document_delete", id=document["id"], expected_version=document["version"])
                self.assertEqual((await tool("initiative_list"))["total"], 0)
                self.assertEqual((await tool("work_item_list"))["total"], 0)
                self.assertEqual((await tool("document_list"))["total"], 0)
        connection_after = (await self.call("sessions"))["payload"][0]["connectionId"]
        self.assertEqual(connection_before, connection_after)
        async with self.http.get(self.origin + "/test/chat-sent") as response:
            self.assertEqual(await response.json(), [])

    async def test_stdio_mcp_reads_and_replies_over_the_registered_socket(self):
        from mcp import ClientSession, StdioServerParameters
        from mcp.client.stdio import stdio_client

        await self.pair()
        thread_id = str(uuid.uuid4())
        connected = await self.call("connect", name="chat-agent", project="swarm", harness="codex",
            mode="solo", workspace=self.temp.name, harness_session_id=thread_id, control=True)
        session_id = connected["session"]["registration"]["sessionId"]
        await self.eventually(lambda s: s["backends"][0]["sessions"][0]["state"] == "registered")
        original_connection = (await self.call("sessions"))["payload"][0]["connectionId"]
        params = StdioServerParameters(command=sys.executable, args=["-m", "swarm_client.cli",
            "--home", str(self.home), "mcp", "--backend", self.origin,
            "--workspace", self.temp.name, "--harness-session-id", thread_id])
        async with stdio_client(params) as (reader, writer):
            async with ClientSession(reader, writer) as mcp:
                await mcp.initialize()
                names = {tool.name for tool in (await mcp.list_tools()).tools}
                self.assertEqual(names, {"swarm_status", "discord_get_channel", "discord_read_messages", "discord_send_message"} | WORKSPACE_TOOLS)

                async def tool(name, **arguments):
                    result = await mcp.call_tool(name, arguments)
                    self.assertFalse(result.isError, result)
                    return json.loads(result.content[0].text)

                channel = await tool("discord_get_channel", channel_id="456")
                self.assertTrue(channel["isThread"])
                reading = asyncio.create_task(tool("discord_read_messages", channel_id="456", limit=2))
                # Agent acknowledgments and CLI calls still flow during a pending Discord read.
                async with self.http.post(self.origin + "/test/agent/discord-test-owner", json={
                    "sessionId": session_id, "prompt": "Keep working in the harness"}) as response:
                    self.assertEqual((await response.json())["status"], "queued")
                page = await reading
                self.assertEqual([m["id"] for m in page["messages"]], ["4", "5"])
                self.assertIn("🌍", page["messages"][0]["content"])
                older = await tool("discord_read_messages", channel_id="456", limit=2,
                    before_message_id=page["nextBeforeMessageId"])
                self.assertEqual([m["id"] for m in older["messages"]], ["2", "3"])
                sent = await tool("discord_send_message", channel_id="456", content="Tests passed 🌍",
                    reply_to_message_id="5")
                self.assertEqual(sent["url"], "https://discord.com/channels/1/456/900")
                await tool("discord_send_message", channel_id="123", content="General update")
                denied = await mcp.call_tool("discord_read_messages", {"channel_id": "789"})
                self.assertTrue(denied.isError)
                self.assertIn("discord_access_denied", denied.content[0].text)
                invalid = await mcp.call_tool("discord_send_message", {"channel_id": "123", "content": "x" * 1801})
                self.assertTrue(invalid.isError)
                await self.call("disconnect", session_id=session_id)
                offline = await mcp.call_tool("discord_send_message", {"channel_id": "123", "content": "too late"})
                self.assertTrue(offline.isError)
        async with self.http.get(self.origin + "/test/chat-sent") as response:
            sent_messages = await response.json()
        self.assertEqual(len(sent_messages), 2)
        self.assertEqual(sent_messages[0]["replyTo"], "5")
        self.assertEqual(sent_messages[0]["attribution"]["sessionId"], session_id)
        self.assertEqual(sent_messages[0]["attribution"]["agentName"], "chat-agent")
        self.assertEqual(sent_messages[0]["attribution"]["ownerId"], "discord-test-owner")
        self.assertEqual(sent_messages[0]["content"], "Tests passed 🌍")
        async with self.http.get(self.origin + "/diagnostics") as response:
            connections = (await response.json())["activeConnections"]
        self.assertEqual([c["id"] for c in connections], [original_connection])

    async def test_cli_saves_project_preferences_without_credentials(self):
        await self.pair()
        workspace = Path(self.temp.name) / "project"
        workspace.mkdir()

        async def cli(*arguments):
            process = await asyncio.create_subprocess_exec(
                sys.executable, "-m", "swarm_client.cli", "--home", str(self.home), *arguments,
                stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            output, error = await process.communicate()
            self.assertEqual(process.returncode, 0, error.decode() + output.decode())
            return json.loads(output)

        result = await cli("connect", "--workspace", str(workspace), "--backend", self.origin,
                           "--project", "swarm", "--harness", "codex", "--name", "my-agent",
                           "--harness-session-id", "live-thread", "--registration-only")
        self.assertEqual(result["status"], "registered")
        saved = json.loads((workspace / ".swarm/config.json").read_text())
        self.assertEqual(saved["backend"], self.origin)
        self.assertEqual(saved["project_id"], "swarm")
        self.assertNotIn("token", json.dumps(saved))
        again = await cli("connect", "--workspace", str(workspace), "--harness", "codex",
                          "--harness-session-id", "live-thread", "--registration-only")
        self.assertEqual(again["session"]["registration"]["sessionId"], result["session"]["registration"]["sessionId"])
        self.assertEqual(again["session"]["registration"]["name"], "my-agent")
        changed = await cli("connect", "--workspace", str(workspace), "--harness", "codex",
                            "--harness-session-id", "live-thread", "--registration-only", "--name", "renamed", "--mode", "swarm")
        self.assertEqual(changed["session"]["registration"]["name"], "renamed")
        self.assertEqual(changed["session"]["registration"]["mode"], "swarm")
        removed = await cli("disconnect", "--workspace", str(workspace), "--session",
                            result["session"]["registration"]["sessionId"])
        self.assertTrue(removed["removed"])

    async def test_connect_automatically_installs_mcp_and_routes_each_calling_thread(self):
        await self.pair()
        sessions = []
        for index in range(2):
            native = str(uuid.uuid4())
            connected = await self.call("connect", name=f"auto-{index}", project="swarm", harness="codex",
                mode="solo", workspace=self.temp.name, harness_session_id=native, control=True)
            self.assertTrue(connected["mcp"]["ready"], connected)
            sessions.append((native, connected["session"]["registration"]["sessionId"]))
        self.assertEqual(self.daemon.mcp_config["unrelated"], {"command": "preserve-me"})
        calls = [json.loads(line) for line in (Path(self.temp.name) / "calls.jsonl").read_text().splitlines()]
        self.assertEqual(sum(c["method"] == "config/value/write" for c in calls), 1)
        self.assertTrue(any(c["method"] == "config/mcpServer/reload" for c in calls))
        for native, session_id in sessions:
            result = await self.daemon.mcp_request(native, "call", {"tool": "swarm_status", "_meta": {"threadId": native}})
            self.assertFalse(result.get("isError"), result)
            self.assertEqual(json.loads(result["content"][0]["text"])["session"]["sessionId"], session_id)
            result = await self.daemon.mcp_request(native, "call", {"tool": "discord_send_message",
                "arguments": {"channel_id": "123", "content": "My own update"}, "_meta": {"threadId": native}})
            self.assertFalse(result.get("isError"), result)
        missing = await self.daemon.mcp_request(sessions[0][0], "call", {"tool": "swarm_status"})
        self.assertTrue(missing["isError"])
        self.assertIn("mcp_thread_identity_missing", missing["content"][0]["text"])
        async with self.http.get(self.origin + "/test/chat-sent") as response:
            posts = await response.json()
        self.assertEqual([p["attribution"]["sessionId"] for p in posts], [s[1] for s in sessions])

    async def test_mcp_setup_failures_preserve_delivery_and_existing_config(self):
        await self.pair()
        self.daemon.mcp_config["swarm"] = {"command": "someone-elses-server"}
        conflict = await self.call("connect", name="conflict", project="swarm", harness="codex", mode="solo",
            workspace=self.temp.name, harness_session_id=str(uuid.uuid4()), control=True)
        self.assertEqual(conflict["status"], "registered")
        self.assertEqual(conflict["mcp"]["error"], "mcp_config_conflict")
        self.assertEqual(self.daemon.mcp_config["swarm"], {"command": "someone-elses-server"})
        del self.daemon.mcp_config["swarm"]
        (Path(self.temp.name) / "no-mcp-reload").touch()
        unsupported = await self.call("connect", name="old-daemon", project="swarm", harness="codex", mode="solo",
            workspace=self.temp.name, harness_session_id=str(uuid.uuid4()), control=True)
        self.assertEqual(unsupported["status"], "registered")
        self.assertEqual(unsupported["mcp"]["error"], "mcp_live_reload_unavailable")
        self.assertFalse(unsupported["mcp"]["ready"])
        process = await asyncio.create_subprocess_exec(sys.executable, "-m", "swarm_client.cli",
            "--home", str(self.home), "connect", "--backend", self.origin, "--workspace", self.temp.name,
            "--project", "swarm", "--harness", "codex", "--harness-session-id", str(uuid.uuid4()),
            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        output, error = await process.communicate()
        self.assertFalse(error, error.decode())
        self.assertEqual(process.returncode, 1)
        self.assertEqual(json.loads(output)["mcp"]["error"], "mcp_live_reload_unavailable")

    async def test_cli_consumed_discord_request_completes_with_original_prompt_context(self):
        await self.pair()
        (Path(self.temp.name) / "cli-start-race").touch()
        (Path(self.temp.name) / "history-only").touch()
        connected = await self.call("connect", name="queue-agent", project="swarm", harness="codex", mode="solo",
            workspace=self.temp.name, harness_session_id=str(uuid.uuid4()), control=True)
        session_id = connected["session"]["registration"]["sessionId"]
        prompt = "Explain the test failure 🌍"
        async with self.http.post(self.origin + "/test/agent/discord-test-owner", json={
                "sessionId": session_id, "prompt": prompt}) as response:
            accepted = await response.json()
        self.assertEqual(accepted["status"], "queued")
        for _ in range(100):
            async with self.http.get(self.origin + "/test/agent-events") as response:
                events = await response.json()
            matching = [e for e in events if e["update"]["requestId"] == accepted["requestId"]]
            if any(e["update"]["status"] == "completed" for e in matching):
                break
            await asyncio.sleep(.05)
        else:
            self.fail(str(events))
        self.assertFalse(any(e["update"]["status"] == "unknown" for e in matching))
        completed = next(e for e in matching if e["update"]["status"] == "completed")
        self.assertEqual(completed["destination"]["prompt"], prompt)
        self.assertIn(prompt, completed["update"]["output"])


if __name__ == "__main__":
    unittest.main()
