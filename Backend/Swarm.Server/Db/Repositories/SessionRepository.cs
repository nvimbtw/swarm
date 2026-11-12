
using Swarm.Server.Classi;

namespace Swarm.Server.Db.Repositories;

public sealed class SessionRepository(DatabaseService database)
{
    public async Task RegisterSessionAsync(InstallationIdentity identity, SessionRegistration request,
        CancellationToken ct = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();

        long userId = await DatabaseQueries.EnsureUserAsync(connection, transaction, identity.OwnerId, ct);
        long machineId = await DatabaseQueries.EnsureMachineAsync(
            connection, transaction, userId, identity.MachineId, identity.MachineName, ct);
        long harnessId = await DatabaseQueries.EnsureHarnessAsync(connection, transaction, request.Harness, ct);

        long existing = await DatabaseQueries.ScalarLongAsync(connection, transaction,
            "SELECT COUNT(*) FROM Session WHERE ID=$id;", ct,
            ("$id", request.SessionId.ToString()));

        long matching = await DatabaseQueries.ScalarLongAsync(connection, transaction,
            "SELECT COUNT(*) FROM Session WHERE ID=$id AND OwnerUserID=$owner AND MachineID=$machine;",
            ct, ("$id", request.SessionId.ToString()), ("$owner", userId), ("$machine", machineId));

        if (existing > 0 && matching == 0)
            throw new InvalidOperationException("Session ID is already owned by another installation.");

        string now = DateTimeOffset.UtcNow.ToString("O");

        await DatabaseQueries.ExecuteAsync(connection, transaction, @"
            INSERT INTO Session (ID, OwnerUserID, MachineID, HarnessID, SessionName, ProjectId, Control,
                WorkspacePath, Mode, ConnectionState, ActivityState, ConnectedAt, LastSeenAt, DisconnectedAt)
            VALUES ($id, $owner, $machine, $harness, $name, $project, $control,
                '', $mode, 'connected', 'idle', $now, $now, NULL)
            ON CONFLICT(ID) DO UPDATE SET OwnerUserID=excluded.OwnerUserID, MachineID=excluded.MachineID,
                HarnessID=excluded.HarnessID, SessionName=excluded.SessionName, ProjectId=excluded.ProjectId,
                Control=excluded.Control, Mode=excluded.Mode, ConnectionState='connected',
                ConnectedAt=excluded.ConnectedAt, LastSeenAt=excluded.LastSeenAt, DisconnectedAt=NULL;",
            ct, ("$id", request.SessionId.ToString()), ("$owner", userId), ("$machine", machineId),
            ("$harness", harnessId), ("$name", request.Name), ("$project", request.ProjectId),
            ("$control", request.Control ? 1 : 0), ("$mode", request.Mode), ("$now", now));

        transaction.Commit();
    }

    public async Task SetSessionStateAsync(Guid sessionId, string state, CancellationToken ct = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(ct);

        string now = DateTimeOffset.UtcNow.ToString("O");

        await DatabaseQueries.ExecuteAsync(connection, null, @"
            UPDATE Session SET ConnectionState=$state, LastSeenAt=$now,
                DisconnectedAt=CASE WHEN $state='disconnected' THEN $now ELSE NULL END
            WHERE ID=$id;", ct, ("$state", state), ("$now", now), ("$id", sessionId.ToString()));
    }

    public async Task DisconnectAllSessionsAsync(CancellationToken ct = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(ct);

        string now = DateTimeOffset.UtcNow.ToString("O");

        await DatabaseQueries.ExecuteAsync(connection, null,
            "UPDATE Session SET ConnectionState='disconnected', DisconnectedAt=$now WHERE ConnectionState='connected';",
            ct, ("$now", now));
    }
}
