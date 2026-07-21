namespace BillFoundry.Domain.Auditing;

public interface IAuditable
{
    DateTimeOffset CreatedAtUtc { get; }
    DateTimeOffset? UpdatedAtUtc { get; }
    Guid? CreatedByUserId { get; }
    Guid? UpdatedByUserId { get; }

    void SetCreated(DateTimeOffset atUtc, Guid? byUserId);

    void SetUpdated(DateTimeOffset atUtc, Guid? byUserId);
}
