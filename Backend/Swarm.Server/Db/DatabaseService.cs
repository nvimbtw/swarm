
using Microsoft.Data.Sqlite;

namespace Swarm.Server.Db;

public class DatabaseService
{
    private readonly string connectionString;

    public DatabaseService(IWebHostEnvironment environment)
    {
        string folder = Path.Combine(
            environment.ContentRootPath,
            "Data"
        );

        Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, "swarm.db");

        connectionString = $"Data Source={path}";
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(connectionString);
    }
}
