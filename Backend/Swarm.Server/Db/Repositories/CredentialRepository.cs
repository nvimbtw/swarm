
using Microsoft.Data.Sqlite;
using Swarm.Server.Classi;

namespace Swarm.Server.Db.Repositories;

public sealed class CredentialRepository(DatabaseService database)
{
    public async Task<InstallationIdentity> IssueCredentialAsync(string ownerId, string machineId,
        string machineName, string tokenHash, CancellationToken ct = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();

        long userId = await DatabaseQueries.EnsureUserAsync(connection, transaction, ownerId, ct);
        long machineDbId = await DatabaseQueries.EnsureMachineAsync(
            connection, transaction, userId, machineId, machineName, ct);

        string now = DateTimeOffset.UtcNow.ToString("O");

        await DatabaseQueries.ExecuteAsync(connection, transaction,
            "UPDATE MachineCredential SET RevokedAt=$now WHERE MachineID=$machine AND RevokedAt IS NULL;",
            ct, ("$now", now), ("$machine", machineDbId));

        await DatabaseQueries.ExecuteAsync(connection, transaction,
            "INSERT INTO MachineCredential (MachineID, TokenHash, IssuedAt) VALUES ($machine, $hash, $now);",
            ct, ("$machine", machineDbId), ("$hash", tokenHash), ("$now", now));

        transaction.Commit();
        return new(ownerId, machineId, machineName);
    }

    public async Task<InstallationIdentity?> AuthenticateAsync(string tokenHash, CancellationToken ct = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT u.DiscordUserID, m.ClientMachineKey, m.Name
            FROM MachineCredential c
            JOIN Machine m ON m.ID = c.MachineID
            JOIN ""User"" u ON u.ID = m.OwnerUserID
            WHERE c.TokenHash = $hash AND c.RevokedAt IS NULL;";

        command.Parameters.AddWithValue("$hash", tokenHash);

        await using var reader = await command.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return null;

        return new(reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }
}
