using System.Net.Sockets;
using BillFoundry.Application.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BillFoundry.Infrastructure.Persistence;

internal sealed class DatabaseMigrationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedService
{
    private const int StartupRetryCount = 12;
    private static readonly TimeSpan StartupRetryDelay = TimeSpan.FromSeconds(3);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.ApplyMigrationsOnStartup)
        {
            return;
        }

        logger.LogInformation(
            "Database:ApplyMigrationsOnStartup is enabled. Applying pending EF Core migrations. The database will not be dropped.");

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        BillFoundryDbContext dbContext = scope.ServiceProvider.GetRequiredService<BillFoundryDbContext>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (Exception exception) when (attempt < StartupRetryCount && IsTransientStartupFailure(exception))
            {
                logger.LogWarning(
                    exception,
                    "The database is not reachable yet. Retrying migrations ({Attempt}/{MaxAttempts}).",
                    attempt,
                    StartupRetryCount);
                await Task.Delay(StartupRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        logger.LogInformation("Pending EF Core migrations were applied.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static bool IsTransientStartupFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException or TimeoutException)
            {
                return true;
            }

            if (current is NpgsqlException npgsql)
            {
                return npgsql.IsTransient || npgsql.SqlState is null;
            }
        }

        return false;
    }
}
