"""Codex CLI control through its existing app-server daemon, never a second harness."""
import asyncio
import json
import os
from pathlib import Path
import uuid

import aiohttp

from . import __version__


class CodexError(RuntimeError):
    def __init__(self, code, *, rpc_message=""):
        super().__init__(code)
        self.rpc_message = rpc_message


class CodexRpc:
    def __init__(self, socket=None):
        self.socket = socket
        self.http = None
        self.ws = None
        self.pending = {}
        self.lock = asyncio.Lock()
        self.write_lock = asyncio.Lock()
        self.reader = None
        self.turns = {}
        self.watched = set()

    async def connect(self):
        async with self.lock:
            if self.ws and not self.ws.closed and self.reader and not self.reader.done():
                return
            await self.close()
            home = Path(os.environ.get("CODEX_HOME", str(Path.home() / ".codex"))).expanduser()
            socket = Path(self.socket).expanduser() if self.socket else home / "app-server-control/app-server-control.sock"
            try:
                # The daemon control socket carries WebSocket frames. The CLI's raw
                # byte proxy is not the app-server JSONL stdio transport.
                self.http = aiohttp.ClientSession(connector=aiohttp.UnixConnector(path=str(socket)),
                    timeout=aiohttp.ClientTimeout(total=8))
                self.ws = await self.http.ws_connect("http://localhost/", max_msg_size=8 * 1024 * 1024)
                self.reader = asyncio.create_task(self.receive())
                await self.call("initialize", {"clientInfo": {"name": "swarm_connector",
                    "title": "Swarm", "version": __version__},
                    "capabilities": {"experimentalApi": True}})
                await self.write({"method": "initialized"})
            except (OSError, aiohttp.ClientError, CodexError, TimeoutError):
                await self.close()
                raise CodexError("codex_unavailable_check_daemon_socket") from None

    async def write(self, message):
        async with self.write_lock:
            try:
                if not self.ws or self.ws.closed:
                    raise CodexError("codex_connection_lost")
                await self.ws.send_json(message)
            except (aiohttp.ClientError, OSError):
                raise CodexError("codex_connection_lost") from None

    async def call(self, method, params):
        request_id = str(uuid.uuid4())
        future = asyncio.get_running_loop().create_future()
        self.pending[request_id] = future
        try:
            await self.write({"id": request_id, "method": method, "params": params})
            return await asyncio.wait_for(future, 8)
        except TimeoutError:
            # A mutating request may have arrived. The caller must not replay it.
            raise CodexError("delivery_unknown") from None
        finally:
            self.pending.pop(request_id, None)

    async def receive(self):
        try:
            async for frame in self.ws:
                if frame.type != aiohttp.WSMsgType.TEXT:
                    break
                message = json.loads(frame.data)
                future = self.pending.get(message.get("id"))
                if future and not future.done():
                    if "error" in message:
                        future.set_exception(CodexError("codex_rejected",
                            rpc_message=message["error"].get("message", "")))
                    else:
                        future.set_result(message.get("result", {}))
                elif "method" in message and "id" not in message:
                    self.notification(message["method"], message.get("params", {}))
                # Approval/user-input requests stay in the local Codex UI. Never approve
                # them or forward their potentially sensitive arguments to Discord.
        except (aiohttp.ClientError, OSError, ValueError):
            pass
        finally:
            for future in list(self.pending.values()):
                if not future.done():
                    future.set_exception(CodexError("codex_connection_lost"))
            for record in self.turns.values():
                record["done"].set()

    def notification(self, method, params):
        thread_id = params.get("threadId")
        if thread_id not in self.watched:
            return
        turn_id = params.get("turnId") or params.get("turn", {}).get("id")
        if not turn_id:
            return
        key = (thread_id, turn_id)
        record = self.turns.setdefault(key, {"messages": {}, "status": "unknown", "done": asyncio.Event()})
        if method == "item/completed" and params.get("item", {}).get("type") == "agentMessage":
            item = params["item"]
            record["messages"][item["id"]] = item.get("text", "")[:16000]
            # Bound output memory, including very long or tool-heavy turns.
            if len(record["messages"]) > 20:
                record["messages"].pop(next(iter(record["messages"])))
        if method == "turn/completed":
            record["status"] = params["turn"]["status"]
            record["done"].set()
        if len(self.turns) > 200:
            for old_key in list(self.turns):
                if old_key != key and self.turns[old_key]["done"].is_set():
                    self.turns.pop(old_key)
                    break

    async def close(self):
        if self.reader:
            self.reader.cancel()
            await asyncio.gather(self.reader, return_exceptions=True)
            self.reader = None
        if self.ws:
            await self.ws.close()
            self.ws = None
        if self.http:
            await self.http.close()
            self.http = None


