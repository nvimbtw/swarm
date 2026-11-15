"""Install one stateless Swarm MCP registration and refresh an existing native thread."""
import asyncio
import json
from pathlib import Path
import sys

from .codex_rpc import CodexError

SERVER_NAME = "swarm"
TOOLS = {"swarm_status", "discord_get_channel", "discord_read_messages", "discord_send_message"}


async def ensure_mcp(rpc, home, session):
    thread_id = session["harness_session_id"]
    home = str(Path(home).resolve())
    args = ["-m", "swarm_client.cli", "--home", home, "mcp", "--auto"]
    config = (await mcp_call(rpc, "config/read", {"cwd": session["workspace"], "includeLayers": False}))["config"]
    existing = config.get("mcp_servers", {}).get(SERVER_NAME)
    if existing is not None:
        old_args = existing.get("args", [])
        if not (old_args == args and existing.get("command") == sys.executable):
            # Never replace an unrelated server, another installation, or a manually pinned binding.
            raise CodexError("mcp_config_conflict")
    desired = {**(existing or {}), "command": sys.executable, "args": args, "enabled": True}
    changed = existing != desired
    if changed:
        await mcp_call(rpc, "config/value/write", {"keyPath": "mcp_servers.swarm", "value": desired,
                                             "mergeStrategy": "replace"})
    if changed or not await ready(rpc, thread_id):
        await mcp_call(rpc, "config/mcpServer/reload", None)
    # Reload refreshes loaded threads; startup can finish after the reload acknowledgment.
    for _ in range(30):
        if await ready(rpc, thread_id):
            result = await mcp_call(rpc, "mcpServer/tool/call", {
                "threadId": thread_id, "server": SERVER_NAME, "tool": "swarm_status", "arguments": {},
                "_meta": {"threadId": thread_id},
            })
            if result.get("isError"):
                raise CodexError("mcp_connection_check_failed")
            try:
                status = json.loads(next(c["text"] for c in result["content"] if c.get("type") == "text"))
                if (status["session"]["sessionId"] != session["registration"]["sessionId"] or
                        status.get("status") != "ok" or status.get("connection") != "connected"):
                    raise ValueError("wrong_attachment")
            except (KeyError, StopIteration, TypeError, ValueError):
                raise CodexError("mcp_connection_check_failed") from None
            return {"ready": True, "state": "enabled", "server": SERVER_NAME, "tools": sorted(TOOLS)}
        await asyncio.sleep(0.2)
    raise CodexError("mcp_tools_not_ready")


async def ready(rpc, thread_id):
    inventory = await mcp_call(rpc, "mcpServerStatus/list", {"threadId": thread_id,
        "serverName": SERVER_NAME, "detail": "toolsAndAuthOnly", "limit": 10})
    return any(server.get("name") == SERVER_NAME and server.get("runtimeStatus") == "connected" and
               not server.get("toolsError") and TOOLS <= set(server.get("tools", {}))
               for server in inventory["data"])


async def mcp_call(rpc, method, params):
    try:
        return await rpc.call(method, params)
    except CodexError:
        code = {"config/read": "mcp_config_unavailable", "config/value/write": "mcp_config_write_failed",
                "config/mcpServer/reload": "mcp_live_reload_unavailable",
                "mcpServerStatus/list": "mcp_inventory_unavailable",
                "mcpServer/tool/call": "mcp_connection_check_failed"}[method]
        raise CodexError(code) from None
