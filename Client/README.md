# Swarm connector

One local service connects multiple harness sessions to the backend through one authenticated WebSocket per backend origin. Discord `/agent` routes prompts through that connection to existing Codex daemon sessions and returns their results to the originating channel.

The connector runs on Linux/macOS. Session-bound MCP tools manage project work and documents, read Discord history, and post updates through the same backend WebSocket. Other harness control adapters, Windows support, and automatic conversation-close detection are not implemented.

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

A fully usable native connection returns `status: registered`, `delivery.ready: true`, and `mcp.ready: true`. Reconnecting the same native thread/workspace/project reuses its Swarm session ID. `connect` saves backend, project, and mode preferences under the workspace's `.swarm/config.json`.

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

`/agent` creates a public receipt containing the full prompt and selected agent before delivery. Results reply to that receipt and include an “In response to” excerpt, so the conversation stays understandable even if the receipt is later deleted. The receipt ID is also included in the native prompt for explicit MCP replies.

`queued` means the local connector accepted the request, not that Codex has started. Requests wait for current work and native queued input to finish. Completion, failure, interruption, or uncertain delivery is posted to the originating channel with the request UUID. Injected requests share the active turn's result and receive output completed after injection. When the native CLI consumes a queued prompt before Swarm starts it, the connector adopts the turn using the exact request/client ID instead of reporting a false delivery failure. Genuine uncertainty still requires checking local Codex before resending. During a rolling backend upgrade, successful responses without a prompt receipt carry a quoted prompt excerpt in the message body.

Final agent text is relayed automatically; messages explicitly marked `commentary` stay local. Older daemon messages without phase metadata retain the previous relay behavior. Approvals stay in the local Codex UI. Output is bounded to 16,000 characters, may shrink further to fit the wire limit, and is split into Discord-sized messages with mentions disabled. Model catalogs describe capabilities; they do not guarantee account entitlement.

## Give the harness Discord MCP tools

Native Codex setup is automatic in `swarm connect`. After pairing, the connector:

1. Verifies and registers the existing native conversation.
2. Adds its `swarm` stdio server to Codex configuration through the daemon's config API, preserving other servers and settings.
3. Calls `config/mcpServer/reload` to refresh the loaded conversation without restarting the harness.
4. Checks the thread's live tool inventory and calls `swarm_status` through that MCP connection to verify the correct attachment and backend.

A successful result includes `mcp.ready: true`. No extra pairing code, user config edit, or harness restart is needed. This requires the native daemon's config, live MCP reload, status, and tool-call APIs. If any stage fails, `connect` reports an explicit MCP error and exits nonzero; prompt delivery can remain registered, but MCP is not reported ready. An unrelated server already named `swarm` is preserved and reported as `mcp_config_conflict`.

The automatic server uses `swarm mcp --auto`: Codex supplies `_meta.threadId` on each tool call. The local connector selects exactly one matching native attachment across its backends. Missing identity, multiple matching registrations, and disconnected attachments fail explicitly; the server never guesses from the workspace or last connected agent. This keeps multiple conversations in one workspace separate. Disconnecting a registration immediately removes its chat and workspace tool access; the shared MCP configuration can stay installed for later connections.

For metadata-only attachments or another harness, **manual stdio setup** is still available with an explicit binding. Replace the paths, backend, and Swarm session ID returned by `connect`:

```json
{
  "mcpServers": {
    "swarm": {
      "command": "/absolute/path/to/Swarm/Client/.venv/bin/swarm",
      "args": [
        "mcp", "--backend", "http://localhost:5142",
        "--workspace", "/absolute/project",
        "--session", "<swarm-session-uuid>"
      ]
    }
  }
}
```

For manual setup, the surrounding config format depends on the harness; `command` and `args` above are the stdio process settings. `swarm serve` must already be running. A custom home goes before the subcommand: `swarm --home /path mcp …`. This process neither starts another harness nor opens another backend connection. Each tool call follows **harness → stdio MCP → private connector socket → existing authenticated backend WebSocket → backend service**, with replies correlated over the same path. Chat operations use Discord; workspace operations use SQLite.

