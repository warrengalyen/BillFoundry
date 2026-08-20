using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BillFoundry.Infrastructure.Persistence;

/// <summary>
/// Assigns a new <c>byte[]</c> concurrency token on insert and update when the
/// store is PostgreSQL. SQL Server continues to use <c>rowversion</c>.
/// </summary>
internal sealed class PostgreSqlRowVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var property = entry.Properties.FirstOrDefault(candidate =>
                candidate.Metadata.Name == "RowVersion"
                && candidate.Metadata.ClrType == typeof(byte[])
                && candidate.Metadata.IsConcurrencyToken);
            if (property is null)
            {
                continue;
            }

            property.CurrentValue = Guid.NewGuid().ToByteArray();
        }
    }
}
