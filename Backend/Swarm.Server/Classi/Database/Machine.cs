namespace Swarm.Server.Classi;

public class Machine
{
    public long ID { get; set; }
    public long OwnerUserID { get; set; }
    public string ClientMachineKey { get; set; } = "";
    public string Name { get; set; } = "";

    public static string CreateTableSql()
    {
        return @"
            CREATE TABLE IF NOT EXISTS Machine (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                OwnerUserID INTEGER NOT NULL,
                ClientMachineKey TEXT NOT NULL,
                Name TEXT NOT NULL,

                FOREIGN KEY (OwnerUserID) REFERENCES ""User""(ID),

                UNIQUE (OwnerUserID, ClientMachineKey)
            );
        ";
    }
}