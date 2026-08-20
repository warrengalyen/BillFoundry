using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BillFoundry.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for the SQL Server Community migration set.
/// </summary>
internal sealed class BillFoundryDbContextFactory : IDesignTimeDbContextFactory<BillFoundryDbContext>
{
    public BillFoundryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BillFoundryDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=BillFoundry;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
            .Options;
        return new BillFoundryDbContext(options);
    }
}
