namespace BillFoundry.Domain.Auditing;

/// <summary>
/// An append-only business activity record. Distinct from <see cref="IAuditable"/>
/// row timestamps and from diagnostic application logs.
/// </summary>
public sealed class AuditEvent
{
    public const int UserNameMaxLength = 256;
    public const int ActionMaxLength = 64;
    public const int EntityTypeMaxLength = 64;
    public const int DescriptionMaxLength = 1000;

    private AuditEvent()
    {
        Action = string.Empty;
        EntityType = string.Empty;
        Description = string.Empty;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public Guid? UserId { get; private set; }

    public string? UserName { get; private set; }

    public string Action { get; private set; }

    public string EntityType { get; private set; }

    public Guid? EntityId { get; private set; }

    public string Description { get; private set; }

    public string? MetadataJson { get; private set; }

    public static AuditEvent Create(
        DateTimeOffset occurredAtUtc,
        Guid? userId,
        string? userName,
        string action,
        string entityType,
        Guid? entityId,
        string description,
        string? metadataJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (action.Length > ActionMaxLength)
        {
            throw new ArgumentException("The audit action is too long.", nameof(action));
        }

        if (entityType.Length > EntityTypeMaxLength)
        {
            throw new ArgumentException("The audit entity type is too long.", nameof(entityType));
        }

        string trimmedDescription = description.Trim();
        if (trimmedDescription.Length > DescriptionMaxLength)
        {
            trimmedDescription = trimmedDescription[..DescriptionMaxLength];
        }

        string? trimmedUser = string.IsNullOrWhiteSpace(userName) ? null : userName.Trim();
        if (trimmedUser is { Length: > UserNameMaxLength })
        {
            trimmedUser = trimmedUser[..UserNameMaxLength];
        }

        return new AuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = occurredAtUtc,
            UserId = userId,
            UserName = trimmedUser,
            Action = action.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId,
            Description = trimmedDescription,
            MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson
        };
    }
}
