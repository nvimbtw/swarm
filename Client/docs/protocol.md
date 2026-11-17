# Connector protocol and delivery

The backend owns Discord identity, authorization, and result destinations. The local connector owns workspace paths, native thread IDs, and Codex control. One outbound `/ws/sessions` WebSocket per authenticated backend origin carries all registration, request, and result messages for that installation. Codex control uses a separate local Unix WebSocket; it never connects to the backend itself.

## Code map

| Module | Responsibility |
| --- | --- |
| `cli.py` | CLI arguments, current-thread identity, local RPC, connection readiness. |
| `service.py` | Private Unix RPC server, pairing, attachments, backend/adapter lifetime. |
| `backend.py` | Shared authenticated socket, reconnect, correlation, deduplication, local delivery checks. |
| `protocol.py` | Wire limits, result encoding, delivery failure classification. |
| `codex_rpc.py` | Codex WebSocket handshake, RPC replies, bounded turn-result observation. |
| `codex.py` | Model validation, one queue worker per native thread, atomic queue start, steering. |
| `mcp.py` | Stdio tools bound by per-call native thread metadata or explicit startup binding; forwards calls via private connector RPC. |
| `workspace_tools.py` | Explicit initiative, issue/feature and document tool schemas; shared operation allowlist. |
| `mcp_setup.py` | Native config registration, live MCP refresh, inventory and connection verification. |
| `SessionSocketController` | Authenticated backend transport, frame bounds, serialized sends. |
| `SessionRegistry` | Installation ownership, reconnect rebinding, Solo/Swarm availability. |
| `AgentRouter` | Request admission, acknowledgment waiters, authenticated event routing. |
| `AgentCommand`, `AgentAutocomplete` | Discord interaction and caller-scoped choices. |
| `AgentDiscordPublisher` | Bounded Discord publication outside the socket receive loop. |
| `DiscordChatService`, `DiscordChatClient` | Session/transport binding, Discord permission checks, history and posting. |

## Pairing and registration

Pairing uses `POST /api/pairing/start` (`machineId`, `machineName`) and `POST /api/pairing/complete` (`pairingId`, connector `secret`, Discord `code`). Only the authenticated `/swarm-pair` interaction approves a challenge. The backend stores hashed credentials in SQLite; pairing the same owner/machine replaces its credential.

The connector authenticates `/ws/sessions` with `Authorization: Bearer <credential>`. The server begins with:

```json
{"type":"hello","version":1,"ownerId":"discord-user-id"}
```

Every subsequent envelope contains `version: 1`, `id`, `type`, and, when needed, `payload`. Client requests and their replies share an envelope ID:

| Request | Payload | Reply |
| --- | --- | --- |
| `session.register` | `sessionId`, `name`, `projectId`, `harness`, `mode`, `control` | `session.registered` |
| `session.unregister` | `sessionId` | `session.unregistered`, with `removed` |
| `session.list` | None | `session.listed`, with owner-scoped registrations |
| `ping` | None | `pong` |
| `discord.channel` | `sessionId`, `channelId` | `discord.result` with channel metadata |
| `discord.read` | `sessionId`, `channelId`, optional `limit` (1–50), `beforeMessageId` | `discord.result` with `channel`, chronological `messages`, `nextBeforeMessageId` |
| `discord.send` | `sessionId`, `channelId`, `content` (1–1,800 chars), optional `replyToMessageId` | `discord.result` with `channelId`, `messageId`, `url` |
| `workspace.<entity>.<action>` | Connector-bound `sessionId`, action fields described below | `workspace.result` with `item`, deletion confirmation, or `items`/`total`/`nextAfterId` |

Errors use `type: error` with an `error` code. The unauthenticated `/ws` greeting demo is unrelated to this protocol.

A registration's identity comes from its bearer credential. Session ID collisions across installations are rejected. The same installation may rebind its ID during reconnect; cleanup of the old transport cannot remove the new registration. Disconnect removes that connection's registrations.

## Agent-controlled Discord chat

Chat RPC travels in the connector-to-backend direction over the **existing session WebSocket**, with normal envelope ID correlation. There is no second backend HTTP/MCP listener, token in the harness, or per-tool backend connection. The stdio MCP process calls the existing user-only Unix connector socket. In automatic mode, each call carries the native `threadId` from Codex MCP request metadata. The connector resolves exactly one matching registered native attachment across its backends; it rejects absent or ambiguous identity. No process environment or workspace fallback is used in this mode. Manual startup configuration can instead bind one Swarm session or one unambiguous native-thread/workspace attachment. Tool arguments cannot select a different session. Processes running as the same OS user remain inside the connector's local trust boundary.

Native `connect` installs the generic `swarm mcp --auto` stdio configuration with the selected daemon's `config/value/write`, then uses `config/mcpServer/reload` to refresh loaded threads. The shared registration contains the connector home and Python entry point, not a pinned conversation, backend credential, or project. Reconnecting preserves other servers and avoids redundant configuration writes. An unrelated `swarm` entry is never overwritten. Configuration and verification run outside the backend registration lock, so the MCP callback can query the local service during setup. Setup is serialized per daemon adapter and bounded to 25 seconds; local connect RPC permits 60 seconds for attachment, registration, and setup.

