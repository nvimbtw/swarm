namespace Swarm.Server.Classi;

public class Member
{
    public long ID { get; set; }
    public long UserID { get; set; }
    public long ServerID { get; set; }
    public string DisplayName { get; set; } = "";
    public DateTimeOffset? LeftAt { get; set; }

    public static string CreateTableSql()
    {
        return @"
            CREATE TABLE IF NOT EXISTS Member (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                UserID INTEGER NOT NULL,
                ServerID INTEGER NOT NULL,
                DisplayName TEXT NOT NULL,
                LeftAt TEXT,

                FOREIGN KEY (UserID) REFERENCES ""User""(ID),
                FOREIGN KEY (ServerID) REFERENCES Server(ID),

                UNIQUE (UserID, ServerID)
            );
        ";
    }
}