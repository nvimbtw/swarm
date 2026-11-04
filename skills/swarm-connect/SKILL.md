---
name: swarm-connect
description: Connect the current coding session to Swarm, complete Discord pairing, and inspect or disconnect its registration.
---

Use the installed `swarm` CLI to attach this existing conversation to the local connector. Read [the command reference](references/commands.md) for setup, command arguments, and result states.

1. Inspect `<workspace>/.swarm/config.json` when present and run `swarm status`. Reuse the shared local service. If it is unavailable, start `swarm serve` using an available persistent process mechanism, or explain that it needs a separate shell. Do not launch another harness conversation.
2. Resolve the backend origin, project, and display name from the user's request and saved settings. Ask only for missing information. A different backend origin requires its own pairing; do not copy credentials between origins.
3. Run `swarm connect` with the absolute workspace path and harness type. Supply a reliable current harness session ID when available; otherwise omit it and retain the returned Swarm session ID. Multiple conversations in one workspace are separate sessions. Default to Solo unless the user requests Swarm mode.
4. If the result is `pairing_required`, run `swarm pair-start` and show its exact Discord command and machine name from `swarm status`. Wait for the user to provide the short code, then send it to `swarm pair-complete --code-stdin` via stdin. Retry connection after successful pairing. If rejected, explain that the code may be incorrect, expired, or exhausted; offer a fresh pairing rather than repeatedly guessing codes.
5. Report a connection as confirmed only when the session state is `registered`. Include its display name, Swarm session ID, backend, project, and mode. Report `pending`, `rejected`, and backend failures accurately.

Permanent credentials are owned by the connector in `~/.swarm`; do not read, print, or place them in project settings. Project settings and display names are preferences, not authorization. Project labels and modes are metadata in this prototype; project permission enforcement is not implemented yet.

For disconnect, use the session ID recorded for this conversation. If it is unavailable, inspect status and ask which registration to disconnect when ambiguous. Do not stop the shared service to disconnect one session.

This version registers sessions only. It does not deliver prompts, wake an idle harness, expose MCP tools, or detect when a conversation closes. Do not claim these capabilities.
