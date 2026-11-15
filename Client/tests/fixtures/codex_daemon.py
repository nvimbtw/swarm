"""A real Unix-socket WebSocket server implementing deterministic Codex replies."""
import json
import asyncio
from aiohttp import web
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client


class CodexDaemon:
    def __init__(self, root):
        self.root = root
        self.socket = root / "app-server-control" / "app-server-control.sock"
        self.queued = {}
        self.request_ids = {}
        self.history = {}
        self.runner = None
        self.mcp_config = {"unrelated": {"command": "preserve-me"}}
        self.mcp_active_config = {}
        self.mcp_clients = {}

    async def start(self):
        self.socket.parent.mkdir(parents=True, exist_ok=True)
        app = web.Application()
        app.router.add_get("/", self.handle)
        self.runner = web.AppRunner(app)
        await self.runner.setup()
        await web.UnixSite(self.runner, str(self.socket)).start()
        return self

    async def close(self):
        for queue, task in self.mcp_clients.values():
            queue.put_nowait(None)
        await asyncio.gather(*(task for queue, task in self.mcp_clients.values()))
        await self.runner.cleanup()

    async def mcp_request(self, thread, method, params):
        if thread not in self.mcp_clients:
            queue = asyncio.Queue()
            task = asyncio.create_task(self.mcp_worker(queue, self.mcp_active_config["swarm"]))
            self.mcp_clients[thread] = queue, task
        queue, task = self.mcp_clients[thread]
        future = asyncio.get_running_loop().create_future()
        queue.put_nowait((method, params, future))
        return await future

    async def mcp_worker(self, queue, config):
        async with stdio_client(StdioServerParameters(command=config["command"], args=config["args"])) as (reader, writer):
            async with ClientSession(reader, writer) as client:
                await client.initialize()
                while item := await queue.get():
                    method, params, future = item
                    if method == "list":
                        response = {"data": [{"name": "swarm", "runtimeStatus": "connected",
                            "tools": {t.name: {} for t in (await client.list_tools()).tools}}]}
                    else:
                        result = await client.call_tool(params["tool"], params.get("arguments", {}), meta=params.get("_meta"))
                        response = result.model_dump(by_alias=True, exclude_none=True)
                    if not future.done():
                        future.set_result(response)

    async def complete_submission(self, ws, thread, turn_id, notify=True):
        items = [
            {"type": "userMessage", "id": "user-" + turn_id, "clientId": self.request_ids[thread],
             "content": [{"type": "text", "text": self.queued[thread]}]},
            {"type": "agentMessage", "id": "progress-" + turn_id, "phase": "commentary", "text": "Local progress"},
            {"type": "agentMessage", "id": "answer-" + turn_id, "phase": "final_answer",
             "text": "Answer: " + self.queued[thread]},
        ]
        turn = {"id": turn_id, "status": "completed", "items": items}
        self.history.setdefault(thread, []).insert(0, turn)
        if notify:
            for item in items:
                await ws.send_json({"method": "item/completed", "params": {"threadId": thread,
                    "turnId": turn_id, "item": item}})
            await ws.send_json({"method": "turn/completed", "params": {"threadId": thread,
                "turn": {"id": turn_id, "status": "completed"}}})

    async def handle(self, request):
        ws = web.WebSocketResponse()
        await ws.prepare(request)  # Requires the same HTTP Upgrade as the real daemon.
        async for frame in ws:
            message = json.loads(frame.data)
            method, params = message["method"], message.get("params") or {}
            with (self.root / "calls.jsonl").open("a") as log:
                log.write(json.dumps(message) + "\n")
            if "id" not in message:
                continue
            thread = params.get("threadId")
            busy = (self.root / "busy").exists()
            result = {}
            if method == "config/mcpServer/reload" and (self.root / "no-mcp-reload").exists():
                await ws.send_json({"id": message["id"], "error": {"code": -32601, "message": "Method not found"}})
                continue
            if method == "config/read":
                result = {"config": {"mcp_servers": self.mcp_config}}
            elif method == "config/value/write":
                self.mcp_config["swarm"] = params["value"]
            elif method == "config/mcpServer/reload":
                self.mcp_active_config = dict(self.mcp_config)
            elif method == "mcpServerStatus/list":
                if "swarm" in self.mcp_active_config:
                    result = await self.mcp_request(thread, "list", params)
                else:
                    result = {"data": []}
            elif method == "mcpServer/tool/call":
                result = await self.mcp_request(thread, "call", params)
            elif method == "thread/read":
                unloaded = (self.root / "unloaded").exists()
                result = {"thread": {"id": thread, "cwd": str(self.root), "model": "test-model",
                    "reasoningEffort": "medium", "canAcceptDirectInput": None if unloaded else True,
                    "status": {"type": "notLoaded" if unloaded else "active" if busy else "idle"}}}
            elif method == "model/list":
                result = {"data": [{"model": "test-model", "displayName": "Test model",
                    "supportedReasoningEfforts": [{"reasoningEffort": "medium"}, {"reasoningEffort": "high"}]}]}
            elif method == "thread/queue/list":
                result = {"data": []}
            elif method == "thread/turns/list":
                turns = ([{"id": "local-turn", "status": "inProgress"}] if busy else []) + self.history.get(thread, [])
                result = {"data": turns[:params.get("limit", 10)]}
            elif method == "thread/queue/add":
                self.queued[thread] = params["input"][0]["text"]
                self.request_ids[thread] = params["clientUserMessageId"]
                result = {"queuedSubmission": {"id": "native-" + params["clientUserMessageId"]}}
                if (self.root / "cli-before-add-reply").exists():
                    await self.complete_submission(ws, thread, "cli-early")
                if (self.root / "add-response-error").exists():
                    await self.complete_submission(ws, thread, "cli-add", notify=False)
                    await ws.send_json({"id": message["id"], "error": {"message": "reply lost after acceptance", "code": -32600}})
                    continue
            elif method == "thread/queue/start":
                turn_id = "turn-" + params["queuedSubmissionId"]
                error = None
                if (self.root / "cli-start-race").exists():
                    await self.complete_submission(ws, thread, "cli-turn", notify=not (self.root / "history-only").exists())
                    error = "queued submission not found"
                if (self.root / "race").exists():
                    error = "reply unavailable after native queue acceptance"
                if (self.root / "busy-race").exists():
                    (self.root / "busy-race").unlink()
                    error = "thread already has an active or pending turn"
                if error:
                    await ws.send_json({"id": message["id"], "error": {"message": error, "code": -32600}})
                    continue
                result = {"turn": {"id": turn_id, "status": "inProgress"}}
                # Completion can race ahead of the request's RPC reply.
                await self.complete_submission(ws, thread, turn_id)
            elif method == "turn/steer":
                result = {"turnId": params["expectedTurnId"]}
            await ws.send_json({"id": message["id"], "result": result})
        return ws
