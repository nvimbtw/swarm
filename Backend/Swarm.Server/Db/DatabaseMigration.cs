
using Microsoft.Data.Sqlite;

namespace Swarm.Server.Db;

public static class DatabaseMigration
{
    public static async Task MigrateOldSchemaAsync(SqliteConnection connection)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA table_info(Session);";

        await using var reader = await check.ExecuteReaderAsync();

        bool hasSession = false;
        bool hasNewOwner = false;

        while (await reader.ReadAsync())
        {
            hasSession = true;

            if (reader.GetString(1) == "OwnerUserID")
                hasNewOwner = true;
        }

        await reader.DisposeAsync();

        if (!hasSession || hasNewOwner)
            return;

        await ExecuteAsync(connection, "PRAGMA foreign_keys=OFF;");

        await ExecuteAsync(connection, @"
            ALTER TABLE RequestEvent RENAME TO RequestEvent_Old;
            ALTER TABLE WorkRequest RENAME TO WorkRequest_Old;
            ALTER TABLE Session RENAME TO Session_Old;");

        await ExecuteAsync(connection, Swarm.Server.Classi.Session.CreateTableSql());
        await ExecuteAsync(connection, Swarm.Server.Classi.WorkRequest.CreateTableSql());
        await ExecuteAsync(connection, Swarm.Server.Classi.RequestEvent.CreateTableSql());

        await ExecuteAsync(connection, @"
            INSERT OR IGNORE INTO Session (ID, OwnerUserID, MachineID, HarnessID, SessionName, ProjectId,
                Control, ExternalThreadID, WorkspacePath, Mode, ConnectionState, ActivityState,
                ActivitySummary, ConnectedAt, LastSeenAt, DisconnectedAt)
            SELECT s.ID, m.UserID, s.MachineID, s.HarnessID, '', '', 0, s.ExternalThreadID,
                s.WorkspacePath, s.Mode, s.ConnectionState, s.ActivityState, s.ActivitySummary,
                s.ConnectedAt, s.LastSeenAt, s.DisconnectedAt
            FROM Session_Old s JOIN Member m ON m.ID=s.OwnerMemberID;

            INSERT OR IGNORE INTO WorkRequest (ID, TargetSessionID, RequesterUserID, RequesterSessionID,
                ParentRequestID, ThreadID, WorkItemID, Kind, DeliveryMode, Prompt, Status, Model,
                Reasoning, Error, DeliveredAt, StartedAt, FinishedAt)
            SELECT w.ID, w.TargetSessionID, m.UserID, w.RequesterSessionID, w.ParentRequestID,
                w.ThreadID, w.WorkItemID, w.Kind, w.DeliveryMode, w.Prompt, w.Status, w.Model,
                w.Reasoning, w.Error, w.DeliveredAt, w.StartedAt, w.FinishedAt
            FROM WorkRequest_Old w JOIN Member m ON m.ID=w.RequesterMemberID
            JOIN Session s ON s.ID=w.TargetSessionID;

            INSERT INTO RequestEvent (ID, RequestID, Kind, SummaryMarkdown, DiscordMessageID,
                Output, Error, CreatedAt)
            SELECT e.ID, e.RequestID, e.Kind, e.SummaryMarkdown, e.DiscordMessageID,
                e.Output, e.Error, e.CreatedAt
            FROM RequestEvent_Old e JOIN WorkRequest w ON w.ID=e.RequestID;

            DROP TABLE RequestEvent_Old;
            DROP TABLE WorkRequest_Old;
            DROP TABLE Session_Old;");

        await ExecuteAsync(connection, "PRAGMA foreign_keys=ON;");
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