Instead of `--session`, use `--harness-session-id <native-thread-id>` plus `--workspace`, or let MCP inherit `CODEX_THREAD_ID` from native Codex. This resolves exactly one local attachment for that backend; missing or ambiguous attachments fail. Explicit session bindings must be refreshed after a connector restart and reattachment. Backend and workspace can use the preferences saved by `connect`. Metadata-only attachments can use MCP too; receiving Discord prompts still requires a supported control adapter.

| Tool | Use |
| --- | --- |
| `swarm_status()` | Verify the calling attachment and backend connection without posting to Discord. |
| `discord_get_channel(channel_id)` | Inspect the destination, including whether it is a thread and its parent channel. |
| `discord_read_messages(channel_id, limit=20, before_message_id=None)` | Read up to 50 messages, oldest first, with author IDs, timestamps, reply IDs, and attachment links. Use `nextBeforeMessageId` to read older history. |
| `discord_send_message(channel_id, content, reply_to_message_id=None)` | Post up to 1,800 characters to that channel or thread, optionally replying to a message in it. Returns the posted message URL. |

A Discord thread has its own channel ID: pass that thread ID to post inside it. Incoming `/agent` prompts now carry the originating channel/thread ID and Swarm session ID. For another destination, supply its Discord ID (Copy Channel ID with Discord Developer Mode). Channels and threads already exist; these tools do not create them or subscribe to live message events. Read history again when more context is needed.

The backend derives attribution from the authenticated session, disables mentions, and checks the paired owner's current guild/channel permissions on every call. Private threads also require membership or thread-management access. The bot must have its own View Channel, Read Message History, Send Messages, Send Messages in Threads, and Embed Links permissions as appropriate. DMs and posting to archived/locked threads are unsupported. Project labels do not restrict chat access: MCP can address guild channels available to both the paired owner and bot. In Swarm mode, people who can prompt that session can influence its use of these tools.

Both explicit MCP posts and automatic results have a Discord embed footer beneath the message body. It identifies the agent display name, paired Discord owner (name and user ID), harness, project, machine (name and ID), Swarm session UUID, and destination channel/thread (name and ID). Automatic results also include the request UUID, status, and part number when split across messages. These fields come from backend session metadata, not agent-supplied text. Request attribution is captured at submission and survives session renames or disconnection; unavailable Discord name lookups fall back to IDs. Chat history exposes the embed footer as `footer` so agents can distinguish messages from different sessions using the same bot.

