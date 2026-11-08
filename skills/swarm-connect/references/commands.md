# Connector command reference

Commands return JSON; inspect it even when the exit code is nonzero. `serve` runs until stopped. Invoke `swarm --home /path …` or set `SWARM_HOME` consistently to isolate an installation; the default is `~/.swarm`.

If `swarm` is unavailable, resolve this skill's source path to the Swarm checkout. From that checkout, create `Client/.venv` with `python3 -m venv Client/.venv`, install with `Client/.venv/bin/python -m pip install -e Client`, and use `Client/.venv/bin/swarm`. Do not assume the user's current workspace is the Swarm repository.

| Command | Arguments and result |
| --- | --- |
| `serve` | Starts the shared local service. It must outlive the CLI call; use a persistent process mechanism or separate shell. |
| `status` | Returns running version, machine identity, backend links, and local attachments. No backend argument. |
| `pair-start` | `--backend <origin>`. Returns the exact `/swarm-pair` command to run in Discord. |
| `pair-complete` | `--backend <origin> --code-stdin`. Feed the eight-digit Discord code through stdin, never a command-line argument. |
| `connect` | `--harness codex --workspace <absolute-path> --backend <origin> --project <label>`, with optional `--name`, `--mode solo\|swarm`, `--harness-session-id`, and `--codex-socket`. |
| `sessions` | `--backend <origin>`. Queries backend registrations owned by the paired Discord account. |
| `disconnect` | `--backend <origin> --session <swarm-session-id>`. Removes only that attachment. |

`connect` saves backend, project, and mode preferences in the workspace's `.swarm/config.json`. Later commands can reuse those settings; use `--workspace` when operating elsewhere. A project is currently a label, not an authorization boundary.

Codex defaults to verified Discord control (`--control` is an explicit alias). Use the current native thread UUID; the CLI reads `CODEX_THREAD_ID` when no explicit ID is supplied. Reconnecting the same native thread/workspace/project reuses its Swarm ID. Never infer an ID from names, use a T3 thread ID, or launch/transfer a conversation to make it controllable.

A control failure must be diagnosed, not silently downgraded. Explicit `--registration-only` is for metadata without Discord delivery and is the default for other harness types. Without a native ID, each metadata attachment is separate. A custom daemon uses `--codex-socket /absolute/control.sock`; otherwise the connector uses the Codex home directory's `app-server-control/app-server-control.sock`.

| State | Interpretation |
| --- | --- |
| `registered` + `delivery.ready: true` | Backend acknowledgment and verified Discord control. |
| `registered` + `delivery.state: registration_only` | Metadata only; hidden from `/agent`. |
| `pending` | Waiting for backend registration. Do not report it as connected. |
| `pairing_required` | Discord verification is needed. Pairing expires after five minutes with five incorrect attempts allowed. |
| `rejected` | Backend refused registration. |
| `authenticated` | Credential issuance succeeded; no session registration is implied. |

Solo permits only the paired owner. Swarm permits other Discord users able to invoke the bot, without project/guild scoping. `/agent` queues by default; explicit Inject requires an active turn and rejects model/reasoning changes. Explicit settings persist in Codex, results return to the originating Discord channel, and approvals stay local.

Service restarts lose attachments/waiting requests but retain credentials; backend restarts invalidate credentials. Updating files does not reload the service: check its running version, restart only when active work will not be disturbed, then reattach. Disconnect explicitly when finished; conversation-close detection is not implemented. After uncertain delivery, check local Codex before resending.

See [connector setup and troubleshooting](../../../Client/README.md) and [protocol details](../../../Client/docs/protocol.md).
