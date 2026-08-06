namespace BillFoundry.Application.Auditing;

public sealed class AuditWriteRequest
{
    public required string Action { get; init; }

    public required string EntityType { get; init; }

    public Guid? EntityId { get; init; }

    public required string Description { get; init; }

    public IReadOnlyDictionary<string, string?>? Metadata { get; init; }

    public Guid? ActorUserId { get; init; }

    public string? ActorUserName { get; init; }
}

public sealed class AuditSearchQuery
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public Guid? UserId { get; set; }

    public string? Action { get; set; }

    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    public void Normalize()
    {
        if (UserId == Guid.Empty)
        {
            UserId = null;
        }

        if (EntityId == Guid.Empty)
        {
            EntityId = null;
        }

        Action = string.IsNullOrWhiteSpace(Action) ? null : Action.Trim();
        EntityType = string.IsNullOrWhiteSpace(EntityType) ? null : EntityType.Trim();
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();

        if (From is DateOnly from && from == default)
        {
            From = null;
        }

        if (To is DateOnly to && to == default)
        {
            To = null;
        }

        if (From is DateOnly start && To is DateOnly end && end < start)
        {
            To = start;
        }

        if (Page < 1)
        {
            Page = 1;
        }

        if (PageSize < 1)
        {
            PageSize = DefaultPageSize;
        }

        if (PageSize > MaxPageSize)
        {
            PageSize = MaxPageSize;
        }
    }
}

public sealed class AuditEventDto
{
    public required Guid Id { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public Guid? UserId { get; init; }

    public string? UserName { get; init; }

    public required string Action { get; init; }

    public required string ActionLabel { get; init; }

    public required string EntityType { get; init; }

    public required string EntityTypeLabel { get; init; }

    public Guid? EntityId { get; init; }

    public required string Description { get; init; }

    public IReadOnlyDictionary<string, string?> Metadata { get; init; } =
        new Dictionary<string, string?>();
}

public sealed class AuditSearchResult
{
    public required IReadOnlyList<AuditEventDto> Items { get; init; }

    public required int TotalCount { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }
}

public sealed class AuditActorOption
{
    public required Guid UserId { get; init; }

    public required string UserName { get; init; }
}

public sealed class AuditQueryResult<T>
{
    public bool Succeeded { get; private init; }

    public bool IsForbidden { get; private init; }

    public T? Value { get; private init; }

    public static AuditQueryResult<T> Success(T value) => new()
    {
        Succeeded = true,
        Value = value
    };

    public static AuditQueryResult<T> Forbidden() => new()
    {
        IsForbidden = true
    };
}
