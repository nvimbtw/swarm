using Microsoft.Extensions.FileProviders;
using Swarm.Server.Db;

sealed class TestDatabase : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "swarm-tests-" + Guid.NewGuid());
    public DatabaseService Database { get; }

    public TestDatabase()
    {
        Directory.CreateDirectory(root);
        Database = new DatabaseService(new TestEnvironment { ContentRootPath = root });
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Swarm.Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = "";
        public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
