import asyncio
import contextlib
import fcntl
import json
from pathlib import Path
import random
import signal
import uuid

import aiohttp

from .storage import Storage, backend_origin


class BackendError(RuntimeError):
    pass


class BackendLink:
    def __init__(self, service, origin):
        self.service, self.origin = service, origin
        self.state = "connecting"
        self.sessions = {}
        self.pending = {}
        self.ws = None
        self.lock = asyncio.Lock()
        self.task = asyncio.create_task(self.run())

    async def run(self):
        delay = 1
        try:
            while True:
                credential = self.service.storage.credentials.get(self.origin)
                if not credential:
                    self.state = "pairing_required"
                    return
                try:
                    async with self.service.http.ws_connect(
                        self.origin + "/ws/sessions",
                        headers={"Authorization": "Bearer " + credential["token"]},
                        heartbeat=20, max_msg_size=64 * 1024,
                    ) as ws:
                        self.ws = ws
                        hello = await asyncio.wait_for(ws.receive_json(), 10)
                        if hello.get("type") != "hello" or hello.get("version") != 1:
                            raise BackendError("unsupported_protocol")
                        self.state = "connected"
                        delay = 1
                        reader = asyncio.create_task(self.receive(ws))
                        try:
                            async with self.lock:
                                for session in list(self.sessions.values()):
                                    await self.register(session)
                            await reader
                        finally:
                            reader.cancel()
                            await asyncio.gather(reader, return_exceptions=True)
                except aiohttp.WSServerHandshakeError as error:
                    if error.status == 401:
                        self.state = "pairing_required"
                        self.service.storage.credentials.pop(self.origin, None)
                        self.service.storage.save_credentials()
                        return
                    self.state = "disconnected"
                except (aiohttp.ClientError, OSError, TimeoutError, BackendError, ValueError):
                    self.state = "disconnected"
                finally:
                    self.ws = None
                    if self.state != "pairing_required":
                        self.state = "disconnected"
                    for future in self.pending.values():
                        if not future.done():
                            future.set_exception(BackendError("connection_lost"))
                    self.pending.clear()
                    for session in self.sessions.values():
                        if session["state"] != "rejected":
                            session["state"] = "pending"
                await asyncio.sleep(delay + random.random())
                delay = min(delay * 2, 30)
        finally:
            if self.state != "pairing_required":
                self.state = "disconnected"

    async def receive(self, ws):
        async for message in ws:
            if message.type != aiohttp.WSMsgType.TEXT:
                break
            result = json.loads(message.data)
            future = self.pending.get(result.get("id"))
            if future and not future.done():
                future.set_result(result)

    async def request(self, kind, payload=None):
        if self.ws is None or self.ws.closed:
            raise BackendError("backend_unavailable")
        request_id = str(uuid.uuid4())
        future = asyncio.get_running_loop().create_future()
        self.pending[request_id] = future
        try:
            await self.ws.send_json({"version": 1, "id": request_id, "type": kind, "payload": payload})
            result = await asyncio.wait_for(future, 10)
            if result.get("type") == "error":
                raise BackendError(result.get("error", "backend_error"))
            return result
        finally:
            self.pending.pop(request_id, None)

    async def register(self, session):
        try:
            await self.request("session.register", session["registration"])
            session["state"] = "registered"
        except BackendError as error:
            session["state"] = "rejected" if str(error) == "invalid_request" else "pending"
            if session["state"] == "pending":
                raise

    async def close(self):
        self.task.cancel()
        await asyncio.gather(self.task, return_exceptions=True)


