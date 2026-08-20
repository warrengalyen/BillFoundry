using Microsoft.EntityFrameworkCore;

namespace BillFoundry.Infrastructure.Persistence;

/// <summary>
/// EF Core context used to discover and apply the PostgreSQL migration set.
/// Runtime code still resolves <see cref="BillFoundryDbContext"/>.
/// </summary>
public sealed class BillFoundryPostgreSqlDbContext(DbContextOptions<BillFoundryPostgreSqlDbContext> options)
    : BillFoundryDbContext(options)
{
}
