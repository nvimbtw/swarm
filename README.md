# Swarm

A shared project workspace for people and coding agents, with Discord as the main place for discussion and collaboration.

Swarm connects project conversations, issue and feature tracking, and agent sessions running on different people's machines. A team can discuss an idea in Discord, turn it into tracked work, ask a connected agent to implement it, and keep questions, decisions, and results in the same thread.

## Project status

This repository is the beginning of a modular rebuild of an earlier Python Discord bot and MCP server. The new backend currently contains the generated ASP.NET Core Web API scaffold and its weather example.

The architecture and workflows below describe the intended system. Session connections, Discord integration, persistence, and the dashboard are not implemented yet. Harness integration capabilities, including prompt injection into an existing session, still need to be verified for each supported environment.

## How it will work

1. People discuss an idea in a Discord thread. An agent can join the discussion when invoked.
2. An agent can suggest a breakdown or, when asked, create issues and features from that discussion.
3. A user opens a coding harness in a local project and invokes `/swarm-connect` to connect that existing session.
4. The local connector registers the session and establishes an authenticated WebSocket connection to the server.
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
| Local connector | Python, planned | Maintain the session's WebSocket connection, expose MCP tools, and use harness-specific adapters to deliver requests. |
| Dashboard API | Part of the central server | Provide snapshots and live updates about projects, sessions, queues, and work. |
| Dashboard | React / TypeScript, proposed | Display connected agents, activity, task progress, and agent animations. Frontend tooling is still to be finalised. |
| Database | SQLite, planned | Store operational state, tracked items, briefs, document records, relationships, and history. |
| Connection skill | `SKILL.md` and supporting instructions | Launch the local connector and connect the current harness session. |

The backend is one modular application. Discord, dashboard, and connection modules use shared services so that task operations follow the same rules across entry points.

Local connectors initiate their connections to the server. Each connected harness session has a WebSocket connection, and each dashboard instance receives live updates through its own connection.

The dashboard is a visual monitoring interface. Its physical presentation—browser, touchscreen, or a display with dedicated controls—will be decided later.

## Repository layout

```text
Swarm/
├── Backend/
│   └── Swarm.Server/    # ASP.NET Core server
├── Client/             # Local connector, MCP tools, and harness adapters
├── Skills/             # Session connection skill
├── Paperwork/          # Coursework and project documentation
└── README.md
```

`Client/` and `Skills/` are placeholders. A `Frontend/` directory is planned for the dashboard.

## Responsibilities

| Team member | Primary responsibility |
| --- | --- |
| Tadej Gungl | Core backend, SQLite and the data model, client–server WebSocket communication, local connector, harness adapters, MCP, and the connection skill. |
| Jan Vidovič | Dashboard frontend and the backend API that supplies its data. |
| Aleks Detiček | Backend integration with the Discord API, including commands, threads, and published updates. |

