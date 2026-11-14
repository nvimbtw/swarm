namespace Swarm.Server.Classi;

public class Harness
{
    public long ID { get; set; }
    public string Name { get; set; } = "";

    public static string CreateTableSql()
    {
        return @"
            CREATE TABLE IF NOT EXISTS Harness (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE
            );
        ";
    }
}