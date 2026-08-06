using System.Globalization;
using BillFoundry.Application.Auditing;

namespace BillFoundry.Infrastructure.Auditing;

internal static class AuditWrites
{
    public static AuditWriteRequest Event(
        string action,
        string entityType,
        Guid? entityId,
        string description,
        IReadOnlyDictionary<string, string?>? metadata = null) =>
        new()
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            Metadata = metadata
        };

    public static string Money(decimal amount, string currency) =>
        string.Create(CultureInfo.InvariantCulture, $"{amount:0.00} {currency}");
}
