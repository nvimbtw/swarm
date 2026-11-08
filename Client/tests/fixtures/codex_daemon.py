"""A real Unix-socket WebSocket server implementing deterministic Codex replies."""
import json
from aiohttp import web


class CodexDaemon:
    def __init__(self, root):
        self.root = root
        self.socket = root / "app-server-control" / "app-server-control.sock"
        self.queued = {}
        self.runner = None

    async def start(self):
        self.socket.parent.mkdir(parents=True, exist_ok=True)
        app = web.Application()
        app.router.add_get("/", self.handle)
        self.runner = web.AppRunner(app)
        await self.runner.setup()
        await web.UnixSite(self.runner, str(self.socket)).start()
        return self

    async def close(self):
        await self.runner.cleanup()

    async def handle(self, request):
        ws = web.WebSocketResponse()
        await ws.prepare(request)  # Requires the same HTTP Upgrade as the real daemon.
        async for frame in ws:
            message = json.loads(frame.data)
            method, params = message["method"], message.get("params", {})
            with (self.root / "calls.jsonl").open("a") as log:
                log.write(json.dumps(message) + "\n")
            if "id" not in message:
                continue
            thread = params.get("threadId")
            busy = (self.root / "busy").exists()
            result = {}
            if method == "thread/read":
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
                result = {"data": [{"id": "local-turn", "status": "inProgress"}] if busy else []}
            elif method == "thread/queue/add":
                self.queued[thread] = params["input"][0]["text"]
                result = {"queuedSubmission": {"id": "native-" + params["clientUserMessageId"]}}
            elif method == "thread/queue/start":
                turn_id = "turn-" + params["queuedSubmissionId"]
                error = None
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
                await ws.send_json({"method": "item/completed", "params": {"threadId": thread,
                    "turnId": turn_id, "item": {"type": "agentMessage", "id": "answer",
                    "text": "Answer: " + self.queued[thread]}}})
                await ws.send_json({"method": "turn/completed", "params": {"threadId": thread,
                    "turn": {"id": turn_id, "status": "completed"}}})
            elif method == "turn/steer":
                result = {"turnId": params["expectedTurnId"]}
            await ws.send_json({"id": message["id"], "result": result})
        return ws
