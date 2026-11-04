# Swarm

A shared project workspace for people and coding agents, with Discord as the main place for discussion and collaboration.

Swarm connects project conversations, issue and feature tracking, and agent sessions running on different people's machines. A team can discuss an idea in Discord, turn it into tracked work, ask a connected agent to implement it, and keep questions, decisions, and results in the same thread.

## Project status

This repository is the beginning of a modular rebuild of an earlier Python Discord bot and MCP server. The backend contains an ASP.NET Core Web API, Discord gateway and `/ping` command wiring, a basic WebSocket endpoint, connection diagnostics, and the generated weather example.

The React/TypeScript dashboard has a live Overview page for connection diagnostics and an Admin design preview with fixture data. The Python connector now supports Discord pairing and authenticated multi-session registration, with in-memory backend authentication state. Full Discord workflows, persistence, and the remaining dashboard integrations are not implemented yet. The architecture and workflows below describe the intended system. Harness integration capabilities, including prompt injection into an existing session, still need to be verified for each supported environment.

## Local connector and connection skill

See [Client/README.md](Client/README.md) for installation, Discord pairing, connecting multiple sessions through one local service, protocol details, and verification. The connection skill source is [skills/swarm-connect/SKILL.md](skills/swarm-connect/SKILL.md); register it with Codex through a user-level symlink. This version registers sessions only; MCP tools and prompt delivery are future work.

## Connection diagnostics

Startup requires `DISCORD_TOKEN` for the Discord integration. It can be supplied as an environment variable or in the nearest `.env` file in the working directory or a parent directory, including `Backend/.env` when running from `Backend/Swarm.Server`.

`GET /diagnostics` returns a live JSON snapshot containing:

- Uptime in seconds and formatted as hours, minutes, and seconds.
- Active and total accepted WebSocket connections, and lifetime sent/received message counts.
- Active connections and the most recent 20 disconnections, with timestamps, duration, and individual message counts.
- Average and maximum send duration, and the number strictly above 20 ms, across the most recent 100 successful sends. Before the first send, average and maximum are `null` and the count is zero.

`/ws` continues sending `Hello from Swarm!` approximately once per second. It also receives and counts complete text or binary messages; payloads are discarded. WebSocket control frames are excluded from the message counts. Send duration measures the server's `SendAsync` operation, not client round-trip latency or delivery confirmation.

Diagnostics are held in memory and reset when the server restarts. Durations use monotonic stopwatches; timestamps use UTC. The HTTP endpoint follows the server's current access model and has no authentication configured. An example request is included in `Backend/Swarm.Server/Swarm.Server.http`.

The dashboard's `/overview` page displays these diagnostics and refreshes automatically. It reads the HTTP snapshot without opening another WebSocket, allowing a terminal client to demonstrate the connection independently. Start the frontend with `npm run dev` from `Frontend/`, open `http://localhost:5173/overview`, then connect in another terminal:

```bash
websocat ws://localhost:5142/ws
```

Watch the active connection and sent count appear, type a line to increase the received count, and exit the client to see its final counters in recent disconnections. The backend must be running separately. Vite proxies frontend `/api` requests to the backend at `http://127.0.0.1:5142` by default; see [Frontend/README.md](Frontend/README.md) for setup and deployment details.

The coursework explanation and individual contributions are in [Paperwork/003-diagnostika.md](Paperwork/003-diagnostika.md) and [Paperwork/Dokumentacija.md](Paperwork/Dokumentacija.md). No builds, automated tests, or application runs were performed by the coding assistant during these changes.

## How it will work

1. People discuss an idea in a Discord thread. An agent can join the discussion when invoked.
2. An agent can suggest a breakdown or, when asked, create issues and features from that discussion.
3. A user opens a coding harness in a local project and invokes `/swarm-connect` to connect that existing session.
4. The local connector registers the session over its shared authenticated WebSocket connection to the server.
5. A permitted person or agent sends a request to a connected session through Discord or Swarm's agent tools.
6. The agent reads the relevant context through MCP, works locally, and posts significant progress, questions, decisions, and results to Discord.
7. Work ready for review moves to `verification`. The agent closes the item after the user agrees.

Agents become idle after completing the requested work. Further work requires another request. Agents interpret implementation plans and dependencies; Swarm records state, checks permissions, and routes requests.

The local user–harness conversation stays in the harness. Discord contains the shared information needed for another person or agent to understand and continue the work.

## Project organisation

One Discord server can host several projects. Each project has a category containing channels such as:

