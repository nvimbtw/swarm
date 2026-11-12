namespace Swarm.Server.Classi;

public class MachineCredential
{
    public long ID { get; set; }
    public long MachineID { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    public static string CreateTableSql()
    {
        return @"
            CREATE TABLE IF NOT EXISTS MachineCredential (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                MachineID INTEGER NOT NULL,
                TokenHash TEXT NOT NULL UNIQUE,
                IssuedAt TEXT NOT NULL,
                RevokedAt TEXT,
                LastUsedAt TEXT,

                FOREIGN KEY (MachineID) REFERENCES Machine(ID)
            );
        ";
    }

    public static string CreateIndexSql()
    {
        return @"
            CREATE UNIQUE INDEX IF NOT EXISTS IX_MachineCredential_Active
            ON MachineCredential(MachineID)
            WHERE RevokedAt IS NULL;
        ";
    }
}