class CodexAdapter:
    def __init__(self, rpc):
        self.rpc = rpc
        self.queues = {}
        self.tasks = set()
        self.locks = {}

    async def attach(self, session):
        await self.rpc.connect()
        thread_id = session["harness_session_id"]
        thread = (await self.rpc.call("thread/read", {"threadId": thread_id}))["thread"]
        if Path(thread["cwd"]).resolve() != Path(session["workspace"]).resolve():
            raise CodexError("codex_workspace_mismatch")
        if thread.get("canAcceptDirectInput") is None:
            raise CodexError("codex_session_not_loaded_on_daemon")
        if thread.get("canAcceptDirectInput") is False:
            raise CodexError("codex_input_not_supported")
        try:
            await self.rpc.call("thread/queue/list", {"threadId": thread_id, "limit": 1})
        except CodexError as error:
            raise CodexError("codex_queue_api_unavailable") from error
        # Subscribe to the same live daemon session, preserving all harness policies.
        await self.rpc.call("thread/resume", {"threadId": thread_id, "excludeTurns": True})
        if thread_id not in self.rpc.watched and len(self.rpc.watched) >= 100:
            raise CodexError("codex_session_limit")
        self.rpc.watched.add(thread_id)

    async def describe(self, session):
        await self.attach(session)
        thread = (await self.rpc.call("thread/read", {"threadId": session["harness_session_id"]}))["thread"]
        models, cursor = [], None
        for _ in range(5):
            result = await self.rpc.call("model/list", {"cursor": cursor, "limit": 100})
            models.extend({"id": m["model"], "name": m["displayName"][:100],
                "reasoning": [r["reasoningEffort"] for r in m["supportedReasoningEfforts"]]}
                for m in result["data"] if not m.get("hidden"))
            cursor = result.get("nextCursor")
            if not cursor:
                break
        return {"model": thread.get("model"), "reasoning": thread.get("reasoningEffort"),
                "models": models[:100]}

    async def submit(self, session, request, emit, alive=lambda: True, ready=None):
        thread_id = session["harness_session_id"]
        options = await self.describe(session)
        model = request.get("model") or options["model"]
        if request.get("model") or request.get("reasoning"):
            selected = next((m for m in options["models"] if m["id"] == model), None)
            if not selected or (request.get("reasoning") and request["reasoning"] not in selected["reasoning"]):
                raise CodexError("unsupported_model_or_reasoning")
        if request["delivery"] == "inject":
            if request.get("model") or request.get("reasoning"):
                raise CodexError("inject_cannot_change_model_or_reasoning")
            # Page only the newest turn, rather than reading the private transcript.
            turns = await self.rpc.call("thread/turns/list", {"threadId": thread_id,
                "limit": 1, "sortDirection": "desc", "itemsView": "notLoaded"})
            active = next((t for t in turns["data"] if t["status"] == "inProgress"), None)
            if not active:
                raise CodexError("no_active_turn_use_queue")
            record = self.rpc.turns.setdefault((thread_id, active["id"]), {
                "messages": {}, "status": "unknown", "done": asyncio.Event()})
            previous = set(record["messages"])
            result = await self.rpc.call("turn/steer", {"threadId": thread_id,
                "expectedTurnId": active["id"], "input": self.input(request),
                "clientUserMessageId": request["requestId"]})
            task = asyncio.create_task(self.observe(thread_id, result["turnId"], record, emit, previous))
            self.tasks.add(task)
            task.add_done_callback(self.tasks.discard)
            return {"status": "injected", "turnId": result["turnId"]}
        queue = self.queues.setdefault(thread_id, asyncio.Queue(maxsize=20))
        if queue.full():
            raise CodexError("session_queue_full")
        queue.put_nowait((session, request, emit, alive, ready))
        if thread_id not in self.locks:
            self.locks[thread_id] = asyncio.Lock()
        task = asyncio.create_task(self.drain(thread_id, queue))
        self.tasks.add(task)
        task.add_done_callback(self.tasks.discard)
        return {"status": "queued"}

    @staticmethod
    def input(request):
        sender = request["senderId"]
        source = f"[Swarm Discord request {request['requestId']}; sender {sender}]\n"
        return [{"type": "text", "text": source + request["prompt"]}]

    async def drain(self, thread_id, queue):
        async with self.locks[thread_id]:
            while not queue.empty():
                session, request, emit, alive, ready = queue.get_nowait()
                try:
                    if ready:
                        await ready.wait()
                    # Wait for both local work and previously queued Codex input. This
                    # prevents changing the settings intended for an earlier request.
                    while True:
                        if not alive():
                            raise CodexError("request_cancelled")
                        thread = (await self.rpc.call("thread/read", {"threadId": thread_id}))["thread"]
                        native = await self.rpc.call("thread/queue/list", {"threadId": thread_id, "limit": 1})
                        if thread["status"]["type"] == "idle" and not native["data"]:
                            break
                        if thread["status"]["type"] in {"notLoaded", "systemError"}:
                            raise CodexError("codex_session_unavailable")
                        await asyncio.sleep(0.5)
                    settings = {"threadId": thread_id}
                    for key, target in (("model", "model"), ("reasoning", "effort")):
                        if request.get(key):
                            settings[target] = request[key]
                    if not alive():
                        raise CodexError("request_cancelled")
                    if len(settings) > 1:
                        await self.rpc.call("thread/settings/update", settings)
                    queued = await self.rpc.call("thread/queue/add", {"threadId": thread_id,
                        "clientUserMessageId": request["requestId"], "input": self.input(request)})
                    queued_id = queued["queuedSubmission"]["id"]
                    result = None
                    while alive():
                        try:
                            result = await self.rpc.call("thread/queue/start", {"threadId": thread_id,
                                "queuedSubmissionId": queued_id})
                            break
                        except CodexError as error:
                            if "already has an active or pending turn" in error.rpc_message:
                                # Atomic start rejected without consuming the queued input.
                                # Wait for idle before retrying that same native queue entry.
                                while alive():
                                    await asyncio.sleep(0.5)
                                    state = await self.rpc.call("thread/read", {"threadId": thread_id})
                                    if state["thread"]["status"]["type"] == "idle":
                                        break
                                continue
                            # A lost reply or an entry consumed locally is ambiguous. Never
                            # re-enqueue it or turn a queued request into a steer operation.
                            break
                    if result is None:
                        await emit({"status": "unknown", "error": "native_queue_check_local_codex"})
                        continue
                    turn_id = result["turn"]["id"]
                    await emit({"status": "started", "turnId": turn_id})
                    record = self.rpc.turns.setdefault((thread_id, turn_id), {
                        "messages": {}, "status": "unknown", "done": asyncio.Event()})
                    await self.observe(thread_id, turn_id, record, emit)
                except CodexError as error:
                    await emit({"status": "unknown" if str(error) in {
                        "delivery_unknown", "codex_connection_lost"} else "failed", "error": str(error)})
                except (OSError, ValueError, KeyError, TypeError):
                    await emit({"status": "unknown", "error": "codex_protocol_error"})
                except asyncio.CancelledError:
                    # Disconnecting the transport must not cancel an accepted Codex turn.
                    raise
                finally:
                    queue.task_done()

    async def observe(self, thread_id, turn_id, record, emit, previous=frozenset()):
        await record["done"].wait()
        await emit({"status": record["status"], "turnId": turn_id,
                    "output": "\n\n".join(text for key, text in record["messages"].items()
                                            if key not in previous)[-16000:]})
        self.rpc.turns.pop((thread_id, turn_id), None)

    async def close(self):
        for task in list(self.tasks):
            task.cancel()
        await asyncio.gather(*self.tasks, return_exceptions=True)
        await self.rpc.close()
