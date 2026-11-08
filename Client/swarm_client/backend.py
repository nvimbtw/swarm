"""One authenticated backend connection, shared by all attached sessions."""
import asyncio
import json
import random
import uuid

import aiohttp

from .codex_rpc import CodexError
from .protocol import MAX_MESSAGE_BYTES, MAX_PROMPT_CHARS, delivery_failure, encode_message


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
                        heartbeat=20, max_msg_size=MAX_MESSAGE_BYTES,
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
        async with self.send_lock:
            await ws.send_str(encode_message(message))

    async def reply_agent(self, ws, message, response):
        payload = message.get("payload", {})
        await self.send(ws, {"version": 1, "id": message["id"], "type": "agent.reply",
            "payload": {"requestId": payload.get("requestId"), "sessionId": payload.get("sessionId"), **response}})

    async def agent_request(self, ws, message):
        acknowledged = asyncio.Event()
        response_future = None
        try:
            try:
                payload = message["payload"]
                request_id = str(uuid.UUID(payload["requestId"]))
                if request_id != message["id"]:
                    raise CodexError("invalid_request")
                cached = self.handled.get(request_id)
                if cached is not None:
                    await self.reply_agent(ws, message, await asyncio.shield(cached))
                    return
                response_future = asyncio.get_running_loop().create_future()
                self.handled[request_id] = response_future
                self.evict_old_replies(request_id)
                response = await self.execute_agent_request(ws, message, acknowledged)
            except (CodexError, ValueError, KeyError, TypeError) as error:
                code = str(error) if isinstance(error, CodexError) else "invalid_request"
                response = delivery_failure(code)
            except (OSError, TimeoutError):
                response = delivery_failure("delivery_unknown")
            if response_future is not None and not response_future.done():
                response_future.set_result(response)
            await self.reply_agent(ws, message, response)
        except (aiohttp.ClientError, OSError):
            pass
        finally:
            # Events cannot overtake the acknowledgment, even for an instant Codex turn.
            acknowledged.set()
            if response_future is not None and not response_future.done():
                response_future.cancel()

    def evict_old_replies(self, current_id):
        if len(self.handled) <= 500:
            return
        for request_id, future in self.handled.items():
            if request_id != current_id and future.done():
                self.handled.pop(request_id)
                return

    def can_deliver(self, ws, session, payload):
        if session is None or self.ws is not ws or ws.closed:
            return False
        if self.sessions.get(payload["sessionId"]) is not session:
            return False
        registration = session["registration"]
        owner_id = self.service.storage.credentials.get(self.origin, {}).get("ownerId")
        return registration.get("control", False) and (
            registration["mode"] == "swarm" or payload.get("senderId") == owner_id)

    async def execute_agent_request(self, ws, message, acknowledged):
        payload = message["payload"]
        session = self.sessions.get(payload["sessionId"])
        is_current = lambda: self.can_deliver(ws, session, payload)
        if not is_current():
            raise CodexError("session_unavailable_or_not_allowed")
        adapter = self.service.adapters.get(session.get("codex_socket"))
        if adapter is None:
            raise CodexError("codex_unavailable")
        if message["type"] == "agent.describe":
            return {"status": "available", **await adapter.describe(session)}
        prompt = payload.get("prompt")
        if (not isinstance(prompt, str) or not prompt.strip() or len(prompt) > MAX_PROMPT_CHARS
                or payload.get("delivery") not in {"queue", "inject"}):
            raise CodexError("invalid_request")

        async def emit(update):
            await acknowledged.wait()
            await self.publish_agent_update(ws, payload, update)

        return await adapter.submit(session, payload, emit, is_current, acknowledged)

    async def publish_agent_update(self, ws, request, update):
        if self.ws is not ws or ws.closed:
            return
        try:
            await self.send(ws, {
                "version": 1, "id": str(uuid.uuid4()), "type": "agent.event",
                "payload": {"requestId": request["requestId"], "sessionId": request["sessionId"], **update},
            })
        except (aiohttp.ClientError, OSError):
            pass  # Accepted work is never replayed onto a replacement connection.

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
