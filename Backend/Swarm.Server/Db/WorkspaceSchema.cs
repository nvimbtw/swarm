using Microsoft.Data.Sqlite;

namespace Swarm.Server.Db;

/// <summary>Additive schema for project work. Discord references stay null until publishing is implemented.</summary>
public static class WorkspaceSchema
{
    public static async Task InitializeAsync(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        await DatabaseQueries.ExecuteAsync(connection, transaction, Sql, default);
        await AddColumn("Server", "Name", "TEXT NOT NULL DEFAULT ''");
        await AddColumn("Session", "WorkspaceProjectID", "INTEGER REFERENCES Project(ID)");
        await AddColumn("Session", "OwnerMemberID", "INTEGER REFERENCES Member(ID)");
        await AddColumn("WorkRequest", "RequesterMemberID", "INTEGER REFERENCES Member(ID)");
        await AddColumn("WorkRequest", "CreatedAt", "TEXT NOT NULL DEFAULT ''");
        transaction.Commit();

        async Task AddColumn(string table, string column, string definition)
        {
            using var check = connection.CreateCommand();
            check.Transaction = transaction;
            check.CommandText = $"PRAGMA table_info({table});";
            bool exists = false;
            await using (var reader = await check.ExecuteReaderAsync())
                while (await reader.ReadAsync()) exists |= reader.GetString(1) == column;
            if (!exists)
                await DatabaseQueries.ExecuteAsync(connection, transaction,
                    $"ALTER TABLE {table} ADD COLUMN {column} {definition};", default);
        }
    }

    public const string Sql = """
        CREATE TABLE IF NOT EXISTS Project (
            ID INTEGER PRIMARY KEY, ServerID INTEGER NOT NULL REFERENCES Server(ID),
            Key TEXT NOT NULL UNIQUE, DiscordCategoryID TEXT UNIQUE, Name TEXT NOT NULL, RepositoryURL TEXT
        );
        CREATE TABLE IF NOT EXISTS Channel (
            ID INTEGER PRIMARY KEY, ProjectID INTEGER NOT NULL REFERENCES Project(ID),
            DiscordChannelID TEXT UNIQUE, Name TEXT NOT NULL, Type TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS Thread (
            ID INTEGER PRIMARY KEY, ChannelID INTEGER NOT NULL REFERENCES Channel(ID),
            DiscordThreadID TEXT UNIQUE, Title TEXT NOT NULL, CreatedAt TEXT NOT NULL, DeletedAt TEXT
        );
        CREATE TABLE IF NOT EXISTS Initiative (
            ID INTEGER PRIMARY KEY, ProjectID INTEGER NOT NULL REFERENCES Project(ID),
            ThreadID INTEGER UNIQUE REFERENCES Thread(ID), Title TEXT NOT NULL, BriefMarkdown TEXT NOT NULL,
            CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, DeletedAt TEXT,
            Version INTEGER NOT NULL DEFAULT 1
        );
        CREATE TABLE IF NOT EXISTS WorkItem (
            ID INTEGER PRIMARY KEY, ProjectID INTEGER NOT NULL REFERENCES Project(ID),
            InitiativeID INTEGER REFERENCES Initiative(ID), ThreadID INTEGER UNIQUE REFERENCES Thread(ID),
            OwnerMemberID INTEGER REFERENCES Member(ID), Type TEXT NOT NULL CHECK(Type IN ('issue','feature')),
            Status TEXT NOT NULL CHECK(Status IN ('open','in-progress','verification','closed')),
            Title TEXT NOT NULL, BriefMarkdown TEXT NOT NULL, Priority INTEGER NOT NULL DEFAULT 0,
            CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, DeletedAt TEXT, Version INTEGER NOT NULL DEFAULT 1
        );
        CREATE TABLE IF NOT EXISTS WorkItemLink (
            FromItemID INTEGER NOT NULL REFERENCES WorkItem(ID), ToItemID INTEGER NOT NULL REFERENCES WorkItem(ID),
            Kind TEXT NOT NULL, PRIMARY KEY(FromItemID, ToItemID, Kind), CHECK(FromItemID <> ToItemID)
        );
        CREATE TABLE IF NOT EXISTS WorkItemEvent (
            ID INTEGER PRIMARY KEY, WorkItemID INTEGER NOT NULL REFERENCES WorkItem(ID),
            ActorMemberID INTEGER NOT NULL REFERENCES Member(ID), ActorSessionID TEXT REFERENCES Session(ID),
            Kind TEXT NOT NULL, OldStatus TEXT, NewStatus TEXT, Summary TEXT NOT NULL,
            ApprovalMemberID INTEGER REFERENCES Member(ID), ApprovalReference TEXT, CreatedAt TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS Document (
            ID INTEGER PRIMARY KEY, ProjectID INTEGER NOT NULL REFERENCES Project(ID),
            ThreadID INTEGER UNIQUE REFERENCES Thread(ID), Title TEXT NOT NULL, CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL, DeletedAt TEXT, Version INTEGER NOT NULL DEFAULT 1
        );
        CREATE TABLE IF NOT EXISTS DocumentRevision (
            DocumentID INTEGER NOT NULL REFERENCES Document(ID), RevisionNo INTEGER NOT NULL,
            Markdown TEXT NOT NULL, AuthorMemberID INTEGER NOT NULL REFERENCES Member(ID),
            AuthorSessionID TEXT REFERENCES Session(ID), PublishState TEXT NOT NULL DEFAULT 'pending',
            CreatedAt TEXT NOT NULL, PRIMARY KEY(DocumentID, RevisionNo)
        );
        CREATE TABLE IF NOT EXISTS DocumentMessage (
            DocumentID INTEGER NOT NULL, RevisionNo INTEGER NOT NULL, PartIndex INTEGER NOT NULL,
            DiscordMessageID TEXT NOT NULL, PublishedAt TEXT NOT NULL,
            PRIMARY KEY(DocumentID, RevisionNo, PartIndex),
            FOREIGN KEY(DocumentID, RevisionNo) REFERENCES DocumentRevision(DocumentID, RevisionNo)
        );
        CREATE TABLE IF NOT EXISTS Question (
            ID INTEGER PRIMARY KEY, RequestID TEXT NOT NULL REFERENCES WorkRequest(ID),
            ThreadID INTEGER NOT NULL REFERENCES Thread(ID), DiscordMessageID TEXT UNIQUE,
            Text TEXT NOT NULL, State TEXT NOT NULL, AnsweredByMemberID INTEGER REFERENCES Member(ID),
            AnswerReference TEXT, CreatedAt TEXT NOT NULL, AnsweredAt TEXT
        );
        CREATE INDEX IF NOT EXISTS IX_WorkItem_Project ON WorkItem(ProjectID, DeletedAt, Status, ID);
        CREATE INDEX IF NOT EXISTS IX_Initiative_Project ON Initiative(ProjectID, DeletedAt, ID);
        CREATE INDEX IF NOT EXISTS IX_Document_Project ON Document(ProjectID, DeletedAt, ID);
        CREATE VIEW IF NOT EXISTS WorkItemOverview AS
        SELECT w.*, i.Title AS InitiativeTitle FROM WorkItem w
        LEFT JOIN Initiative i ON i.ID=w.InitiativeID AND i.DeletedAt IS NULL;
        """;
}
