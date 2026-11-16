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