For ordinary message text, enable **Message Content Intent** under the application's **Bot → Privileged Gateway Intents** settings in the Discord Developer Portal (and obtain approval if Discord requires it). History tools use REST: Discord's application-level access controls whether REST returns human message content, independently of the intents requested by the Gateway connection. Without that access, human content and attachment fields can be empty while authors, timestamps, and the bot's own message text remain visible. After saving the portal setting, retry the history read; no MCP or harness restart is needed. The separate backend flag `Discord__ReadMessageContent=true` requests message content for Gateway events on backend startup; it does not grant REST access. See [Discord's HTTP and message content restrictions](https://docs.discord.com/developers/events/gateway#http-restrictions).

Keep detailed implementation, logs, and approvals inside the harness. Use MCP for chosen shared progress, questions, and results; posting a question does not itself wait for a reply or resume work. Further instructions can arrive through `/agent`. Automatic final-answer relay remains enabled, so avoid repeating a full MCP update in the final answer. Sends are never replayed after reconnect or retried by Swarm. If a send returns `chat_delivery_unknown_check_discord`, inspect history before considering another send.

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
| `mcp_live_reload_unavailable` / `mcp_config_unavailable` | Native daemon lacks the required live setup API; MCP readiness remains false. Diagnose/update that adapter instead of claiming it is connected. |
| `mcp_config_conflict` | Another `swarm` MCP entry exists; the connector preserves it. Inspect that entry before deciding how to resolve the conflict. |
| `pairing_required` | Pair again if the credential was revoked, replaced, or the backend database was reset. Credentials persist across ordinary backend restarts. |
| `codex_session_not_loaded_on_daemon` | Connect the actual live conversation on the correct daemon; do not resume its history in another process. |
| `codex_unavailable_check_daemon_socket` | Check the daemon and its socket path. The control socket requires a WebSocket handshake. |
| Updated code seems unchanged | Check the running version. Restart the connector and reattach its sessions when no active work will be disturbed; editing files does not reload Python. |
| `unknown` delivery or connection lost | Check local Codex before resending. Accepted work may still be running. |

Reconnect restores live registrations after transport loss, with backoff up to approximately 31 seconds. Prompts and results are never replayed automatically. A disconnect cancels requests still waiting locally; work already accepted by Codex can continue. Connector restarts discard local attachments and waiting requests but retain credentials. Live request correlation lasts up to 24 hours and is lost on restart; persisted request/event history remains in SQLite and is not replayed.

## Manage initiatives, issues/features and documents

The same MCP connection exposes fifteen additional tools:

| Resource | Tools |
| --- | --- |
| Initiatives (epics) | `initiative_create`, `initiative_get`, `initiative_update`, `initiative_delete`, `initiative_list` |
| Issues and features | `work_item_create`, `work_item_get`, `work_item_update`, `work_item_delete`, `work_item_list` |
| Documents | `document_create`, `document_get`, `document_update`, `document_delete`, `document_list` |

For example, create an initiative, then call `work_item_create(title="Implement search", type="feature", initiative_id=<returned ID>)`. List its open children with `work_item_list(initiative_id=<ID>, status="open")`, or only open features with `type="feature"`. Issues/features are peer child items, not parents of one another. Standalone work items are supported too.

All list tools accept `query` for literal title search, `limit` (1–20) and `after_id` (pass the returned `nextAfterId`). Results contain `items`, `total` and `nextAfterId`. They omit bodies; use the get tools for full briefs or document Markdown. Reads return `item`, including `version`. Updates/deletes require that number as `expected_version`. On a version conflict, reread before reconciling. Document updates append revisions; deletes preserve history. An initiative cannot be deleted while it has active children. Use `work_item_update` to move or unlink them (`clear_initiative=true`) first.

These tools always use the attached session's project and paired owner. They cannot accept a project/session/author override. The backend operator must [provision the project key and active server members](../Backend/docs/workspace.md#provision-a-project-before-using-its-tools); a matching local label alone grants no access. This database membership check is separate from the existing live Discord channel permission checks.

Workspace writes do not post to Discord. `workspace_delivery_unknown_read_before_retry` means a write may have committed before its response was lost: inspect the current list/item before considering another write. No mutation is retried automatically. Completed work goes to verification; close it only after the user agrees.

After updating the connector to 0.5.0, restart the shared service when it will not disrupt active work, reattach sessions, and let native connect reload/verify the expanded MCP inventory. No new pairing is required for an ordinary backend restart using the same database.

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

The tests exercise the real ASP.NET controllers, shared connector, and stdio MCP process using a Unix-socket WebSocket Codex fixture, without Discord credentials or inference calls. They cover ownership, multiplexing, queue/inject races, duplicate requests, reconnect, cancellation, correlated results, chat history, channel/thread replies, and uncertain send handling. Discord REST checks use a fake HTTP handler to verify permission denials, private-thread membership, reply references, mentions, and history ordering. Test-only Discord endpoints are absent from production.

Live transport checks used Codex daemon 0.167.0. Discord-to-Codex prompting was also confirmed manually. Real account access and local approval interaction still depend on the user's Codex configuration.
