using BillFoundry.Application.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BillFoundry.Infrastructure.Persistence;

internal sealed class DatabaseMigrationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedService
{
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
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Pending EF Core migrations were applied.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
