using BillFoundry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BillFoundry.IntegrationTests;

/// <summary>
/// Isolated PostgreSQL database for Community persistence tests. Skips when a
/// server is not reachable (Windows CI uses LocalDB and does not start Postgres).
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public const string DefaultAdminConnectionString =
        "Host=127.0.0.1;Port=5432;Database=postgres;Username=billfoundry;Password=DevOnly_P@ssw0rd";

    public bool IsAvailable { get; private set; }

    public string ConnectionString { get; private set; } = string.Empty;

    public string DatabaseName { get; private set; } = string.Empty;

    public string LogoRoot { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        DatabaseName = $"billfoundry_it_{Guid.NewGuid():N}";
        LogoRoot = Path.Combine(Path.GetTempPath(), "billfoundry-logos-pg", DatabaseName);
        string adminConnection = Environment.GetEnvironmentVariable("BILLFOUNDRY_TEST_POSTGRES")
            ?? DefaultAdminConnectionString;

        try
        {
            await using (var admin = new NpgsqlConnection(adminConnection))
            {
                await admin.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{DatabaseName}\"", admin);
                await create.ExecuteNonQueryAsync();
            }

            ConnectionString = new NpgsqlConnectionStringBuilder(adminConnection)
            {
                Database = DatabaseName
            }.ConnectionString;

            var options = new DbContextOptionsBuilder<BillFoundryPostgreSqlDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;
            await using var db = new BillFoundryPostgreSqlDbContext(options);
            await db.Database.MigrateAsync();
            IsAvailable = true;
        }
        catch (Exception)
        {
            IsAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (!IsAvailable)
        {
            TryDeleteLogoRoot();
            return;
        }

        try
        {
            string adminConnection = Environment.GetEnvironmentVariable("BILLFOUNDRY_TEST_POSTGRES")
                ?? DefaultAdminConnectionString;
            await using var admin = new NpgsqlConnection(adminConnection);
            await admin.OpenAsync();
            await using (var terminate = new NpgsqlCommand(
                $"""
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = '{DatabaseName}' AND pid <> pg_backend_pid()
                """,
                admin))
            {
                await terminate.ExecuteNonQueryAsync();
            }

            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{DatabaseName}\"", admin);
            await drop.ExecuteNonQueryAsync();
        }
        catch (Exception)
        {
            // Best-effort cleanup when the server is already gone.
        }

        TryDeleteLogoRoot();
    }

    public void RequireOrSkip()
    {
        if (IsAvailable)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BILLFOUNDRY_TEST_POSTGRES")))
        {
            throw new InvalidOperationException(
                "BILLFOUNDRY_TEST_POSTGRES is set but PostgreSQL did not accept a connection.");
        }
    }

    public bool ShouldRun => IsAvailable;

    private void TryDeleteLogoRoot()
    {
        if (Directory.Exists(LogoRoot))
        {
            Directory.Delete(LogoRoot, recursive: true);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSql";
}