class Connector:
    def __init__(self, storage, http):
        self.storage, self.http = storage, http
        self.links = {}
        self.pairings = {}

    async def post(self, origin, path, payload):
        async with self.http.post(origin + path, json=payload, allow_redirects=False) as response:
            if response.status != 200:
                raise BackendError(f"backend_http_{response.status}")
            return await response.json()

    async def dispatch(self, request):
        operation = request["operation"]
        if operation == "status":
            return {"status": "running", "machine": self.storage.config,
                    "backends": [{"backend": origin, "state": link.state,
                                  "sessions": list(link.sessions.values())}
                                 for origin, link in self.links.items()]}
        origin = backend_origin(request["backend"])
        if operation == "pair-start":
            if len(self.pairings) >= 50:
                self.pairings.clear()
            challenge = await self.post(origin, "/api/pairing/start", {
                "machineId": self.storage.config["machine_id"],
                "machineName": self.storage.config["machine_name"]})
            self.pairings[origin] = challenge
            return {"status": "pairing_required", "backend": origin,
                    "discord_command": "/swarm-pair pairing-id:" + challenge["pairingId"],
                    "expires_at": challenge["expiresAt"]}
        if operation == "pair-complete":
            challenge = self.pairings.get(origin)
            if challenge is None:
                raise BackendError("pairing_not_started")
            credential = await self.post(origin, "/api/pairing/complete", {
                "pairingId": challenge["pairingId"], "secret": challenge["secret"], "code": request["code"]})
            self.storage.credentials[origin] = credential
            self.storage.save_credentials()
            self.pairings.pop(origin, None)
            old = self.links.pop(origin, None)
            sessions = old.sessions if old else {}
            if old:
                await old.close()
            link = self.get_link(origin)
            link.sessions = sessions
            return {"status": "authenticated", "owner_id": credential["ownerId"]}
        if origin not in self.storage.credentials:
            return {"status": "pairing_required", "backend": origin}
        link = self.get_link(origin)
        if operation == "connect":
            async with link.lock:
                harness_id = request.get("harness_session_id")
                existing = next((s for s in link.sessions.values() if harness_id and
                                 s.get("harness_session_id") == harness_id and
                                 s["workspace"] == request["workspace"] and
                                 s["registration"]["harness"] == request["harness"] and
                                 s["registration"]["projectId"] == request["project"]), None)
                if existing:
                    if request.get("name_explicit"):
                        existing["registration"]["name"] = request["name"]
                    if request.get("mode_explicit"):
                        existing["registration"]["mode"] = request["mode"]
                    if link.state == "connected":
                        try:
                            await link.register(existing)
                        except (BackendError, TimeoutError, aiohttp.ClientError):
                            existing["state"] = "pending"
                            if link.ws:
                                await link.ws.close()
                    return {"status": existing["state"], "session": existing}
                if len(link.sessions) >= 100:
                    raise BackendError("session_limit")
                session_id = str(uuid.uuid4())
                session = {"registration": {"sessionId": session_id, "name": request["name"],
                           "projectId": request["project"], "harness": request["harness"], "mode": request["mode"]},
                           "workspace": request["workspace"], "harness_session_id": harness_id,
                           "state": "pending"}
                link.sessions[session_id] = session
                if link.state == "connected":
                    try:
                        await link.register(session)
                    except (BackendError, TimeoutError, aiohttp.ClientError):
                        session["state"] = "pending"
                        if link.ws:
                            await link.ws.close()
                return {"status": session["state"], "session": session}
        if operation == "disconnect":
            async with link.lock:
                session = link.sessions.pop(request["session_id"], None)
                if session and link.state == "connected":
                    try:
                        await link.request("session.unregister", {"sessionId": request["session_id"]})
                    except (BackendError, TimeoutError, aiohttp.ClientError):
                        # Close the transport so the backend cannot keep an orphan registration.
                        if link.ws:
                            await link.ws.close()
                return {"status": "disconnected", "removed": session is not None}
        if operation == "sessions":
            return await link.request("session.list")
        raise ValueError("Unknown operation")

    def get_link(self, origin):
        if origin not in self.links:
            self.links[origin] = BackendLink(self, origin)
        return self.links[origin]

    async def handle(self, reader, writer):
        try:
            line = await asyncio.wait_for(reader.readline(), 10)
            request = json.loads(line)
            result = await self.dispatch(request)
        except BackendError as error:
            result = {"status": "error", "error": str(error)}
        except (ValueError, KeyError, TypeError, aiohttp.ClientError, OSError, TimeoutError):
            # Never include exception strings: HTTP errors can contain credential-bearing headers.
            result = {"status": "error", "error": "request_failed",
                      "hint": "Check arguments, pairing state, and backend availability."}
        try:
            writer.write((json.dumps(result) + "\n").encode())
            await writer.drain()
        except (ConnectionError, OSError):
            pass
        finally:
            writer.close()
            with contextlib.suppress(ConnectionError, OSError):
                await writer.wait_closed()


async def serve(home):
    home = Path(home).expanduser().resolve()
    home.mkdir(parents=True, exist_ok=True, mode=0o700)
    home.chmod(0o700)
    # Lock before unlinking a socket, including sockets left behind by a crashed service.
    with (home / "connector.lock").open("a") as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise BackendError("A connector is already running for this home directory.") from None
        storage = Storage(home)
        storage.socket.unlink(missing_ok=True)
        async with aiohttp.ClientSession(timeout=aiohttp.ClientTimeout(total=15)) as http:
            connector = Connector(storage, http)
            server = await asyncio.start_unix_server(connector.handle, path=storage.socket, limit=64 * 1024)
            storage.socket.chmod(0o600)
            stopped = asyncio.Event()
            loop = asyncio.get_running_loop()
            for sig in (signal.SIGINT, signal.SIGTERM):
                loop.add_signal_handler(sig, stopped.set)
            print(json.dumps({"status": "listening", "socket": str(storage.socket)}), flush=True)
            try:
                async with server:
                    await stopped.wait()
            finally:
                await asyncio.gather(*(link.close() for link in connector.links.values()))
                storage.socket.unlink(missing_ok=True)
