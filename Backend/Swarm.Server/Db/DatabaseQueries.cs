
using Microsoft.Data.Sqlite;

namespace Swarm.Server.Db;

internal static class DatabaseQueries
{
    public static async Task<long> EnsureUserAsync(SqliteConnection connection, SqliteTransaction transaction,
        string discordId, CancellationToken ct)
    {
        await ExecuteAsync(connection, transaction, @"
            INSERT INTO ""User"" (DiscordUserID, Name) VALUES ($id, $id)
            ON CONFLICT(DiscordUserID) DO NOTHING;",
            ct, ("$id", discordId));
    
        return await ScalarLongAsync(connection, transaction,
            @"SELECT ID FROM ""User"" WHERE DiscordUserID=$id;",
            ct, ("$id", discordId));
    }

    public static async Task<long> EnsureMachineAsync(SqliteConnection connection, SqliteTransaction transaction,
        long userId, string machineKey, string name, CancellationToken ct)
    {
        await ExecuteAsync(connection, transaction, @"
            INSERT INTO Machine (OwnerUserID, ClientMachineKey, Name)
            VALUES ($owner, $key, $name)
            ON CONFLICT(OwnerUserID, ClientMachineKey)
            DO UPDATE SET Name=excluded.Name;",
            ct, ("$owner", userId), ("$key", machineKey), ("$name", name));

        return await ScalarLongAsync(connection, transaction,
            "SELECT ID FROM Machine WHERE OwnerUserID=$owner AND ClientMachineKey=$key;",
            ct, ("$owner", userId), ("$key", machineKey));
    }

    public static async Task<long> EnsureHarnessAsync(SqliteConnection connection, SqliteTransaction transaction,
        string name, CancellationToken ct)
    {
        await ExecuteAsync(connection, transaction,
            "INSERT INTO Harness (Name) VALUES ($name) ON CONFLICT(Name) DO NOTHING;",
            ct, ("$name", name));

        return await ScalarLongAsync(connection, transaction,
            "SELECT ID FROM Harness WHERE Name=$name;",
            ct, ("$name", name));
    }

    public static async Task<long> ScalarLongAsync(SqliteConnection connection, SqliteTransaction transaction,
        string sql, CancellationToken ct, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        AddParameters(command, parameters);

        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    public static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction,
        string sql, CancellationToken ct, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        AddParameters(command, parameters);

        await command.ExecuteNonQueryAsync(ct);
    }

    private static void AddParameters(SqliteCommand command, (string Name, object? Value)[] parameters)
    {
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
    }
}
