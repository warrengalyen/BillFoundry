using BillFoundry.Domain.Auditing;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillFoundry.Infrastructure.Auditing;

internal static class AuditChangeTracker
{
    public static void DiscardPending(BillFoundryDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        foreach (var entry in dbContext.ChangeTracker.Entries<AuditEvent>()
            .Where(audit => audit.State == EntityState.Added)
            .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