MCP readiness requires the selected thread's inventory to advertise the four status/chat tools and all fifteen workspace tools and a successful `swarm_status` call returning the same Swarm session ID and a connected backend. This call stays local and does not publish to Discord. The result is exposed as `mcp.ready` separately from registration and prompt-delivery readiness. Unsupported APIs, conflicts, missing tools, and failed verification report failure without starting, transferring, or restarting a harness conversation. Metadata-only sessions retain manual MCP setup.

The backend requires the session to belong to this authenticated connection; control capability is not required for MCP. The session's paired Discord owner determines guild access, regardless of Solo/Swarm mode. NetCord evaluates roles and channel overwrites, including thread parent permissions; private threads also require membership or thread-management rights. The Discord REST API enforces the bot's own access. Chat access is not scoped by project label or by the user who most recently sent `/agent`.

`channelId` addresses either an existing guild text channel or a thread. A send contains one attributed Discord message, with mentions disabled and an optional reply reference that fails when the target no longer exists. Archived/locked thread writes are rejected without changing thread state. History returns message/author IDs as strings, names, bot flags, content, timestamps, reply references, embed footer text, and up to five attachment links. It never downloads attachments. Page JSON stays below 54 KiB, reserving room for the envelope within the 64 KiB wire limit. If size limits shorten a page, its cursor still refers to the oldest included message; an individual oversized message fails explicitly.

MCP posts and automatic results share an embed footer format beneath the body: agent name; paired owner name and ID; harness; project; machine name and ID; session UUID; and channel/thread name and ID. Automatic results add the request UUID, status, and split-message part number. The bot needs Embed Links permission. MCP attribution is derived from the authenticated live registration; the agent cannot override it in tool arguments. The router snapshots attribution with the pending request, preserving the original identity after rename or disconnect. Discord name lookups are best-effort with ID fallbacks; no native harness IDs or workspace paths are added to the footer.

Up to 16 chat operations per connection run independently of the receive loop, each with an eight-second deadline, so Discord REST does not block harness requests, acknowledgments, or registrations. The connector waits ten seconds for a correlated backend response. Lost connections/timeouts on sends return uncertain delivery and are not retried or replayed. There is no durable chat idempotency store. Read the destination before resending. Read calls are snapshots, not live subscriptions; `/agent` remains the mechanism for waking the harness.

## Project workspace operations

Entities are `initiative`, `work_item`, and `document`; actions are `create`, `get`, `update`, `delete`, and `list`. They share the chat transport and the connection's limit of sixteen concurrent tool operations. No new listener, authentication token or Discord API call is introduced.

The connector exposes local operation `workspace`, resolves the calling attachment exactly as for chat, allowlists fields, and overwrites `sessionId` with the attachment's ID. The backend requires a live registration on that authenticated connection and looks up its project key plus paired owner's active server membership in SQLite on every request. Tool arguments cannot override project or author identity. Unknown/unprovisioned labels and departed members return `project_unavailable_or_not_allowed`.

Create accepts `title` and `briefMarkdown` (initiatives/work items) or `markdown` (documents). Work items also accept `type` (`issue` or `feature`), optional `initiativeId`, and `priority` (0–5). Get needs `id`. Update/delete need `id` and `expectedVersion`; stale writes return `version_conflict_read_again`. Updates preserve omitted values. Work items can change `status`, `type`, `priority` or parent, with `clearInitiative` explicitly unlinking the parent. Initiatives with live children reject deletion. Deletes are soft, document updates create revisions, and work-item changes append member/session-attributed events in the same transaction.

List accepts `query`, `limit` (1–20), `afterId` (default 0), and work-item filters `type`, `status`, `initiativeId`. Search treats `%` and `_` literally. Results are ordered by ID, omit bodies, include the total number of matches and a nullable `nextAfterId` cursor. Titles are at most 200 characters, briefs 4000 and document Markdown 8000. Failed reads are errors, never successful empty pages. See [backend storage and Discord handoff](../../Backend/docs/workspace.md) for provisioning and schema compatibility.

A lost reply to a mutating call yields `workspace_delivery_unknown_read_before_retry`. Inspect current state before retrying: there is no durable operation-id deduplication. Native MCP readiness verifies the expanded inventory but only calls the read-only local `swarm_status`; it does not mutate project data.

## Harness requests and results

The backend sends `agent.submit` or `agent.describe` through the selected session's installation socket:

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

For submissions the connector includes the originating channel/thread, Swarm session, and prompt receipt message IDs in the native harness prompt, so the agent can address and reply to the original request through chat tools.

`agent.describe` uses the same addressing fields and returns options without submitting input. The connector replies with `agent.reply`, the original envelope ID, and `payload.requestId`/`sessionId`:

