using Microsoft.Data.Sqlite;
using Swarm.Server.Workspace;

namespace Swarm.Server.Db.Repositories;

public sealed partial class WorkspaceRepository
{
    private static async Task<object> WriteAsync(SqliteConnection connection, SqliteTransaction transaction,
        string entity, string table, string action, long projectId, long memberId, Guid sessionId,
        WorkspaceRequest request, CancellationToken ct)
    {
        async Task Execute(string sql, params (string Name, object? Value)[] args) =>
            await DatabaseQueries.ExecuteAsync(connection, transaction, sql, ct, args);
        async Task<long> Scalar(string sql, params (string Name, object? Value)[] args) =>
            await DatabaseQueries.ScalarLongAsync(connection, transaction, sql, ct, args);

        WorkspaceItem? old = null;
        if (action != "create")
        {
            old = await GetAsync(connection, transaction, entity, table, projectId, request.Id!.Value, ct);
            if (old.Version != request.ExpectedVersion) throw new WorkspaceException("version_conflict_read_again");
        }
        if (request.InitiativeId is long initiativeId && await Scalar(
            "SELECT COUNT(*) FROM Initiative WHERE ID=$id AND ProjectID=$project AND DeletedAt IS NULL;",
            ("$id", initiativeId), ("$project", projectId)) != 1)
            throw new WorkspaceException("initiative_unavailable_in_project");
        if (entity == "initiative" && action == "delete" && await Scalar(
            "SELECT COUNT(*) FROM WorkItem WHERE InitiativeID=$id AND DeletedAt IS NULL;", ("$id", old!.Id)) > 0)
            throw new WorkspaceException("initiative_has_work_items");

        string now = DateTimeOffset.UtcNow.ToString("O");
        long id = old?.Id ?? 0;
        string title = request.Title?.Trim() ?? old?.Title ?? "";
        string brief = request.BriefMarkdown ?? old?.BriefMarkdown ?? "";
        string status = request.Status ?? old?.Status ?? "open";
        if (action == "create")
        {
            string sql = entity switch
            {
                "initiative" => """
                    INSERT INTO Initiative (ProjectID, Title, BriefMarkdown, CreatedAt, UpdatedAt)
                    VALUES ($project, $title, $brief, $now, $now);
                    """,
                "work_item" => """
                    INSERT INTO WorkItem (ProjectID, Title, BriefMarkdown, Type, Status, Priority, InitiativeID, CreatedAt, UpdatedAt)
                    VALUES ($project, $title, $brief, $type, $status, $priority, $initiative, $now, $now);
                    """,
                _ => """
                    INSERT INTO Document (ProjectID, Title, CreatedAt, UpdatedAt)
                    VALUES ($project, $title, $now, $now);
                    """
            };
            var parameters = new List<(string Name, object? Value)>
            {
                ("$project", projectId), ("$title", title), ("$now", now)
            };
            if (entity != "document") parameters.Add(("$brief", brief));
            if (entity == "work_item")
            {
                parameters.Add(("$type", request.Type!));
                parameters.Add(("$status", status));
                parameters.Add(("$priority", request.Priority ?? 0));
                parameters.Add(("$initiative", request.InitiativeId));
            }
            await Execute(sql, parameters.ToArray());
            id = await Scalar("SELECT last_insert_rowid();");
        }
        else if (action == "update")
        {
            string sql = entity switch
            {
                "initiative" => """
                    UPDATE Initiative SET Title=$title, BriefMarkdown=$brief, UpdatedAt=$now, Version=Version+1
                    WHERE ID=$id;
                    """,
                "work_item" => """
                    UPDATE WorkItem SET Title=$title, BriefMarkdown=$brief, Type=$type, Status=$status,
                        Priority=$priority, InitiativeID=$initiative, UpdatedAt=$now, Version=Version+1
                    WHERE ID=$id;
                    """,
                _ => """
                    UPDATE Document SET Title=$title, UpdatedAt=$now, Version=Version+1 WHERE ID=$id;
                    """
            };
            var parameters = new List<(string Name, object? Value)>
            {
                ("$id", id), ("$title", title), ("$now", now)
            };
            if (entity != "document") parameters.Add(("$brief", brief));
            if (entity == "work_item")
            {
                long? updatedInitiativeId = request.ClearInitiative ? null : request.InitiativeId ?? old!.InitiativeId;
                parameters.Add(("$type", request.Type ?? old!.Type));
                parameters.Add(("$status", status));
                parameters.Add(("$priority", request.Priority ?? old!.Priority));
                parameters.Add(("$initiative", updatedInitiativeId));
            }
            await Execute(sql, parameters.ToArray());
        }
        else
            await Execute($"UPDATE {table} SET DeletedAt=$now, UpdatedAt=$now, Version=Version+1 WHERE ID=$id;",
                ("$now", now), ("$id", id));

        if (entity == "document" && action != "delete")
            await Execute("""
                INSERT INTO DocumentRevision (DocumentID, RevisionNo, Markdown, AuthorMemberID, AuthorSessionID, PublishState, CreatedAt)
                VALUES ($id, $version, $markdown, $member, $session, 'pending', $now);
                """, ("$id", id), ("$version", (old?.Version ?? 0) + 1),
                ("$markdown", request.Markdown ?? old?.Markdown ?? ""), ("$member", memberId),
                ("$session", sessionId.ToString()), ("$now", now));
        if (entity == "work_item")
            await Execute("""
                INSERT INTO WorkItemEvent (WorkItemID, ActorMemberID, ActorSessionID, Kind, OldStatus, NewStatus, Summary, CreatedAt)
                VALUES ($id, $member, $session, $kind, $old, $new, $summary, $now);
                """, ("$id", id), ("$member", memberId), ("$session", sessionId.ToString()),
                ("$kind", action), ("$old", (object?)old?.Status ?? DBNull.Value), ("$new", status),
                ("$summary", title), ("$now", now));
        return action == "delete" ? new { id, deleted = true, version = old!.Version + 1 }
            : new { item = await GetAsync(connection, transaction, entity, table, projectId, id, ct) };
    }

}
