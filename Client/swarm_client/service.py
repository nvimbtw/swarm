import asyncio
import contextlib
import fcntl
import json
from pathlib import Path
import signal
import uuid

import aiohttp

from . import __version__
from .backend import BackendError, BackendLink
from .codex import CodexAdapter
from .codex_rpc import CodexError, CodexRpc
from .protocol import MAX_MESSAGE_BYTES, MAX_SESSIONS
from .storage import Storage, backend_origin


class Connector:
    def __init__(self, storage, http):
        self.storage, self.http = storage, http
        self.links = {}
        self.pairings = {}
        self.adapters = {}

    async def post(self, origin, path, payload):
        async with self.http.post(origin + path, json=payload, allow_redirects=False) as response:
            if response.status != 200:
                raise BackendError(f"backend_http_{response.status}")
            return await response.json()

    async def dispatch(self, request):
        operation = request["operation"]
        if operation == "status":
            return {"status": "running", "version": __version__, "machine": self.storage.config,
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
            return await self.connect_session(link, request)
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

    async def connect_session(self, link, request):
        async with link.lock:
            harness_id = request.get("harness_session_id")
            existing = next((s for s in link.sessions.values() if harness_id and
                             s.get("harness_session_id") == harness_id and
                             s["workspace"] == request["workspace"] and
                             s["registration"]["harness"] == request["harness"] and
                             s["registration"]["projectId"] == request["project"]), None)
            control = bool(request.get("control"))
            socket = request.get("codex_socket")
            if control:
                if request["harness"] != "codex" or not harness_id:
                    raise BackendError("codex_control_requires_thread_id")
                try:
                    uuid.UUID(harness_id)
                    adapter = self.adapters.setdefault(socket, CodexAdapter(CodexRpc(socket)))
                    await adapter.attach({"harness_session_id": harness_id, "workspace": request["workspace"]})
                except (ValueError, CodexError) as error:
                    raise BackendError(str(error) if isinstance(error, CodexError) else "invalid_codex_thread_id") from None
            if existing:
                if "control" in request:
                    existing["registration"]["control"] = control
                    existing["codex_socket"] = socket if control else None
                if request.get("name_explicit"):
                    existing["registration"]["name"] = request["name"]
                if request.get("mode_explicit"):
                    existing["registration"]["mode"] = request["mode"]
                await self.register_when_connected(link, existing)
                return {"status": existing["state"], "session": existing}
            if len(link.sessions) >= MAX_SESSIONS:
                raise BackendError("session_limit")
            session_id = str(uuid.uuid4())
            session = {
                "registration": {
                    "sessionId": session_id, "name": request["name"], "projectId": request["project"],
                    "harness": request["harness"], "mode": request["mode"], "control": control,
                },
                "workspace": request["workspace"], "harness_session_id": harness_id,
                "codex_socket": socket, "state": "pending",
            }
            link.sessions[session_id] = session
            await self.register_when_connected(link, session)
            return {"status": session["state"], "session": session}

    @staticmethod
    async def register_when_connected(link, session):
        if link.state != "connected":
            return
        try:
            await link.register(session)
        except (BackendError, TimeoutError, aiohttp.ClientError):
            session["state"] = "pending"
            if link.ws:
                await link.ws.close()

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
            server = await asyncio.start_unix_server(connector.handle, path=storage.socket, limit=MAX_MESSAGE_BYTES)
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
                await asyncio.gather(*(adapter.close() for adapter in connector.adapters.values()))
                storage.socket.unlink(missing_ok=True)
