
using Microsoft.Data.Sqlite;

namespace Swarm.Server.Db;

public class CreateDatabase(DatabaseService database)
{
    public async Task InitializeAsync()
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();

        await DatabaseMigration.MigrateOldSchemaAsync(connection);

        await ExecuteAsync(connection, Swarm.Server.Classi.User.CreateTableSql());
        await ExecuteAsync(connection, Swarm.Server.Classi.Server.CreateTableSql());
        await ExecuteAsync(connection, Swarm.Server.Classi.Member.CreateTableSql());

        await ExecuteAsync(connection, Swarm.Server.Classi.Machine.CreateTableSql());
        await ExecuteAsync(connection, Swarm.Server.Classi.MachineCredential.CreateTableSql());
        await ExecuteAsync(connection, Swarm.Server.Classi.MachineCredential.CreateIndexSql());

        await ExecuteAsync(connection, Swarm.Server.Classi.Harness.CreateTableSql());
        await ExecuteAsync(connection, Swarm.Server.Classi.Session.CreateTableSql());

        await ExecuteAsync(connection, Swarm.Server.Classi.WorkRequest.CreateTableSql());
        await ExecuteAsync(connection, Swarm.Server.Classi.RequestEvent.CreateTableSql());
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
