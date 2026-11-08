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
| `SessionSocketController` | Authenticated backend transport, frame bounds, serialized sends. |
| `SessionRegistry` | Installation ownership, reconnect rebinding, Solo/Swarm availability. |
| `AgentRouter` | Request admission, acknowledgment waiters, authenticated event routing. |
| `AgentCommand`, `AgentAutocomplete` | Discord interaction and caller-scoped choices. |
| `AgentDiscordPublisher` | Bounded Discord publication outside the socket receive loop. |

## Pairing and registration

Pairing uses `POST /api/pairing/start` (`machineId`, `machineName`) and `POST /api/pairing/complete` (`pairingId`, connector `secret`, Discord `code`). Only the authenticated `/swarm-pair` interaction approves a challenge. The backend stores hashed credentials in memory; pairing the same owner/machine replaces its credential.

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

Errors use `type: error` with an `error` code. The unauthenticated `/ws` greeting demo is unrelated to this protocol.

A registration's identity comes from its bearer credential. Session ID collisions across installations are rejected. The same installation may rebind its ID during reconnect; cleanup of the old transport cannot remove the new registration. Disconnect removes that connection's registrations.

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

`agent.describe` uses the same addressing fields and returns options without submitting input. The connector replies with `agent.reply`, the original envelope ID, and `payload.requestId`/`sessionId`:

| Reply status | Meaning |
| --- | --- |
| `available` | Description includes `models` (`id`, `name`, `reasoning`), current `model`, and current `reasoning`. |
| `queued` | Accepted into the connector's in-memory FIFO. |
| `injected` | Codex accepted steering for the expected active `turnId`. |
| `failed` | Rejected before confirmed delivery; `error` identifies the cause. |
| `unknown` | Acceptance is ambiguous. Check local Codex before resending. |

Subsequent `agent.event` envelopes have fresh envelope IDs but retain the original payload `requestId` and `sessionId`. Their status is `started`, `completed`, `failed`, `interrupted`, or `unknown`, with optional `turnId`, `output`, and `error`. The connector sends the acknowledgment before releasing event publication, including when Codex completes before its RPC reply arrives.

The router retains the original Discord destination and accepts replies/events only from the selected session's authenticated connection. A client cannot redirect results by supplying a channel or sender in an event. Authorization is checked at submission and again locally before queued delivery. `solo` requires the paired owner; `swarm` allows other bot users without project/guild scoping. Credential replacement is checked even on otherwise idle sockets.

## Codex delivery ownership

The daemon's control socket requires HTTP WebSocket Upgrade. It is not the JSONL transport of `codex app-server --listen stdio://`. Initialization opts into the experimental API required by the native queue methods. Attachment verifies `thread/read`, live `canAcceptDirectInput`, workspace identity, and `thread/queue/list` before subscribing with `thread/resume`. Stored history on another process is never resumed to manufacture a controllable session.

For queued requests, one worker per native thread waits for idle and an empty native queue, then applies explicit session settings, adds a native submission, and starts that exact submission with Codex's atomic start-if-idle operation. A definite idle-precondition failure can retry the same native entry. An ambiguous start response cannot re-enqueue it or substitute `turn/steer`. Explicit settings persist in Codex for subsequent turns.

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
| Backend request history | Pruned after 24 hours when new work is admitted; lost on backend restart. |
| Connector duplicate cache | Up to 500 recent acknowledgments plus in-flight entries, per live backend connection. |
| Discord publication queue | 256 events; bounded backpressure. |

Socket sends are serialized. Harness operations run in independent tasks so waiting on Codex does not block registration or heartbeat replies. The duplicate cache shares an in-flight acknowledgment instead of submitting the same request twice; it is not durable idempotency across reconnects. Transport reconnect uses jittered exponential backoff and restores only live registrations.

Only agent message text is collected. Reasoning, tool arguments, transcript history, and approval payloads are not published. Discord mentions are disabled; local approval and question handling remain the responsibility of the Codex UI.
