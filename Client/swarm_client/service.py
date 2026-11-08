import asyncio
import contextlib
import fcntl
import json
from pathlib import Path
import random
import signal
import uuid

import aiohttp

from . import __version__
from .storage import Storage, backend_origin
from .codex import CodexAdapter, CodexRpc, CodexError


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
        self.send_lock = asyncio.Lock()
        self.incoming = set()
        self.handled = {}
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
                        self.handled.clear()
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
            if result.get("version") == 1 and result.get("type") in {"agent.submit", "agent.describe"}:
                if len(self.incoming) >= 100:
                    await self.reply_agent(ws, result, {"status": "failed", "error": "connector_busy"})
                    continue
                task = asyncio.create_task(self.agent_request(ws, result))
                self.incoming.add(task)
                task.add_done_callback(self.incoming.discard)
                continue
            future = self.pending.get(result.get("id"))
            if future and not future.done():
                future.set_result(result)

    async def send(self, ws, message):
        # Enforce the wire limit after JSON escaping, including non-ASCII output.
        encoded = json.dumps(message, ensure_ascii=False)
        while len(encoded.encode()) > 64 * 1024:
            payload = message.get("payload", {})
            if payload.get("output"):
                payload["output"] = payload["output"][:len(payload["output"]) // 2]
                payload["truncated"] = True
            elif payload.get("models"):
                payload["models"].pop()
            else:
                raise ValueError("message_too_large")
            encoded = json.dumps(message, ensure_ascii=False)
        async with self.send_lock:
            await ws.send_str(encoded)

    async def reply_agent(self, ws, message, response):
        payload = message.get("payload", {})
        await self.send(ws, {"version": 1, "id": message["id"], "type": "agent.reply",
            "payload": {"requestId": payload.get("requestId"), "sessionId": payload.get("sessionId"), **response}})

    async def agent_request(self, ws, message):
        ready = asyncio.Event()
        try:
            payload = message["payload"]
            request_id = str(uuid.UUID(payload["requestId"]))
            if request_id != message["id"]:
                raise CodexError("invalid_request")
            if request_id in self.handled:
                response = await self.handled[request_id]
                await self.reply_agent(ws, message, response)
                return
            future = asyncio.get_running_loop().create_future()
            self.handled[request_id] = future
            # Keep in-flight IDs as well as recent acknowledgments; never submit a duplicate.
            if len(self.handled) > 500:
                for old_id, old in list(self.handled.items()):
                    if old.done() and old_id != request_id:
                        self.handled.pop(old_id)
                        break
            session = self.sessions.get(payload["sessionId"])
            credential = self.service.storage.credentials.get(self.origin, {})
            def alive():
                return (self.ws is ws and not ws.closed and
                    self.sessions.get(payload["sessionId"]) is session and session is not None and
                    session["registration"].get("control", False) and
                    (session["registration"]["mode"] == "swarm" or
                     payload.get("senderId") == credential.get("ownerId")))
            if not session or not session["registration"].get("control") or not alive():
                raise CodexError("session_unavailable_or_not_allowed")
            adapter = self.service.adapters.get(session.get("codex_socket"))
            if not adapter:
                raise CodexError("codex_unavailable")
            async def emit(update):
                await ready.wait()
                if self.ws is ws and not ws.closed:
                    try:
                        await self.send(ws, {"version": 1, "id": str(uuid.uuid4()), "type": "agent.event",
                            "payload": {"requestId": request_id, "sessionId": payload["sessionId"], **update}})
                    except (aiohttp.ClientError, OSError):
                        pass  # Never replay accepted work after a transport failure.
            if message["type"] == "agent.describe":
                response = {"status": "available", **await adapter.describe(session)}
            else:
                if (not isinstance(payload.get("prompt"), str) or not payload["prompt"].strip() or
                        len(payload["prompt"]) > 4000 or payload.get("delivery") not in {"queue", "inject"}):
                    raise CodexError("invalid_request")
                response = await adapter.submit(session, payload, emit, alive, ready)
        except (CodexError, ValueError, KeyError, TypeError) as error:
            code = str(error) if isinstance(error, CodexError) else "invalid_request"
            response = {"status": "unknown" if code in {"delivery_unknown", "codex_connection_lost"} else "failed",
                        "error": code}
        except (OSError, TimeoutError):
            response = {"status": "unknown", "error": "delivery_unknown"}
        try:
            if "future" in locals() and not future.done():
                future.set_result(response)
            await self.reply_agent(ws, message, response)
        except (aiohttp.ClientError, OSError):
            pass
        finally:
            ready.set()

    async def request(self, kind, payload=None):
        if self.ws is None or self.ws.closed:
            raise BackendError("backend_unavailable")
        request_id = str(uuid.uuid4())
        future = asyncio.get_running_loop().create_future()
        self.pending[request_id] = future
        try:
            await self.send(self.ws, {"version": 1, "id": request_id, "type": kind, "payload": payload})
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
        for task in list(self.incoming):
            task.cancel()
        await asyncio.gather(*self.incoming, return_exceptions=True)


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
                           "projectId": request["project"], "harness": request["harness"], "mode": request["mode"], "control": control},
                           "workspace": request["workspace"], "harness_session_id": harness_id,
                           "codex_socket": socket,
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
                await asyncio.gather(*(adapter.close() for adapter in connector.adapters.values()))
                storage.socket.unlink(missing_ok=True)
