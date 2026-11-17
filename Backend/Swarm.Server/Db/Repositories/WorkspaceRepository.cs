using Microsoft.Data.Sqlite;
using Swarm.Server.Classi;
using Swarm.Server.Workspace;

namespace Swarm.Server.Db.Repositories;

public sealed partial class WorkspaceRepository(DatabaseService database) : IWorkspaceRepository
{
    public async Task<object> ExecuteAsync(RegisteredSession session, string kind, WorkspaceRequest request,
        CancellationToken ct)
    {
        var parts = kind.Split('.');
        if (parts.Length != 3 || parts[0] != "workspace" ||
            parts[1] is not ("initiative" or "work_item" or "document") ||
            parts[2] is not ("create" or "get" or "update" or "delete" or "list"))
            throw new WorkspaceException("unknown_workspace_operation");
        string entity = parts[1], action = parts[2];
        string table = entity switch { "initiative" => "Initiative", "work_item" => "WorkItem", _ => "Document" };
        Validate(entity, action, request);

        await using var connection = database.CreateConnection();
        await connection.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();
        // Project labels are preferences, never grants. Membership is provisioned by the server operator.
        long projectId, memberId;
        using (var access = connection.CreateCommand())
        {
            access.Transaction = transaction;
            access.CommandText = """
                SELECT p.ID, m.ID FROM Project p
                JOIN Member m ON m.ServerID=p.ServerID
                JOIN "User" u ON u.ID=m.UserID
                WHERE p.Key=$key AND u.DiscordUserID=$owner AND m.LeftAt IS NULL;
                """;
            access.Parameters.AddWithValue("$key", session.ProjectId);
            access.Parameters.AddWithValue("$owner", session.Identity.OwnerId);
            await using var reader = await access.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new WorkspaceException("project_unavailable_or_not_allowed");
            projectId = reader.GetInt64(0);
            memberId = reader.GetInt64(1);
        }

        object result = action switch
        {
            "list" => await ReadPageAsync(connection, transaction, entity, table, projectId, request, ct),
            "get" => new { item = await GetAsync(connection, transaction, entity, table, projectId, request.Id!.Value, ct) },
            _ => await WriteAsync(connection, transaction, entity, table, action, projectId, memberId, session.SessionId, request, ct)
        };
        transaction.Commit();
        // TODO: Show create/update/delete changes in Discord here, after the database commit.
        // Create/update/archive the linked thread. Publish pending document revisions into DocumentMessage parts.
        return result;
    }
}
