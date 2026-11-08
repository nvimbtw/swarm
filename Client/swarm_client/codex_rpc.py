"""Codex CLI control through its existing app-server daemon, never a second harness."""
import asyncio
import json
import os
from dataclasses import dataclass, field
from pathlib import Path
import uuid

import aiohttp

from . import __version__
from .protocol import MAX_OUTPUT_CHARS, MAX_SESSIONS


class CodexError(RuntimeError):
    def __init__(self, code, *, rpc_message=""):
        super().__init__(code)
        self.rpc_message = rpc_message


@dataclass
class TurnResult:
    messages: dict[str, str] = field(default_factory=dict)
    status: str = "unknown"
    completed: asyncio.Event = field(default_factory=asyncio.Event)

    def output_since(self, previous):
        return "\n\n".join(text for key, text in self.messages.items()
                            if key not in previous)[-MAX_OUTPUT_CHARS:]


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
                record.completed.set()

    def track_turn(self, thread_id, turn_id):
        key = (thread_id, turn_id)
        if key not in self.turns:
            self.turns[key] = TurnResult()
        return self.turns[key]

    def notification(self, method, params):
        thread_id = params.get("threadId")
        if thread_id not in self.watched:
            return
        turn_id = params.get("turnId") or params.get("turn", {}).get("id")
        if not turn_id:
            return
        key = (thread_id, turn_id)
        record = self.track_turn(thread_id, turn_id)
        if method == "item/completed" and params.get("item", {}).get("type") == "agentMessage":
            item = params["item"]
            record.messages[item["id"]] = item.get("text", "")[:MAX_OUTPUT_CHARS]
            # Bound output memory, including very long or tool-heavy turns.
            if len(record.messages) > 20:
                record.messages.pop(next(iter(record.messages)))
        if method == "turn/completed":
            record.status = params["turn"]["status"]
            record.completed.set()
        if len(self.turns) > MAX_SESSIONS * 2:
            for old_key in list(self.turns):
                if old_key != key and self.turns[old_key].completed.is_set():
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
