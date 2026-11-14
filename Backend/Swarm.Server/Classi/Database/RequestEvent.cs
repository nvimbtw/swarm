namespace Swarm.Server.Classi;

public class RequestEvent
{
    public long ID { get; set; }
    public Guid RequestID { get; set; }

    public string Kind { get; set; } = "";
    public string SummaryMarkdown { get; set; } = "";
    public string? DiscordMessageID { get; set; }

    public string? Output { get; set; }
    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public static string CreateTableSql()
    {
        return @"
            CREATE TABLE IF NOT EXISTS RequestEvent (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                RequestID TEXT NOT NULL,
                Kind TEXT NOT NULL,
                SummaryMarkdown TEXT NOT NULL,
                DiscordMessageID TEXT,
                Output TEXT,
                Error TEXT,
                CreatedAt TEXT NOT NULL,

                FOREIGN KEY (RequestID) REFERENCES WorkRequest(ID)
            );
        ";
    }
}