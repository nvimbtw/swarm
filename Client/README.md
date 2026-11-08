# Swarm connector

One local service connects multiple harness sessions to the backend through one authenticated WebSocket per backend origin. Discord `/agent` routes prompts through that connection to existing Codex daemon sessions and returns their results to the originating channel.

The connector runs on Linux/macOS. MCP, other harness adapters, Windows support, and automatic conversation-close detection are not implemented.

## Install and pair

From the repository root:

```bash
python3 -m venv Client/.venv
Client/.venv/bin/python -m pip install -e Client
Client/.venv/bin/swarm serve
```

Keep the service running. Start the backend separately with `dotnet run` from `Backend/Swarm.Server`; it reads `DISCORD_TOKEN` from the environment or the nearest `.env`. Invite the Discord application with `bot` and `applications.commands` scopes, and allow View Channels, Send Messages, and application commands in the target channel.

In another shell:

```bash
Client/.venv/bin/swarm pair-start --backend http://localhost:5142
```

Run the returned `/swarm-pair pairing-id:…` command in Discord, then enter its private eight-digit code locally:

```bash
Client/.venv/bin/swarm pair-complete --backend http://localhost:5142
```

Pairing expires after five minutes and allows five incorrect attempts. Remote backend origins require HTTPS; HTTP is accepted only for loopback addresses.

## Connect an existing Codex conversation

Register the [connection skill](../skills/swarm-connect/SKILL.md) once for this OS user:

```bash
mkdir -p ~/.agents/skills
ln -s /absolute/path/to/Swarm/skills/swarm-connect ~/.agents/skills/swarm-connect
```

Check an existing destination before creating the symlink. In native Codex CLI, invoke `$swarm-connect` or select it under `/skills`. The link keeps the skill current with this checkout. See [Codex skill discovery](https://learn.chatgpt.com/docs/build-skills#where-codex-loads-local-skills).

The manual equivalent is:

```bash
swarm connect --backend http://localhost:5142 --project swarm \
  --name backend-codex --harness codex --workspace /absolute/project \
  --harness-session-id <actual-codex-thread-uuid> --mode solo
```

Use the installed `Client/.venv/bin/swarm` path if `swarm` is not on `PATH`. When invoked inside the existing conversation, `connect` can use its `CODEX_THREAD_ID` environment variable instead of the explicit ID. Never infer a thread ID from a directory or display name.

Codex control is enabled by default. The connector verifies that the thread is already live on the selected daemon, accepts direct input, matches the workspace, and supports the native queue API. It does not launch or transfer a conversation. An unavailable control interface returns an error; metadata-only registration requires explicit `--registration-only`. Other harness types default to metadata.

The daemon must expose its Unix WebSocket control socket and `thread/queue/*` and `thread/settings/update` APIs. The default socket is `$CODEX_HOME/app-server-control/app-server-control.sock` (`CODEX_HOME` defaults to `~/.codex`); use `--codex-socket /absolute/control.sock` for a custom daemon. T3-managed conversations require a separate adapter.

A usable connection returns both `status: registered` and `delivery.ready: true`. Reconnecting the same native thread/workspace/project reuses its Swarm session ID. `connect` saves backend, project, and mode preferences under the workspace's `.swarm/config.json`.

## Use from Discord

```text
/agent session:<select session> prompt:Run the tests
/agent session:<select session> prompt:Investigate the failure model:<model> reasoning:<level>
/agent session:<select session> prompt:Focus on login delivery:Inject
```

| Option | Behavior |
| --- | --- |
| `session` | Shows controllable sessions the caller may use, labelled by name, harness, machine, and project. |
| `prompt` | Required; up to 4,000 characters. |
| `model`, `reasoning` | Optional; choices come from that daemon. Explicit changes persist for subsequent Codex turns. |
| `delivery` | Defaults to Queue. Inject steers the current active turn and rejects model/reasoning changes. |

Solo permits only the paired Discord owner. Swarm permits any Discord user able to invoke the bot; there is no project-membership or guild scoping yet. Choose Swarm only when that sharing is intended.

`queued` means the local connector accepted the request, not that Codex has started. Requests wait for current work and native queued input to finish. Completion, failure, interruption, or uncertain delivery is posted to the originating channel with the request UUID. Injected requests share the active turn's result and receive output completed after injection.

Only agent message text is relayed. Approvals and questions stay in the local Codex UI. Output is bounded to 16,000 characters, may shrink further to fit the wire limit, and is split into Discord-sized messages with mentions disabled. Model catalogs describe capabilities; they do not guarantee account entitlement.

## Operate and troubleshoot

```bash
swarm status
swarm sessions --backend http://localhost:5142
swarm disconnect --backend http://localhost:5142 --session <swarm-session-id>
```

`status` reads local state and the running connector version; `sessions` queries the backend for this owner's registrations. Disconnect the intended registration explicitly. Stop the shared service only when all its sessions should go offline.

| Symptom | Check or action |
| --- | --- |
| Empty Discord session picker | Check `registration.control` and `delivery.ready`. Metadata-only sessions are hidden. Solo also requires the paired Discord account. |
| `pairing_required` | Pair again. Backend credentials are held in memory and are lost on backend restart. |
| `codex_session_not_loaded_on_daemon` | Connect the actual live conversation on the correct daemon; do not resume its history in another process. |
| `codex_unavailable_check_daemon_socket` | Check the daemon and its socket path. The control socket requires a WebSocket handshake. |
| Updated code seems unchanged | Check the running version. Restart the connector and reattach its sessions when no active work will be disturbed; editing files does not reload Python. |
| `unknown` delivery or connection lost | Check local Codex before resending. Accepted work may still be running. |

Reconnect restores live registrations after transport loss, with backoff up to approximately 31 seconds. Prompts and results are never replayed automatically. A disconnect cancels requests still waiting locally; work already accepted by Codex can continue. Connector restarts discard local attachments and waiting requests but retain credentials. Backend request tracking lasts up to 24 hours and is lost on restart.

## Local state

| Location | Contents |
| --- | --- |
| `~/.swarm/config.json` | Machine UUID and display name. |
| `~/.swarm/credentials.json` | Private bearer credentials, keyed by backend origin. Never copy them between origins. |
| `~/.swarm/connector.sock`, `connector.lock` | User-only CLI transport and single-instance lock. |
| `<workspace>/.swarm/config.json` | Project preferences; no credentials or live thread IDs. Add `.swarm/` to the project's ignore rules. |

`--home /path` or `SWARM_HOME` isolates an installation; use the same value for the service and its CLI commands. Workspace paths, native thread IDs, and daemon socket paths stay local. The backend receives session metadata and delivery capability.

## Development

- [Protocol and delivery rules](docs/protocol.md): wire messages, trust boundaries, queue ownership, and limits.
- [Skill command reference](../skills/swarm-connect/references/commands.md): arguments and result states for agent callers.

```bash
dotnet run --project Backend/Swarm.Server.Tests
Client/.venv/bin/python -m unittest discover -s Client/tests -v
```

The tests exercise the real ASP.NET controllers and shared connector using a Unix-socket WebSocket Codex fixture, without Discord credentials or inference calls. They cover ownership, multiplexing, queue/inject races, duplicate requests, reconnect, cancellation, and correlated results. The fixture's simulated Discord approval endpoint is absent from production.

Live transport checks used Codex daemon 0.167.0. Discord-to-Codex prompting was also confirmed manually. Real account access and local approval interaction still depend on the user's Codex configuration.
