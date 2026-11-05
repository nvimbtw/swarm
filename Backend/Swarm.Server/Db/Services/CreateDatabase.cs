using Microsoft.Data.Sqlite;

namespace Swarm.Server.Db;

public class CreateDatabase
{
    private readonly DatabaseService database;

    public CreateDatabase(DatabaseService database)
    {
        this.database = database;
    }

    public async Task InitializeAsync()
    {
        using var connection = database.CreateConnection();

        await connection.OpenAsync();

        using var command = connection.CreateCommand();

        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                UserId TEXT PRIMARY KEY,
                Username TEXT NOT NULL,
                Roles INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );
        ";

        await command.ExecuteNonQueryAsync();
    }
}
