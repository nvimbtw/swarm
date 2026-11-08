import argparse
import asyncio
import json
import os
from pathlib import Path
import sys

from .storage import backend_origin, project_settings, write_json


async def rpc(home, request):
    reader, writer = await asyncio.open_unix_connection(str(home / "connector.sock"), limit=1024 * 1024)
    try:
        writer.write((json.dumps(request) + "\n").encode())
        await writer.drain()
        return json.loads(await asyncio.wait_for(reader.readline(), 20))
    finally:
        writer.close()
        await writer.wait_closed()


async def execute(args, parser):
    home = Path(args.home).expanduser().resolve()
    if args.command == "serve":
        from .service import serve
        await serve(home)
        return
    if args.command == "status":
        return await rpc(home, {"operation": "status"})
    workspace = Path(args.workspace).expanduser().resolve()
    saved = project_settings(workspace)
    backend = args.backend or saved.get("backend")
    if not backend:
        parser.error("Supply --backend or save project settings with connect first.")
    backend = backend_origin(backend)
    request = {"operation": args.command, "backend": backend}
    if args.command == "pair-complete":
        code = sys.stdin.readline().strip() if args.code_stdin else input("Discord pairing code: ").strip()
        if len(code) != 8 or not code.isascii() or not code.isdigit():
            raise ValueError("The pairing code must contain eight digits.")
        request["code"] = code
    if args.command == "connect":
        project = args.project or saved.get("project_id")
        if not project:
            parser.error("Supply --project (a project label for this prototype).")
        if not workspace.is_dir():
            raise ValueError("Workspace does not exist.")
        harness = args.harness
        name = args.name or saved.get("session_name_prefix", workspace.name) + "-" + harness
        mode = args.mode or saved.get("mode", "solo")
        for value, limit in ((name, 80), (project, 100), (harness, 40)):
            if not value.strip() or len(value) > limit or any(ord(c) < 32 for c in value):
                raise ValueError("Invalid session name, project, or harness.")
        harness_id = args.harness_session_id
        if not harness_id and harness == "codex":
            harness_id = os.environ.get("CODEX_THREAD_ID")
        control = args.control if args.control is not None else harness == "codex"
        request.update(workspace=str(workspace), project=project, name=name, harness=harness,
                       mode=mode, harness_session_id=harness_id,
                       name_explicit=args.name is not None, mode_explicit=args.mode is not None,
                       control=control, codex_socket=args.codex_socket)
    if args.command == "disconnect":
        request["session_id"] = args.session
    result = await rpc(home, request)
    if args.command == "connect" and "session" in result:
        write_json(workspace / ".swarm" / "config.json", {
            "version": 1, "backend": backend, "project_id": project,
            "session_name_prefix": saved.get("session_name_prefix", workspace.name), "mode": mode})
        # Return backend confirmation when possible, rather than treating a queued registration as connected.
        session_id = result["session"]["registration"]["sessionId"]
        for _ in range(40):
            if result["status"] != "pending":
                break
            await asyncio.sleep(0.25)
            status = await rpc(home, {"operation": "status"})
            link = next((b for b in status["backends"] if b["backend"] == backend), None)
            if link and link["state"] == "pairing_required":
                return {"status": "pairing_required", "backend": backend}
            session = next((s for s in link["sessions"] if s["registration"]["sessionId"] == session_id), None) if link else None
            if session:
                result = {"status": session["state"], "session": session}
    if args.command == "connect" and "session" in result:
        controlled = result["session"]["registration"].get("control", False)
        result["delivery"] = {"ready": controlled and result["status"] == "registered",
            "state": "enabled" if controlled else "registration_only"}
        if not controlled:
            result["delivery"]["hint"] = "This session cannot appear in Discord /agent. Reconnect with Codex control enabled."
    return result


def main():
    parser = argparse.ArgumentParser(description="Local Swarm connector (Linux/macOS)")
    parser.add_argument("--home", default=os.environ.get("SWARM_HOME", str(Path.home() / ".swarm")))
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("serve", help="Run the shared local service in the foreground")
    commands.add_parser("status", help="Inspect the local service and its session states")
    for command in ("pair-start", "pair-complete", "connect", "disconnect", "sessions"):
        sub = commands.add_parser(command)
        sub.add_argument("--backend", help="HTTP(S) backend origin, e.g. http://localhost:5142")
        sub.add_argument("--workspace", default=str(Path.cwd()))
        if command == "pair-complete":
            sub.add_argument("--code-stdin", action="store_true", help="Read the code from stdin without a prompt")
        if command == "connect":
            sub.add_argument("--project")
            sub.add_argument("--name")
            sub.add_argument("--harness", required=True)
            sub.add_argument("--harness-session-id")
            delivery = sub.add_mutually_exclusive_group()
            delivery.add_argument("--control", dest="control", action="store_true", default=None,
                                  help="Verify and enable Discord delivery (default for Codex)")
            delivery.add_argument("--registration-only", dest="control", action="store_false",
                                  help="Explicitly register metadata without Discord delivery")
            sub.add_argument("--codex-socket", help="Optional absolute path to the existing Codex daemon control socket")
            sub.add_argument("--mode", choices=["solo", "swarm"])
        if command == "disconnect":
            sub.add_argument("--session", required=True)
    args = parser.parse_args()
    try:
        result = asyncio.run(execute(args, parser))
    except (OSError, TimeoutError):
        result = {"status": "error", "error": "service_unavailable",
                  "hint": "Start 'swarm serve' with the same --home in another shell."}
    except (ValueError, RuntimeError) as error:
        result = {"status": "error", "error": str(error)}
    except KeyboardInterrupt:
        return
    if result is not None:
        print(json.dumps(result, indent=2))
        if result.get("status") in {"error", "pairing_required", "pending", "rejected"}:
            sys.exit(1)


if __name__ == "__main__":
    main()
