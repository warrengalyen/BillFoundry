using Microsoft.EntityFrameworkCore;

namespace BillFoundry.Infrastructure.Persistence;

public sealed class BillFoundryDbContext(DbContextOptions<BillFoundryDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillFoundryDbContext).Assembly);
    }
}
