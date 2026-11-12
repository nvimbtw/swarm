namespace Swarm.Server.Classi;

public class Server
{
    public long ID { get; set; }
    public string DiscordGuildID { get; set; } = "";

    public static string CreateTableSql()
    {
        return @"
            CREATE TABLE IF NOT EXISTS Server (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                DiscordGuildID TEXT NOT NULL UNIQUE
            );
        ";
    }
}