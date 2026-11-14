namespace Swarm.Server.Classi;

public class User
{
    public long ID { get; set; }
    public string DiscordUserID { get; set; } = "";
    public string Name { get; set; } = "";

    public static string CreateTableSql()
    {
        return @"
            CREATE TABLE IF NOT EXISTS ""User"" (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                DiscordUserID TEXT NOT NULL UNIQUE,
                Name TEXT NOT NULL
            );
        ";
    }
}