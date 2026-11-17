namespace Swarm.Server.Db.Repositories;

/// <summary>Trusted server configuration provisions projects and guild membership, never MCP arguments.</summary>
public sealed class ProjectProvisioning(DatabaseService database)
{
    public async Task ApplyAsync(IConfiguration configuration)
    {
        foreach (var project in configuration.GetSection("Workspace:Projects").GetChildren())
        {
            string key = project["Key"] ?? throw new InvalidOperationException("Project Key is required.");
            string guild = project["GuildId"] ?? throw new InvalidOperationException("Project GuildId is required.");
            string name = project["Name"] ?? key;
            if (string.IsNullOrWhiteSpace(key) || key.Length > 100 || string.IsNullOrWhiteSpace(guild))
                throw new InvalidOperationException("Invalid project configuration.");
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            async Task Execute(string sql, params (string Name, object? Value)[] args) =>
                await DatabaseQueries.ExecuteAsync(connection, transaction, sql, default, args);
            async Task<long> Scalar(string sql, params (string Name, object? Value)[] args) =>
                await DatabaseQueries.ScalarLongAsync(connection, transaction, sql, default, args);
            await Execute("INSERT INTO Server (DiscordGuildID, Name) VALUES ($id, $name) ON CONFLICT(DiscordGuildID) DO NOTHING;",
                ("$id", guild), ("$name", project["ServerName"] ?? guild));
            long serverId = await Scalar("SELECT ID FROM Server WHERE DiscordGuildID=$id;", ("$id", guild));
            await Execute("INSERT INTO Project (ServerID, Key, Name) VALUES ($server, $key, $name) ON CONFLICT(Key) DO NOTHING;",
                ("$server", serverId), ("$key", key), ("$name", name));
            if (await Scalar("SELECT ServerID FROM Project WHERE Key=$key;", ("$key", key)) != serverId)
                throw new InvalidOperationException("Project key already belongs to a different server.");
            foreach (var member in project.GetSection("MemberDiscordIds").GetChildren())
            {
                string owner = member.Value ?? throw new InvalidOperationException("Member ID is required.");
                long userId = await DatabaseQueries.EnsureUserAsync(connection, transaction, owner, default);
                await Execute("""
                    INSERT INTO Member (UserID, ServerID, DisplayName) VALUES ($user, $server, $name)
                    ON CONFLICT(UserID, ServerID) DO UPDATE SET LeftAt=NULL;
                    """, ("$user", userId), ("$server", serverId), ("$name", owner));
            }
            // TODO: Show provisioned projects in Discord; persist category/channel IDs from the Discord setup flow.
            transaction.Commit();
        }
    }
}
