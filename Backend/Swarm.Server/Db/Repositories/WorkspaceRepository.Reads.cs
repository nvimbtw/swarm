using Microsoft.Data.Sqlite;
using Swarm.Server.Workspace;

namespace Swarm.Server.Db.Repositories;

public sealed partial class WorkspaceRepository
{
    private static async Task<WorkspacePage> ReadPageAsync(SqliteConnection connection,
        SqliteTransaction transaction, string entity, string table, long projectId,
        WorkspaceRequest request, CancellationToken ct)
    {
        string source = entity == "work_item" ? "WorkItemOverview" : table;
        string where = "ProjectID=$project AND DeletedAt IS NULL AND Title LIKE $query ESCAPE '\\'";
        var parameters = new List<(string Name, object? Value)> { ("$project", projectId), ("$query", "%" + EscapeLike(request.Query ?? "") + "%") };
        if (entity == "work_item")
        {
            if (request.Status is not null) { where += " AND Status=$status"; parameters.Add(("$status", request.Status)); }
            if (request.Type is not null) { where += " AND Type=$type"; parameters.Add(("$type", request.Type)); }
            if (request.InitiativeId is not null) { where += " AND InitiativeID=$initiative"; parameters.Add(("$initiative", request.InitiativeId.Value)); }
        }
        long total = await DatabaseQueries.ScalarLongAsync(connection, transaction, $"SELECT COUNT(*) FROM {source} WHERE {where};", ct, parameters.ToArray());
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT * FROM {source} WHERE {where} AND ID>$after ORDER BY ID LIMIT $limit;";
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        command.Parameters.AddWithValue("$after", request.AfterId);
        command.Parameters.AddWithValue("$limit", request.Limit + 1);
        var items = new List<WorkspaceItem>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) items.Add(Read(reader, entity, summary: true));
        bool more = items.Count > request.Limit;
        if (more) items.RemoveAt(items.Count - 1);
        return new WorkspacePage(items, total, more ? items[^1].Id : null);
    }

    private static async Task<WorkspaceItem> GetAsync(SqliteConnection connection, SqliteTransaction transaction,
        string entity, string table, long projectId, long id, CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        string source = entity == "work_item" ? "WorkItemOverview" : table;
        command.CommandText = entity == "document" ? """
            SELECT d.*, r.Markdown FROM Document d
            JOIN DocumentRevision r ON r.DocumentID=d.ID AND r.RevisionNo=d.Version
            WHERE d.ID=$id AND d.ProjectID=$project AND d.DeletedAt IS NULL;
            """ : $"SELECT * FROM {source} WHERE ID=$id AND ProjectID=$project AND DeletedAt IS NULL;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$project", projectId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new WorkspaceException("item_not_found");
        return Read(reader, entity, summary: false);
    }

    private static WorkspaceItem Read(SqliteDataReader reader, string entity, bool summary)
    {
        string? Text(string column) => reader[column] is DBNull ? null : (string)reader[column];
        bool isWorkItem = entity == "work_item";
        return new WorkspaceItem(
            Id: reader.GetInt64(reader.GetOrdinal("ID")),
            ProjectId: reader.GetInt64(reader.GetOrdinal("ProjectID")),
            Title: Text("Title")!,
            Version: reader.GetInt32(reader.GetOrdinal("Version")),
            CreatedAt: Text("CreatedAt")!,
            UpdatedAt: Text("UpdatedAt")!,
            BriefMarkdown: !summary && entity != "document" ? Text("BriefMarkdown") : null,
            Markdown: !summary && entity == "document" ? Text("Markdown") : null,
            Type: isWorkItem ? Text("Type") : null,
            Status: isWorkItem ? Text("Status") : null,
            Priority: isWorkItem ? reader.GetInt32(reader.GetOrdinal("Priority")) : null,
            InitiativeId: isWorkItem && reader["InitiativeID"] is not DBNull
                ? reader.GetInt64(reader.GetOrdinal("InitiativeID")) : null,
            InitiativeTitle: isWorkItem ? Text("InitiativeTitle") : null);
    }

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

}
