using System.Text.Json;
using Microsoft.Data.Sqlite;
using Swarm.Server.Classi;
using Swarm.Server.Connections;
using Swarm.Server.Db;
using Swarm.Server.Db.Repositories;
using Swarm.Server.Workspace;

static class WorkspaceChecks
{
    public static async Task RunAsync()
    {
        using var fixture = new TestDatabase();
        var database = fixture.Database;
        await SeedLegacyDatabaseAsync(database);
        var initializer = new CreateDatabase(database);
        await initializer.InitializeAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Workspace:Projects:0:Key"] = "alpha", ["Workspace:Projects:0:GuildId"] = "1",
            ["Workspace:Projects:0:MemberDiscordIds:0"] = "owner",
            ["Workspace:Projects:0:MemberDiscordIds:1"] = "colleague",
            ["Workspace:Projects:1:Key"] = "beta", ["Workspace:Projects:1:GuildId"] = "2",
            ["Workspace:Projects:1:MemberDiscordIds:0"] = "other"
        }).Build();
        await new ProjectProvisioning(database).ApplyAsync(configuration);
        var registry = new SessionRegistry(new SessionRepository(database));
        var connectionId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await registry.RegisterAsync(connectionId, new("owner", "machine", "Laptop"),
            new(sessionId, "Agent", "alpha", "codex", "solo"));
        var service = new WorkspaceService(registry, new WorkspaceRepository(database));
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        async Task<JsonElement> Call(string entity, string action, WorkspaceRequest request) =>
            JsonSerializer.SerializeToElement(await service.ExecuteAsync(connectionId,
                $"workspace.{entity}.{action}", request, default), jsonOptions);
        async Task Reject(string code, Func<Task> operation)
        {
            try { await operation(); throw new Exception("Expected rejection: " + code); }
            catch (WorkspaceException error) when (error.Message == code) { }
        }
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
        async Task<long> Scalar(string sql)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        Check(await Scalar("SELECT COUNT(*) FROM Session WHERE ID='legacy-session';") == 1,
            "Schema upgrade preserves existing sessions");
        Check(await Scalar("SELECT COUNT(*) FROM WorkRequest WHERE ID='legacy-request';") == 1,
            "Schema upgrade preserves existing request history");
        Check(await Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';") == 20,
            "All nineteen diagram tables and MachineCredential exist");
        var epic = (await Call("initiative", "create", new(sessionId, Title: "Agent collaboration", BriefMarkdown: "Large goal"))).GetProperty("item");
        long epicId = epic.GetProperty("id").GetInt64();
        var feature = (await Call("work_item", "create", new(sessionId, Title: "Search 100%_ literally",
            Type: "feature", InitiativeId: epicId))).GetProperty("item");
        long featureId = feature.GetProperty("id").GetInt64();
        Check(feature.GetProperty("initiativeTitle").GetString() == "Agent collaboration", "JOIN reads the epic title");
        var issue = (await Call("work_item", "create", new(sessionId, Title: "Fix search", Type: "issue", InitiativeId: epicId))).GetProperty("item");
        long issueId = issue.GetProperty("id").GetInt64();
        var children = await Call("work_item", "list", new(sessionId, InitiativeId: epicId, Status: "open", Limit: 1));
        Check(children.GetProperty("total").GetInt64() == 2 && children.GetProperty("items").GetArrayLength() == 1,
            "An epic contains peer issues/features and lists are bounded");
        long cursor = children.GetProperty("nextAfterId").GetInt64();
        var secondPage = await Call("work_item", "list", new(sessionId, InitiativeId: epicId, Limit: 1, AfterId: cursor));
        Check(secondPage.GetProperty("items")[0].GetProperty("id").GetInt64() == issueId &&
            secondPage.GetProperty("nextAfterId").ValueKind == JsonValueKind.Null, "Stable pagination has no duplicate items");
        var literal = await Call("work_item", "list", new(sessionId, Query: "%_"));
        Check(literal.GetProperty("total").GetInt64() == 1, "Search escapes SQL LIKE wildcards");
        var empty = await Call("work_item", "list", new(sessionId, Query: "' OR 1=1 --"));
        Check(empty.GetProperty("items").GetArrayLength() == 0, "Search parameters remain data");
        await Reject("initiative_has_work_items", () => Call("initiative", "delete", new(sessionId, Id: epicId, ExpectedVersion: 1)));
        await Call("initiative", "update", new(sessionId, Id: epicId, Title: "Bigger goal", ExpectedVersion: 1));
        await Reject("version_conflict_read_again", () => Call("initiative", "update", new(sessionId, Id: epicId, Title: "Stale", ExpectedVersion: 1)));
        await Reject("invalid_workspace_request", () => Call("work_item", "create", new(sessionId, Title: "bad", Type: "epic")));
        await Reject("invalid_workspace_request", () => Call("document", "create", new(sessionId, Title: "   ")));
        await Reject("initiative_unavailable_in_project", () => Call("work_item", "create", new(sessionId, Title: "bad", Type: "issue", InitiativeId: 999999)));

        var doc = (await Call("document", "create", new(sessionId, Title: "Design", Markdown: "First revision"))).GetProperty("item");
        long documentId = doc.GetProperty("id").GetInt64();
        await Call("document", "update", new(sessionId, Id: documentId, Markdown: "Second revision", ExpectedVersion: 1));
        doc = (await Call("document", "get", new(sessionId, Id: documentId))).GetProperty("item");
        Check(doc.GetProperty("version").GetInt32() == 2 && doc.GetProperty("markdown").GetString() == "Second revision",
            "Document reads return the current immutable revision");
        Check(await Scalar($"SELECT COUNT(*) FROM DocumentRevision WHERE DocumentID={documentId} AND PublishState='pending';") == 2,
            "Both revisions are retained for future Discord publishing");
        await Reject("version_conflict_read_again", () => Call("document", "update", new(sessionId, Id: documentId, Markdown: "stale", ExpectedVersion: 1)));
        Check(await Scalar("SELECT COUNT(*) FROM DocumentRevision;") == 2, "Failed edits do not insert a revision");

        await Reject("session_unavailable_or_not_allowed", () => service.ExecuteAsync(Guid.NewGuid(),
            "workspace.document.get", new(sessionId, Id: documentId), default));
        var outsiderId = Guid.NewGuid();
        await registry.RegisterAsync(connectionId, new("outsider", "machine", "Laptop"),
            new(outsiderId, "Outsider", "alpha", "codex", "solo"));
        await Reject("project_unavailable_or_not_allowed", () => Call("initiative", "list", new(outsiderId)));
        var betaId = Guid.NewGuid();
        await registry.RegisterAsync(connectionId, new("other", "machine", "Laptop"),
            new(betaId, "Other", "beta", "codex", "solo"));
        long foreignEpic = (await Call("initiative", "create", new(betaId, Title: "Foreign"))).GetProperty("item").GetProperty("id").GetInt64();
        await Reject("item_not_found", () => Call("initiative", "get", new(sessionId, Id: foreignEpic)));
        await Reject("initiative_unavailable_in_project", () => Call("work_item", "update",
            new(sessionId, Id: featureId, InitiativeId: foreignEpic, ExpectedVersion: 1)));

        await Call("work_item", "update", new(sessionId, Id: featureId, Status: "verification", ClearInitiative: true, ExpectedVersion: 1));
        var standalone = (await Call("work_item", "get", new(sessionId, Id: featureId))).GetProperty("item");
        Check(standalone.GetProperty("initiativeTitle").ValueKind == JsonValueKind.Null, "LEFT JOIN preserves standalone work");
        await Call("work_item", "delete", new(sessionId, Id: issueId, ExpectedVersion: 1));
        await Call("initiative", "delete", new(sessionId, Id: epicId, ExpectedVersion: 2));
        await Call("document", "delete", new(sessionId, Id: documentId, ExpectedVersion: 2));
        await Reject("item_not_found", () => Call("document", "get", new(sessionId, Id: documentId)));
        Check(await Scalar("SELECT COUNT(*) FROM DocumentRevision;") == 2, "Soft deletion preserves document history");
        Check(await Scalar("SELECT COUNT(*) FROM WorkItemEvent;") == 4, "Work-item creation, edit and delete are attributed events");
        Check(await Scalar("SELECT COUNT(*) FROM Thread;") == 0, "CRUD does not fabricate Discord threads");

        // A new repository connection reads stored rows, independent of previously returned local lists.
        var repository = new WorkspaceRepository(database);
        var local = (WorkspacePage)await repository.ExecuteAsync(registry.FindConnected(connectionId, sessionId)!,
            "workspace.work_item.list", new(sessionId), default);
        int originalCount = local.Items.Count;
        ((List<WorkspaceItem>)local.Items).Clear();
        var reloaded = (WorkspacePage)await repository.ExecuteAsync(registry.FindConnected(connectionId, sessionId)!,
            "workspace.work_item.list", new(sessionId), default);
        Check(originalCount == 1 && local.Items.Count == 0 && reloaded.Items.Count == 1, "Clear affects the disconnected list, not the database");
        await initializer.InitializeAsync();
        Check(await Scalar("SELECT COUNT(*) FROM WorkItem;") == 2, "Repeated startup preserves data");
        Check(await Scalar("SELECT COUNT(*) FROM pragma_foreign_key_check;") == 0, "No broken foreign keys");
        Console.WriteLine("Workspace schema, hierarchy, CRUD, revision, pagination and authorization checks passed.");
    }

    private static async Task SeedLegacyDatabaseAsync(DatabaseService database)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        string[] definitions =
        {
            User.CreateTableSql(),
            "CREATE TABLE Server (ID INTEGER PRIMARY KEY, DiscordGuildID TEXT NOT NULL UNIQUE);",
            Member.CreateTableSql(), Machine.CreateTableSql(), MachineCredential.CreateTableSql(),
            Harness.CreateTableSql(), Session.CreateTableSql(), WorkRequest.CreateTableSql(), RequestEvent.CreateTableSql()
        };
        foreach (string sql in definitions)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
        using var seed = connection.CreateCommand();
        seed.CommandText = """
            INSERT INTO "User" (ID, DiscordUserID, Name) VALUES (999, 'legacy-user', 'Legacy');
            INSERT INTO Machine (ID, OwnerUserID, ClientMachineKey, Name) VALUES (999, 999, 'old', 'Old');
            INSERT INTO Harness (ID, Name) VALUES (999, 'legacy');
            INSERT INTO Session (ID, OwnerUserID, MachineID, HarnessID, SessionName, ProjectId, Control,
                WorkspacePath, Mode, ConnectionState, ActivityState)
            VALUES ('legacy-session', 999, 999, 999, 'Old session', 'old-label', 0, '', 'solo', 'disconnected', 'idle');
            INSERT INTO WorkRequest (ID, TargetSessionID, RequesterUserID, Kind, DeliveryMode, Prompt, Status)
            VALUES ('legacy-request', 'legacy-session', 999, 'agent.submit', 'queue', 'Old prompt', 'completed');
            """;
        await seed.ExecuteNonQueryAsync();
    }

}
