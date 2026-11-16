# Workspace storage and Discord handoff

An initiative is the large parent (an epic). Issues and features are peer work items linked through `WorkItem.InitiativeID`; they do not contain each other. Standalone work items are supported, as in the project proposal. An initiative with live children cannot be deleted: move or unlink the children first.

## Diagram coverage

Before this change, eight of the nineteen tables in `Paperwork/Database.drawio.xml` existed: User, Member, Server, Machine, Harness, Session, WorkRequest, RequestEvent. MachineCredential is an additional authentication table.

`WorkspaceSchema` adds all eleven missing tables: Project, Channel, Thread, Initiative, WorkItem, WorkItemLink, WorkItemEvent, Document, DocumentRevision, DocumentMessage, Question. Startup applies the additions transactionally and can run repeatedly against an existing database.

Compatibility choices:

- Existing sessions keep their free-text `ProjectId` and user ownership. `WorkspaceProjectID` and `OwnerMemberID` add relational bindings without breaking the connector protocol. SQLite treats `ProjectId` and `ProjectID` as the same column name, so the extra name is necessary.
- WorkRequest keeps RequesterUserID and Status, adding RequesterMemberID and CreatedAt. Harness keeps Name rather than the diagram's Type. Session keeps SessionName rather than Name. Existing RequestEvent output/error fields remain.
- Discord category, channel, thread and message references are nullable where creation must work before Discord publishing. No fake Discord identifiers or threads are generated.
- Version and UpdatedAt columns support safe edits; Project.Key binds the connector's project label to a provisioned project.
- The existing WorkRequest ThreadID/WorkItemID columns still lack foreign-key declarations on legacy databases. They are not written by these tools. Question, links, and Discord publishing tables are schema foundations, not newly exposed tools.

## Discord integration points

Workspace writes commit before any future Discord side effect. TODO comments mark the handoff. Initiatives, work items and documents have nullable unique ThreadID references; the Discord implementation can create a Thread under a project Channel and attach it later. DocumentRevision.PublishState starts as `pending`. Publishing should record each DiscordMessage part in DocumentMessage and then mark that revision published. Soft deletes preserve thread mappings, document revisions and work-item events for later archival.

There is no new Discord publishing or Discord API call in workspace CRUD.

## Provision a project before using its tools

The connector's project label is not an access grant. The operator can provision a project and its server members through backend configuration (`appsettings.Development.json` or the equivalent environment configuration):

```json
{
  "Workspace": {
    "Projects": [
      {
        "Key": "swarm",
        "Name": "Swarm",
        "GuildId": "YOUR_DISCORD_SERVER_ID",
        "MemberDiscordIds": ["YOUR_DISCORD_USER_ID", "TEAMMATE_DISCORD_USER_ID"]
      }
    ]
  }
}
```

Use that exact `Key` with `swarm connect --project swarm`. Startup creates the Server/Project and grants the configured users active Member rows. This provisioning is additive: removing an ID from configuration does not revoke existing membership. To revoke it, set that member's LeftAt in the database and remove the configuration grant so startup does not reactivate it. All active members of the same server can use that server's projects; project-specific roles/invitations are not implemented. The future Discord setup path can use the same tables instead of configuration.

Every workspace call checks the attached session's paired owner against current database membership. It never trusts project, author or session overrides from a tool. Existing session registration and Discord tools continue working for labels that are not provisioned, but workspace tools return `project_unavailable_or_not_allowed` for them.

## Tool and storage behavior

There are fifteen workspace tools: create, get, update, delete and list for initiatives, work items, and documents. Issues and features share work_item tools with `type=issue|feature`. Work-item lists accept initiative, type and status filters. All lists search literal title substrings, return summaries, order by ID, and accept `after_id` for the next page (`nextAfterId`). Total counts include all matches, independent of the current page. Pages contain at most twenty items. Get reads the full brief/Markdown.

Updates and deletes require `expected_version` from the last read. A mismatch returns `version_conflict_read_again`; the agent must reread and reconcile instead of blindly overwriting. Omitted update fields remain unchanged. `clear_initiative=true` unlinks a work item; otherwise `initiative_id` moves it to another live initiative in the same project. Priorities are integers from 0 to 5. Work-item status is open, in-progress, verification or closed. Tool instructions tell agents to request user agreement before closing completed work; the database does not verify that conversation.

Documents retain immutable revisions, including author member/session, while work-item mutations append attributed events. Deletes mark DeletedAt and preserve history. Initiatives with active children cannot be deleted. Standalone work items remain visible through a LEFT JOIN in WorkItemOverview. Markdown is limited to 8000 characters, briefs to 4000, and titles to 200 to keep responses inside the shared socket frame.

Writes are never replayed automatically. A lost response can mean the database committed: `workspace_delivery_unknown_read_before_retry` tells the agent to inspect current state before deciding whether to repeat an action. Exactly-once creation across uncertain delivery is not provided.

## Verification

Run `dotnet run --project Backend/Swarm.Server.Tests` for schema, startup idempotency, hierarchy, CRUD, version conflicts, revisions, attribution, pagination, literal search, disconnected lists and project authorization checks. The tests use disposable SQLite files, not the application's database. Connector tests exercise all fifteen tools over stdio MCP and the shared authenticated WebSocket, and verify that no Discord posts occur.
