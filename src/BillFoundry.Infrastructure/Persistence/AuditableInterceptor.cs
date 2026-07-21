using BillFoundry.Application.Security;
using BillFoundry.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BillFoundry.Infrastructure.Persistence;

internal sealed class AuditableInterceptor(ICurrentUser currentUser, TimeProvider timeProvider)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAudit(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid? userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.SetCreated(now, userId);
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.SetUpdated(now, userId);
            }
        }
    }
}
