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

import aiohttp

from swarm_client.cli import rpc
from swarm_client.storage import backend_origin
from swarm_client.backend import BackendLink
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


if __name__ == "__main__":
    unittest.main()
