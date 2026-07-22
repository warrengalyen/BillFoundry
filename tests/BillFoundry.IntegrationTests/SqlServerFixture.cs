using BillFoundry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillFoundry.IntegrationTests;

public sealed class SqlServerFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public string DatabaseName { get; private set; } = string.Empty;

    public string LogoRoot { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        DatabaseName = $"BillFoundry_IT_{Guid.NewGuid():N}";
        LogoRoot = Path.Combine(Path.GetTempPath(), "billfoundry-logos", DatabaseName);
        ConnectionString =
            $@"Server=(localdb)\mssqllocaldb;Database={DatabaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Connect Timeout=5";

        var options = new DbContextOptionsBuilder<BillFoundryDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        await using var db = new BillFoundryDbContext(options);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            var options = new DbContextOptionsBuilder<BillFoundryDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            await using var db = new BillFoundryDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
        catch (Exception)
        {
            // LocalDB may already be unavailable during cleanup.
        }

        if (Directory.Exists(LogoRoot))
        {
            Directory.Delete(LogoRoot, recursive: true);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
