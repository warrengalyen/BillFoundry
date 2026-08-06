namespace BillFoundry.Application.Auditing;

/// <summary>
/// Reads business audit events. The administrator log requires the
/// <c>Administrator</c> policy. Entity timelines require the policy used to
/// view that entity.
/// </summary>
public interface IAuditService
{
    Task<AuditQueryResult<AuditSearchResult>> SearchAsync(
        AuditSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<AuditQueryResult<IReadOnlyList<AuditEventDto>>> ListForEntityAsync(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);

    Task<AuditQueryResult<IReadOnlyList<AuditActorOption>>> ListActorsAsync(
        CancellationToken cancellationToken = default);
}
