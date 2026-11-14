namespace Swarm.Server.Classi;

public class WorkRequest
{
    public Guid ID { get; set; }
    public Guid TargetSessionID { get; set; }
    public long RequesterUserID { get; set; }
    public Guid? RequesterSessionID { get; set; }
    public Guid? ParentRequestID { get; set; }
    public long? ThreadID { get; set; }
    public long? WorkItemID { get; set; }
    public string Kind { get; set; } = "";
    public string DeliveryMode { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Model { get; set; }
    public string? Reasoning { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public static string CreateTableSql() => @"
        CREATE TABLE IF NOT EXISTS WorkRequest (
            ID TEXT PRIMARY KEY,
            TargetSessionID TEXT NOT NULL,
            RequesterUserID INTEGER NOT NULL,
            RequesterSessionID TEXT,
            ParentRequestID TEXT,
            ThreadID INTEGER,
            WorkItemID INTEGER,
            Kind TEXT NOT NULL,
            DeliveryMode TEXT NOT NULL,
            Prompt TEXT NOT NULL,
            Status TEXT NOT NULL,
            Model TEXT,
            Reasoning TEXT,
            Error TEXT,
            DeliveredAt TEXT,
            StartedAt TEXT,
            FinishedAt TEXT,
            FOREIGN KEY (TargetSessionID) REFERENCES Session(ID),
            FOREIGN KEY (RequesterUserID) REFERENCES ""User""(ID),
            FOREIGN KEY (RequesterSessionID) REFERENCES Session(ID),
            FOREIGN KEY (ParentRequestID) REFERENCES WorkRequest(ID)
        );";
}
