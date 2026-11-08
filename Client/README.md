# Local Swarm connector

One Python service per OS user serves multiple coding sessions and workspaces. Each authenticated backend origin has one outbound WebSocket carrying separate session registrations. The initial local transport is a user-only Unix socket (Linux/macOS). Native Codex CLI sessions can receive Discord prompts and return results through that same connection. Windows support, MCP tools, and automatic harness lifecycle detection are not implemented.

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

## Discord → native Codex CLI

Use a Codex CLI version exposing `app-server proxy`, `thread/queue/*`, and `thread/settings/update` (the implementation was checked against the 0.166.0 protocol schema). The target conversation must live on the existing local app-server daemon. Swarm attaches to that daemon; it does not start a second conversation or resume the same history in a separate Codex process. IDE extensions and T3-managed Codex sessions need separate adapters.

Pair as above, then connect the **actual existing Codex thread UUID**, from the current native CLI session:

```bash
swarm connect --backend http://localhost:5142 --project swarm \
  --name backend-codex --harness codex --workspace /absolute/project \
  --harness-session-id <actual-codex-thread-uuid> --control --mode solo
```

`--control` verifies that the daemon can read the thread, that its workspace matches, and that its queue API is available before advertising Discord delivery. A custom daemon can be selected with `--codex-socket /absolute/control.sock`; this stays local. Without `--control`, connection still registers metadata, preserving support for other harnesses. Reconnect an existing metadata registration with `--control` to enable delivery. Do not use a T3 thread ID or infer a Codex thread from a workspace/name.

In Discord:

```text
/agent session:<select connected session> prompt:Run the tests
/agent session:<select connected session> prompt:Investigate the failure model:<model> reasoning:<level>
/agent session:<select connected session> prompt:Focus on the login failure delivery:Inject
```

Session autocomplete includes the name, harness, machine, and project and only shows controllable sessions the caller may use. Model and reasoning autocomplete query the selected machine through the shared backend WebSocket. Values are checked again locally. These are the daemon's advertised capabilities, not a guarantee of account entitlement; inference can still fail. Prompts are limited to 4,000 characters. Omitted settings inherit the session's configuration. Explicit model/reasoning changes take effect when the queued request starts and remain the session settings for subsequent turns, matching Codex's API semantics.

Queue is the default. Requests wait in a bounded FIFO (20 waiting requests per native thread) on the connector until current work and native queued input finish. Swarm then uses Codex's persistent queue plus its atomic start-if-idle operation; a race with local input cannot turn a queued prompt into steering. `Inject` requires an active turn and uses its expected turn ID; it never interrupts work or silently starts another turn. Injection rejects model/reasoning overrides.

The command privately acknowledges `queued` or `injected` with a request UUID. Queue acknowledgment means the local connector accepted it, not that Codex has started. Completion, failure, interruption, or uncertain delivery is posted in the originating Discord channel. Only agent message text from the relevant turn is returned; no transcript, reasoning, tool arguments, or approval payloads are relayed. Injected requests receive output completed after injection and share the active turn's result. Keep the local CLI open for approvals and questions; Discord approval buttons and interactive question forwarding are outside this version. Output is bounded to 16,000 characters and split into Discord-sized messages; oversized escaped WebSocket output is truncated further. Mentions are disabled.

Queued requests are held in memory until submitted to Codex; connector restarts discard them. A disconnect cancels work still waiting locally. Work already accepted by Codex can continue, and native queue entries remain in Codex. After an ambiguous acknowledgment or connection loss, check local Codex before resending: Swarm never automatically replays prompts or cancels an active turn. Results from a disconnected transport are not replayed on reconnect. Backend request tracking expires after 24 hours and resets on restart.

## State and authentication

- `~/.swarm/config.json`: machine UUID and display name.
- `~/.swarm/credentials.json`: backend-origin-specific bearer credentials, owner-readable/writable only. Credentials do not appear in CLI output.
- `~/.swarm/connector.sock` and `connector.lock`: private local communication and single-instance ownership.
- `<workspace>/.swarm/config.json`: backend origin, project label, session-name prefix, mode; no credentials or live session IDs. Ignored by this repository's Git configuration. Add `.swarm/` to other projects' ignore rules too.

Project settings store an explicit backend origin in this first version instead of named profiles. Credentials are always looked up by that origin; changing a project URL does not transfer credentials. `--home` or `SWARM_HOME` isolates a connector installation for testing.

