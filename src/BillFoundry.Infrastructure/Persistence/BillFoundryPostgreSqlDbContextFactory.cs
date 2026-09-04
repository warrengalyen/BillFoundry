using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BillFoundry.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for the PostgreSQL Community migration set.
/// </summary>
internal sealed class BillFoundryPostgreSqlDbContextFactory
    : IDesignTimeDbContextFactory<BillFoundryPostgreSqlDbContext>
{
    public BillFoundryPostgreSqlDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BillFoundryPostgreSqlDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=5433;Database=billfoundry;Username=billfoundry;Password=unused")
            .Options;
        return new BillFoundryPostgreSqlDbContext(options);
    }
}
