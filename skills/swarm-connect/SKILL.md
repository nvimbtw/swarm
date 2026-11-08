---
name: swarm-connect
description: Connect the current coding session to Swarm, complete Discord pairing, and inspect or disconnect its registration.
---

Use the installed `swarm` CLI to attach this existing conversation to the local connector. Read [the command reference](references/commands.md) for setup, command arguments, and result states.

1. Inspect `<workspace>/.swarm/config.json` when present and run `swarm status`. Reuse the shared local service. If it is unavailable, start `swarm serve` using an available persistent process mechanism, or explain that it needs a separate shell. Do not launch another harness conversation.
2. Resolve the backend origin, project, and display name from the user's request and saved settings. Ask only for missing information. A different backend origin requires its own pairing; do not copy credentials between origins.
3. Run `swarm connect` with the absolute workspace path and harness type. Supply a reliable current harness session ID when available; otherwise omit it and retain the returned Swarm session ID. For a native Codex CLI conversation on its shared daemon, include `--control` and the actual Codex thread UUID to enable Discord delivery. The connector verifies the thread/workspace and queue API. Do not use `--control` for T3 Code, an IDE session, or an unidentified conversation, and do not launch or transfer a conversation to make it controllable. Multiple conversations in one workspace are separate sessions. Default to Solo unless the user requests Swarm mode.
4. If the result is `pairing_required`, run `swarm pair-start` and show its exact Discord command and machine name from `swarm status`. Wait for the user to provide the short code, then send it to `swarm pair-complete --code-stdin` via stdin. Retry connection after successful pairing. If rejected, explain that the code may be incorrect, expired, or exhausted; offer a fresh pairing rather than repeatedly guessing codes.
5. Report a connection as confirmed only when the session state is `registered`. Include its display name, Swarm session ID, backend, project, and mode. Report `pending`, `rejected`, and backend failures accurately.

Permanent credentials are owned by the connector in `~/.swarm`; do not read, print, or place them in project settings. Project settings and display names are preferences, not authorization. Project labels are metadata; project membership enforcement is not implemented. Solo permits only the paired Discord owner. Swarm permits any Discord user who can invoke the bot; it is not restricted to a project or guild. Default to Solo and enable Swarm only when requested.

For disconnect, use the session ID recorded for this conversation. If it is unavailable, inspect status and ask which registration to disconnect when ambiguous. Do not stop the shared service to disconnect one session.

A registered native Codex CLI session with `control: true` supports Discord `/agent` prompts through the shared backend WebSocket: queue by default, explicit injection into an active turn, optional model/reasoning for queued turns, and results in the originating Discord channel. Model/reasoning changes persist as Codex session settings. Local approvals/questions remain in the native CLI. A registration without control is metadata only; do not claim it can receive prompts. MCP tools, other harness adapters, and automatic conversation-close detection are not implemented. After uncertain delivery, check the local conversation before resending; requests are not automatically replayed.