Pairing requires a private connector secret plus an eight-digit code issued to the Discord interaction's authenticated user. Challenges expire after five minutes, are single-use, and allow five incorrect code attempts. The backend hashes credentials in memory. Pairing HTTP endpoints are rate-limited per source IP. Re-pairing the same installation/account replaces its previous credential. Backend restarts invalidate all credentials until database support is added; the connector reports `pairing_required` when a reconnect receives HTTP 401.

HTTP is accepted only for loopback backend addresses. A Raspberry Pi accessed over the network needs an HTTPS endpoint with a trusted certificate; the connector uses the corresponding secure WebSocket automatically. Redirects on pairing requests are refused. Workspace paths and harness thread identifiers remain local; only session ID, name, project label, harness type, and mode are registered remotely.

Discord delivery enforces session mode: `solo` accepts only the paired Discord owner; `swarm` accepts any Discord user able to invoke the bot. There is no project-membership or guild scoping yet, so Swarm mode is an explicit opt-in to sharing across the bot's users. Agent-to-agent MCP access is not implemented. Metadata-only registrations cannot receive prompts. Sessions remain attached until explicitly disconnected or the service stops.

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
    "mode": "solo",
    "control": true
  }
}
```

Supported requests: `session.register`, `session.unregister` (`sessionId` payload), `session.list`, and `ping`. Replies have the same correlation ID and types `session.registered`, `session.unregistered`, `session.listed`, `pong`, or `error`. Lists are owner-scoped, and unregistering requires the owning connection. The same authenticated installation can rebind its session IDs on reconnect, even before the old socket finishes cleanup. Identity is derived from the bearer credential, not request fields. Messages are limited to 64 KiB of JSON text, including across fragments. There is a 100-session cap per installation identity. Disconnection removes that connection's registrations.

The connector retries transport failures with jittered exponential backoff (up to approximately 31 seconds). It re-registers live local attachments, not previous process state. Registrations are restored, but work requests and results are never replayed.

### Addressed harness messages on the same socket

There is no extra backend-to-harness connection. The backend multiplexes control messages with registration and heartbeat messages:

```json
{
  "version": 1,
  "id": "request-uuid",
  "type": "agent.submit",
  "payload": {
    "requestId": "request-uuid",
    "sessionId": "swarm-session-uuid",
    "senderId": "verified-discord-user-id",
    "channelId": "discord-channel-id",
    "delivery": "queue",
    "prompt": "Run the tests",
    "model": null,
    "reasoning": null
  }
}
```

`agent.describe` uses the same envelope and returns model/reasoning options without submitting input. The connector answers with `agent.reply`, retaining the envelope's correlation ID and `payload.requestId`/`sessionId`. Submit replies are `queued`, `injected`, `failed`, or `unknown`; descriptions return `available` plus `models`, `model`, and `reasoning`. Subsequent `agent.event` messages have their own envelope ID and the original request/session IDs in the payload, with status `started`, `completed`, `failed`, `interrupted`, or `unknown`, and optional `turnId`, `output`, and `error`.

Sender identity comes exclusively from the authenticated Discord interaction. The backend authorizes again at submission, binds every reply/event to the selected session and authenticated installation socket, and retains the original Discord destination itself. Client-supplied destinations are never trusted for result publication. A swapped credential is checked before dispatch even on otherwise idle sockets. The local connector independently checks owner/mode and attachment before delivery. Concurrent sends are serialized on each transport; incoming harness operations run separately so waiting for Codex never blocks registration/heartbeat replies. The router caps outstanding requests at 100 per connection and 1,000 globally; duplicate request IDs on a live connection reuse the original acknowledgment instead of resubmitting input.

## Verification

```bash
dotnet run --project Backend/Swarm.Server.Tests
Client/.venv/bin/python -m unittest discover -s Client/tests -v
```

The C# executable checks pairing expiry, single-use codes, attempt limits, identity binding, credential replacement, and registration ownership. With `--serve` it is a separate loopback-only fixture host used by Python integration tests; its simulated Discord approval endpoint is absent from the production backend. The Python tests exercise actual ASP.NET HTTP/WebSocket controllers, the local service, and a deterministic fake Codex daemon proxy without a Discord token or inference API call. They cover mode permissions, two controllable sessions sharing one socket, queue/inject behavior, correlated results, disconnect cancellation, literal prompt preservation, unsupported options, and atomic-start races. A live native Codex and Discord smoke test is still needed to verify real inference, local approval routing, bot invitation, autocomplete, and result publication in the target server.
