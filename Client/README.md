# Local Swarm connector

One Python service per OS user serves multiple coding sessions and workspaces. Each authenticated backend origin has one outbound WebSocket carrying separate session registrations. The initial local transport is a user-only Unix socket (Linux/macOS); Windows support, MCP tools, prompt injection, and automatic harness lifecycle detection are not implemented.

## Setup and first connection

From the repository root:

```bash
python3 -m venv Client/.venv
Client/.venv/bin/python -m pip install -e Client
Client/.venv/bin/swarm serve
```

Leave that shell running. Start the backend separately from `Backend/Swarm.Server` using `dotnet run`; it needs `DISCORD_TOKEN` in the environment or the existing `.env` setup. Invite the Discord application with the `bot` and `applications.commands` scopes. View Channels and Send Messages are sufficient for the current slash-command test; channel and application-command permissions must allow the user to invoke commands.

In another shell:

```bash
Client/.venv/bin/swarm pair-start --backend http://localhost:5142
```

Run the returned `/swarm-pair pairing-id:...` command in Discord. The bot privately returns an eight-digit code. Complete pairing locally (the command prompts for the code):

```bash
Client/.venv/bin/swarm pair-complete --backend http://localhost:5142
Client/.venv/bin/swarm connect --backend http://localhost:5142 --project swarm --name backend-codex --harness codex --workspace "$PWD"
Client/.venv/bin/swarm status
Client/.venv/bin/swarm sessions
```

`connect` stores preferences in the workspace's `.swarm/config.json`. For a second harness, connect with another name and its workspace. A real `--harness-session-id`, when available, makes reconnecting that same harness idempotent. Without it, each invocation creates a new registration; retain the returned ID to disconnect it later.

For the full CLI contract and states, see [the skill command reference](../skills/swarm-connect/references/commands.md). Use `swarm disconnect --session <id>` to remove one registration. Stop the service with Ctrl+C only when all attached sessions should go offline. Service restarts lose attached sessions, but keep credentials.

## State and authentication

- `~/.swarm/config.json`: machine UUID and display name.
- `~/.swarm/credentials.json`: backend-origin-specific bearer credentials, owner-readable/writable only. Credentials do not appear in CLI output.
- `~/.swarm/connector.sock` and `connector.lock`: private local communication and single-instance ownership.
- `<workspace>/.swarm/config.json`: backend origin, project label, session-name prefix, mode; no credentials or live session IDs. Ignored by this repository's Git configuration. Add `.swarm/` to other projects' ignore rules too.

Project settings store an explicit backend origin in this first version instead of named profiles. Credentials are always looked up by that origin; changing a project URL does not transfer credentials. `--home` or `SWARM_HOME` isolates a connector installation for testing.

Pairing requires a private connector secret plus an eight-digit code issued to the Discord interaction's authenticated user. Challenges expire after five minutes, are single-use, and allow five incorrect code attempts. The backend hashes credentials in memory. Pairing HTTP endpoints are rate-limited per source IP. Re-pairing the same installation/account replaces its previous credential. Backend restarts invalidate all credentials until database support is added; the connector reports `pairing_required` when a reconnect receives HTTP 401.

HTTP is accepted only for loopback backend addresses. A Raspberry Pi accessed over the network needs an HTTPS endpoint with a trusted certificate; the connector uses the corresponding secure WebSocket automatically. Redirects on pairing requests are refused. Workspace paths and harness thread identifiers remain local; only session ID, name, project label, harness type, and mode are registered remotely.

There is no project-membership database or work routing yet. `solo`/`swarm` are declared session modes, not an implemented authorization system. Registration means that a session is attached, not that an idle agent can receive prompts. Sessions remain attached until explicitly disconnected or the service stops.

## Codex skill registration

The canonical source is [skills/swarm-connect/SKILL.md](../skills/swarm-connect/SKILL.md). Register it for this user without copying the source:

```bash
mkdir -p ~/.agents/skills
ln -s /absolute/path/to/Swarm/skills/swarm-connect ~/.agents/skills/swarm-connect
```

Do not replace an existing skill with that name blindly. Codex supports symlinked skill folders; invoke `$swarm-connect`. If discovery has not refreshed, restart Codex. See [official skill discovery guidance](https://learn.chatgpt.com/docs/build-skills#where-codex-loads-local-skills).

## Protocol v1

The existing unauthenticated `/ws` greeting demo remains available. The connector uses `/ws/sessions`, authenticated with `Authorization: Bearer <credential>`. It is included in the existing connection diagnostics.

Pairing uses `POST /api/pairing/start` with `machineId`/`machineName`, followed by `POST /api/pairing/complete` with `pairingId`/`secret`/`code`. Discord approval happens exclusively through `/swarm-pair`, not a production HTTP approval endpoint.

The WebSocket starts with `{"type":"hello","version":1,"ownerId":"..."}`. Requests use a version, correlation ID, type, and payload:

```json
{
  "version": 1,
  "id": "request-uuid",
  "type": "session.register",
  "payload": {
    "sessionId": "session-uuid",
    "name": "backend-codex",
    "projectId": "swarm",
    "harness": "codex",
    "mode": "solo"
  }
}
```

Supported requests: `session.register`, `session.unregister` (`sessionId` payload), `session.list`, and `ping`. Replies have the same correlation ID and types `session.registered`, `session.unregistered`, `session.listed`, `pong`, or `error`. Lists are owner-scoped, and unregistering requires the owning connection. The same authenticated installation can rebind its session IDs on reconnect, even before the old socket finishes cleanup. Identity is derived from the bearer credential, not request fields. Messages are limited to 16 KiB of JSON text, including across fragments. There is a 100-session cap per installation identity. Disconnection removes that connection's registrations.

The connector retries transport failures with jittered exponential backoff (up to approximately 31 seconds). It re-registers live local attachments, not previous process state. No work requests are replayed or delivered in this version.

## Verification

```bash
dotnet run --project Backend/Swarm.Server.Tests
Client/.venv/bin/python -m unittest discover -s Client/tests -v
```

The C# executable checks pairing expiry, single-use codes, attempt limits, identity binding, credential replacement, and registration ownership. With `--serve` it is a separate loopback-only fixture host used by Python integration tests; its simulated Discord approval endpoint is absent from the production backend. The Python tests exercise actual ASP.NET HTTP/WebSocket controllers and the local service without a Discord token. A live Discord test is still needed to verify bot invitation, command registration, and the private response in the target server.