| Reply status | Meaning |
| --- | --- |
| `available` | Description includes `models` (`id`, `name`, `reasoning`), current `model`, and current `reasoning`. |
| `queued` | Accepted into the connector's in-memory FIFO. |
| `injected` | Codex accepted steering for the expected active `turnId`. |
| `failed` | Rejected before confirmed delivery; `error` identifies the cause. |
| `unknown` | Acceptance is ambiguous. Check local Codex before resending. |

Subsequent `agent.event` envelopes have fresh envelope IDs but retain the original payload `requestId` and `sessionId`. Their status is `started`, `completed`, `failed`, `interrupted`, or `unknown`, with optional `turnId`, `output`, and `error`. The connector sends the acknowledgment before releasing event publication, including when Codex completes before its RPC reply arrives.

The Discord command creates a public prompt receipt before calling the router. The router retains the original prompt, receipt message ID, and Discord destination and accepts replies/events only from the selected session's authenticated connection. Automatic replies reference that receipt with `fail_if_not_exists=false` and retain a bounded prompt excerpt if the receipt has been deleted. Client events cannot override either prompt or receipt ID. A client cannot redirect results by supplying a channel or sender in an event. Authorization is checked at submission and again locally before queued delivery. `solo` requires the paired owner; `swarm` allows other bot users without project/guild scoping. Credential replacement is checked even on otherwise idle sockets.

## Codex delivery ownership

The daemon's control socket requires HTTP WebSocket Upgrade. It is not the JSONL transport of `codex app-server --listen stdio://`. Initialization opts into the experimental API required by the native queue methods. Attachment verifies `thread/read`, live `canAcceptDirectInput`, workspace identity, and `thread/queue/list` before subscribing with `thread/resume`. Stored history on another process is never resumed to manufacture a controllable session.

For queued requests, one worker per native thread waits for idle and an empty native queue, then applies explicit session settings, adds a native submission, and starts that exact submission with Codex's atomic start-if-idle operation. A definite idle-precondition failure can retry the same native entry. Correlation starts before `thread/queue/add`, because the native CLI can consume the submission before the add/start RPC replies. User-message notifications identify its turn using `clientId == requestId`. After an ambiguous or rejected queue operation, the connector also searches up to ten recent display-summary turns within a three-second reconciliation deadline and adopts only an exact client ID match. Completed matching turns hydrate final assistant text from history; commentary and unrelated turns are excluded. A definite busy-precondition failure may retry the same entry only after reconciliation finds no match. An unresolved ambiguous response cannot re-enqueue input or substitute `turn/steer`. Explicit settings persist in Codex for subsequent turns.

Injection uses `turn/steer` with `expectedTurnId`, fails if no turn is active, and rejects model/reasoning overrides. Result observation excludes agent messages completed before injection. Multiple injected requests may observe the same turn's result.

Before native acceptance, the connector owns the queued request and cancels it when the originating connection or attachment is no longer current. After native acceptance, Codex owns execution. Closing the connector cancels its workers/observers, not an active Codex turn. Native queue entries can survive. Prompt and result replay is deliberately absent because a lost acknowledgment does not prove non-delivery.

## Bounds and failure handling

| Resource | Limit |
| --- | --- |
| Backend wire message | 64 KiB of JSON text, across fragments and after UTF-8 encoding/escaping. |
| Prompt / returned output | 4,000 / 16,000 characters; encoded output/catalogs may shrink further to fit the frame. Prompts are never truncated. |
| Registrations | 100 per installation identity. |
| Controlled native threads | 100 per daemon adapter. |
| Local queued requests | 20 waiting requests per native thread, in addition to its running request. |
| Backend outstanding requests | 100 per connection, 1,000 globally, including description lookups. |
| Backend acknowledgment wait | 12 seconds; Discord autocomplete uses a shorter two-second caller deadline. |
| Codex RPC wait | Eight seconds; a timeout of a mutating request is ambiguous. |
| Backend live request correlation | Pruned after 24 hours when new work is admitted; lost on backend restart. SQLite request/event history remains. |
| Connector duplicate cache | Up to 500 recent acknowledgments plus in-flight entries, per live backend connection. |
| Discord publication queue | 256 events; bounded backpressure. |

Socket sends are serialized. Harness operations run in independent tasks so waiting on Codex does not block registration or heartbeat replies. The duplicate cache shares an in-flight acknowledgment instead of submitting the same request twice; it is not durable idempotency across reconnects. Transport reconnect uses jittered exponential backoff and restores only live registrations.

Only agent message text is collected. Explicit `commentary` phase messages are excluded from automatic relay; final answers and legacy messages without phase metadata retain relay behavior. Reasoning, tool arguments, transcript history, and approval payloads are not published. Discord mentions are disabled; local approval handling remains the responsibility of the Codex UI. Agents can explicitly post shared questions through MCP, then read replies or receive a further `/agent` instruction.
