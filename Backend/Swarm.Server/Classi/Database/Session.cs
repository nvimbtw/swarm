namespace Swarm.Server.Classi;

public class Session
{
    public Guid ID { get; set; }
    public long OwnerUserID { get; set; }
    public long MachineID { get; set; }
    public long HarnessID { get; set; }
    public string SessionName { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public bool Control { get; set; }
    public string? ExternalThreadID { get; set; }
    public string WorkspacePath { get; set; } = "";
    public string Mode { get; set; } = "";
    public string ConnectionState { get; set; } = "";
    public string ActivityState { get; set; } = "";
    public string? ActivitySummary { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset? DisconnectedAt { get; set; }

    public static string CreateTableSql() => @"
        CREATE TABLE IF NOT EXISTS Session (
            ID TEXT PRIMARY KEY,
            OwnerUserID INTEGER NOT NULL,
            MachineID INTEGER NOT NULL,
            HarnessID INTEGER NOT NULL,
            SessionName TEXT NOT NULL,
            ProjectId TEXT NOT NULL,
            Control INTEGER NOT NULL DEFAULT 0,
            ExternalThreadID TEXT,
            WorkspacePath TEXT NOT NULL,
            Mode TEXT NOT NULL,
            ConnectionState TEXT NOT NULL,
            ActivityState TEXT NOT NULL,
            ActivitySummary TEXT,
            ConnectedAt TEXT,
            LastSeenAt TEXT,
            DisconnectedAt TEXT,
            FOREIGN KEY (OwnerUserID) REFERENCES ""User""(ID),
            FOREIGN KEY (MachineID) REFERENCES Machine(ID),
            FOREIGN KEY (HarnessID) REFERENCES Harness(ID)
        );";
}
