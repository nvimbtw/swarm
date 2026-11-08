"""Queue and steer requests on an existing Codex daemon session."""
import asyncio
from collections.abc import Awaitable, Callable
from dataclasses import dataclass
from pathlib import Path

from .codex_rpc import CodexError, CodexRpc
from .protocol import MAX_SESSIONS, delivery_failure

EmitUpdate = Callable[[dict], Awaitable[None]]


@dataclass
class QueuedRequest:
    payload: dict
    emit: EmitUpdate
    is_current: Callable[[], bool]
    acknowledged: asyncio.Event | None


class CodexAdapter:
    def __init__(self, rpc: CodexRpc):
        self.rpc = rpc
        self.queues = {}
        self.workers = {}
        self.tasks = set()

    async def attach(self, session):
        await self.rpc.connect()
        thread_id = session["harness_session_id"]
        thread = (await self.rpc.call("thread/read", {"threadId": thread_id}))["thread"]
        if Path(thread["cwd"]).resolve() != Path(session["workspace"]).resolve():
            raise CodexError("codex_workspace_mismatch")
        if thread.get("canAcceptDirectInput") is None:
            raise CodexError("codex_session_not_loaded_on_daemon")
        if thread["canAcceptDirectInput"] is False:
            raise CodexError("codex_input_not_supported")
        if thread_id not in self.rpc.watched and len(self.rpc.watched) >= MAX_SESSIONS:
            raise CodexError("codex_session_limit")
        try:
            await self.rpc.call("thread/queue/list", {"threadId": thread_id, "limit": 1})
        except CodexError as error:
            raise CodexError("codex_queue_api_unavailable") from error
        # Subscribe only after confirming this daemon already owns the live thread.
        await self.rpc.call("thread/resume", {"threadId": thread_id, "excludeTurns": True})
        self.rpc.watched.add(thread_id)

    async def describe(self, session):
        await self.attach(session)
        thread = (await self.rpc.call("thread/read", {"threadId": session["harness_session_id"]}))["thread"]
        models, cursor = [], None
        for _ in range(5):
            result = await self.rpc.call("model/list", {"cursor": cursor, "limit": 100})
            models.extend({
                "id": model["model"],
                "name": model["displayName"][:100],
                "reasoning": [option["reasoningEffort"] for option in model["supportedReasoningEfforts"]],
            } for model in result["data"] if not model.get("hidden"))
            cursor = result.get("nextCursor")
            if not cursor:
                break
        return {"model": thread.get("model"), "reasoning": thread.get("reasoningEffort"),
                "models": models[:100]}

    async def submit(self, session, request, emit, is_current=lambda: True, acknowledged=None):
        options = await self.describe(session)
        self.validate_options(request, options)
        thread_id = session["harness_session_id"]
        if request["delivery"] == "inject":
            return await self.inject(thread_id, request, emit)
        queue = self.queues.setdefault(thread_id, asyncio.Queue(maxsize=20))
        if queue.full():
            raise CodexError("session_queue_full")
        queue.put_nowait(QueuedRequest(request, emit, is_current, acknowledged))
        worker = self.workers.get(thread_id)
        if worker is None or worker.done():
            self.workers[thread_id] = self.start_task(self.drain(thread_id, queue))
        return {"status": "queued"}

    @staticmethod
    def validate_options(request, options):
        if not (request.get("model") or request.get("reasoning")):
            return
        if request["delivery"] == "inject":
            raise CodexError("inject_cannot_change_model_or_reasoning")
        model_id = request.get("model") or options["model"]
        model = next((model for model in options["models"] if model["id"] == model_id), None)
        if model is None or (request.get("reasoning") and request["reasoning"] not in model["reasoning"]):
            raise CodexError("unsupported_model_or_reasoning")

    async def inject(self, thread_id, request, emit):
        turns = await self.rpc.call("thread/turns/list", {
            "threadId": thread_id, "limit": 1, "sortDirection": "desc", "itemsView": "notLoaded",
        })
        active = next((turn for turn in turns["data"] if turn["status"] == "inProgress"), None)
        if active is None:
            raise CodexError("no_active_turn_use_queue")
        record = self.rpc.track_turn(thread_id, active["id"])
        previous = set(record.messages)
        result = await self.rpc.call("turn/steer", {
            "threadId": thread_id, "expectedTurnId": active["id"],
            "input": self.prompt_input(request), "clientUserMessageId": request["requestId"],
        })
        self.start_task(self.observe(thread_id, result["turnId"], record, emit, previous))
        return {"status": "injected", "turnId": result["turnId"]}

    @staticmethod
    def prompt_input(request):
        source = f"[Swarm Discord request {request['requestId']}; sender {request['senderId']}]\n"
        return [{"type": "text", "text": source + request["prompt"]}]

    async def drain(self, thread_id, queue):
        while not queue.empty():
            queued = queue.get_nowait()
            try:
                if queued.acknowledged:
                    await queued.acknowledged.wait()
                await self.deliver_queued(thread_id, queued)
            except CodexError as error:
                await queued.emit(delivery_failure(str(error)))
            except (OSError, ValueError, KeyError, TypeError):
                await queued.emit({"status": "unknown", "error": "codex_protocol_error"})
            finally:
                queue.task_done()

    async def deliver_queued(self, thread_id, queued):
        await self.wait_until_idle(thread_id, queued.is_current, wait_for_native_queue=True)
        settings = {"threadId": thread_id}
        for source, target in (("model", "model"), ("reasoning", "effort")):
            if queued.payload.get(source):
                settings[target] = queued.payload[source]
        if not queued.is_current():
            raise CodexError("request_cancelled")
        if len(settings) > 1:
            await self.rpc.call("thread/settings/update", settings)
        response = await self.rpc.call("thread/queue/add", {
            "threadId": thread_id, "clientUserMessageId": queued.payload["requestId"],
            "input": self.prompt_input(queued.payload),
        })
        submission_id = response["queuedSubmission"]["id"]
        turn_id = await self.start_native_submission(thread_id, submission_id, queued.is_current)
        if turn_id is None:
            await queued.emit({"status": "unknown", "error": "native_queue_check_local_codex"})
            return
        await queued.emit({"status": "started", "turnId": turn_id})
        await self.observe(thread_id, turn_id, self.rpc.track_turn(thread_id, turn_id), queued.emit)

    async def wait_until_idle(self, thread_id, is_current, *, wait_for_native_queue=False):
        while is_current():
            thread = (await self.rpc.call("thread/read", {"threadId": thread_id}))["thread"]
            status = thread["status"]["type"]
            if status in {"notLoaded", "systemError"}:
                raise CodexError("codex_session_unavailable")
            if status == "idle":
                if not wait_for_native_queue:
                    return
                native = await self.rpc.call("thread/queue/list", {"threadId": thread_id, "limit": 1})
                if not native["data"]:
                    return
            await asyncio.sleep(0.5)
        raise CodexError("request_cancelled")

    async def start_native_submission(self, thread_id, submission_id, is_current):
        while is_current():
            try:
                result = await self.rpc.call("thread/queue/start", {
                    "threadId": thread_id, "queuedSubmissionId": submission_id,
                })
                return result["turn"]["id"]
            except CodexError as error:
                # Only a definite idle-precondition failure is safe to retry. A lost
                # reply or an entry consumed by the CLI must never be re-enqueued.
                if "already has an active or pending turn" not in error.rpc_message:
                    return None
                try:
                    await self.wait_until_idle(thread_id, is_current)
                except CodexError:
                    return None
        return None

    async def observe(self, thread_id, turn_id, record, emit, previous=frozenset()):
        await record.completed.wait()
        await emit({"status": record.status, "turnId": turn_id, "output": record.output_since(previous)})
        self.rpc.turns.pop((thread_id, turn_id), None)

    def start_task(self, coroutine):
        task = asyncio.create_task(coroutine)
        self.tasks.add(task)
        task.add_done_callback(self.tasks.discard)
        return task

    async def close(self):
        # Cancelling connector observers/workers does not cancel accepted Codex turns.
        for task in list(self.tasks):
            task.cancel()
        await asyncio.gather(*self.tasks, return_exceptions=True)
        await self.rpc.close()
