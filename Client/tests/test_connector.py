import asyncio
import json
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
import unittest

import aiohttp

from swarm_client.cli import rpc
from swarm_client.storage import backend_origin


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


@unittest.skipUnless(FIXTURE.exists(), "Build Backend/Swarm.Server.Tests first")
class ConnectorIntegrationTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.home = Path(self.temp.name) / "state"
        self.backend = await asyncio.create_subprocess_exec(
            "dotnet", str(FIXTURE), "--serve", stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        self.origin = (await asyncio.wait_for(self.backend.stdout.readline(), 15)).decode().strip()
        self.assertTrue(self.origin.startswith("http://127.0.0.1:"), self.origin)
        self.service = await asyncio.create_subprocess_exec(
            sys.executable, "-m", "swarm_client.cli", "--home", str(self.home), "serve",
            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
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
                           "--harness-session-id", "live-thread")
        self.assertEqual(result["status"], "registered")
        saved = json.loads((workspace / ".swarm/config.json").read_text())
        self.assertEqual(saved["backend"], self.origin)
        self.assertEqual(saved["project_id"], "swarm")
        self.assertNotIn("token", json.dumps(saved))
        again = await cli("connect", "--workspace", str(workspace), "--harness", "codex",
                          "--harness-session-id", "live-thread")
        self.assertEqual(again["session"]["registration"]["sessionId"], result["session"]["registration"]["sessionId"])
        self.assertEqual(again["session"]["registration"]["name"], "my-agent")
        changed = await cli("connect", "--workspace", str(workspace), "--harness", "codex",
                            "--harness-session-id", "live-thread", "--name", "renamed", "--mode", "swarm")
        self.assertEqual(changed["session"]["registration"]["name"], "renamed")
        self.assertEqual(changed["session"]["registration"]["mode"], "swarm")
        removed = await cli("disconnect", "--workspace", str(workspace), "--session",
                            result["session"]["registration"]["sessionId"])
        self.assertTrue(removed["removed"])


if __name__ == "__main__":
    unittest.main()
