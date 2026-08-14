using BillFoundry.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BillFoundry.Infrastructure.Demo;

internal sealed class DemoSeedHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<DemoSeedOptions> options,
    ILogger<DemoSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        DemoSeedOptions seed = options.Value;
        if (!seed.Enabled)
        {
            return;
        }

        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            DemoSeeder seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
            await seeder.SeedAsync(seed, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Demo seed was skipped because the database was unavailable or seed failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