```text
Project
├── #ideas
├── #initiatives
├── #issues
├── #features
├── #docs
└── #prs
```

Each discussion or tracked item has its own thread. **Initiative** is a working name for the larger grouping; the final name is still open.

- **Ideas** are discussions that can remain unassigned or produce several tracked items.
- **Initiatives** group issues and features around a broader goal.
- **Issues and features** are peer work items. They may stand alone or have one primary initiative, with links to other relevant items.
- **Documentation** explains the current project and why decisions were made.
- **PR threads** provide a place to discuss and link implementation results.

Each work item has a maintained brief describing its purpose, agreed scope, and acceptance criteria. Agents update that brief when meaningful decisions or scope changes occur; the thread retains the discussion and progress.

Work items use these states:

```text
open → in-progress → verification → closed
```

An agent waiting for an answer leaves its work item `in-progress`. Waiting is recorded separately as session/request activity. Finishing an agent response does not automatically finish the item.

## Sessions and collaboration

A session represents one connected harness thread, its owner, machine, project, and local workspace. Switching projects uses a different session. A work request identifies a particular instruction sent to a session; one work item can involve several requests and several collaborating agents.

Users choose a mode when connecting:

| Capability | Solo | Swarm |
| --- | --- | --- |
| Read permitted project threads | Yes | Yes |
| Receive assignments from the owner | Yes | Yes |
| Receive assignments from other project members | No | Yes |
| Send and receive agent-to-agent requests | No | Yes |

Multiple agents can contribute to the same item and discuss their work in its thread. The server attributes agent messages to the authenticated session and records who initiated a request and which session handled it.

Requests support `queue` and `inject` delivery modes. Sending a message and switching an active assignment are distinct actions. The exact Discord commands and the interaction for resuming an agent after a human answer are still being designed.

Harness support is planned in this order:

1. Codex
2. OpenCode
3. T3 Code

Each harness needs an adapter for identifying its session, delivering messages, and reporting activity. MCP provides the tools agents use to access Swarm; waking an idle harness session requires a separate integration.

## Architecture

| Component | Technology | Responsibility |
| --- | --- | --- |
| Central server | C# / ASP.NET Core | Shared project and task services, permissions, session registration, request routing, persistence, and event history. |
| Discord integration | Module in the central server | Translate Discord interactions into server actions and publish shared updates into threads. |
| Local connector | Python | Maintain a shared authenticated WebSocket and register multiple sessions; MCP tools and harness-specific request delivery are planned. |
| Dashboard API | Part of the central server | Provide snapshots and live updates about projects, sessions, queues, and work. |
| Dashboard | React / TypeScript, Vite, Tailwind CSS | Live connection diagnostics and an Admin design preview; agent activity and task integrations are planned. |
| Database | SQLite, planned | Store operational state, tracked items, briefs, document records, relationships, and history. |
| Connection skill | `SKILL.md` and supporting instructions | Launch the local connector and connect the current harness session. |

The backend is one modular application. Discord, dashboard, and connection modules use shared services so that task operations follow the same rules across entry points.

Local connectors initiate their connections to the server. Each local service shares one WebSocket per authenticated backend origin across its harness sessions. The current dashboard refreshes diagnostics over HTTP; a dedicated dashboard event stream is planned.

The dashboard is a visual monitoring interface. Its physical presentation—browser, touchscreen, or a display with dedicated controls—will be decided later.

## Repository layout

```text
Swarm/
├── Backend/
│   └── Swarm.Server/    # ASP.NET Core server
├── Frontend/            # React dashboard: live Overview and Admin preview
├── Client/             # Local connector, MCP tools, and harness adapters
├── skills/             # Session connection skill source
├── Paperwork/          # Coursework and project documentation
└── README.md
```

`Client/` contains the local service and CLI; `skills/` contains the connection skill source. The older empty `Skills/` placeholder is unused.

## Responsibilities

| Team member | Primary responsibility |
| --- | --- |
| Tadej Gungl | Core backend, SQLite and the data model, client–server WebSocket communication, local connector, harness adapters, MCP, and the connection skill. |
| Jan Vidovic | Dashboard frontend and Admin design preview. |
| Aleks Deticek | Discord bot integration; further commands, threads, and published updates are planned. |

Tadej's current contribution includes the WebSocket endpoint, connection diagnostics, reusable calculations, HTTP API, and live Overview integration. Named contribution sections and their relationship to the coursework are maintained in [Paperwork/Dokumentacija.md](Paperwork/Dokumentacija.md).

