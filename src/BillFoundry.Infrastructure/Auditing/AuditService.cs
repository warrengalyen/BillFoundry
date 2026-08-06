using System.Text.Json;
using BillFoundry.Application.Auditing;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Auditing;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BillFoundry.Infrastructure.Auditing;

internal sealed class AuditService(
    BillFoundryDbContext dbContext,
    IAuthorizationService authorizationService,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IAuditService, IAuditRecorder
{
    private static readonly HashSet<string> ForbiddenMetadataKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "newpassword",
        "currentpassword",
        "confirmpassword",
        "oldpassword",
        "token",
        "accesstoken",
        "refreshtoken",
        "passwordresettoken",
        "secret",
        "connectionstring"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Record(AuditWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Guid? userId = request.ActorUserId ?? currentUser.UserId;
        string? userName = request.ActorUserName ?? currentUser.Email;
        dbContext.AuditEvents.Add(AuditEvent.Create(
            timeProvider.GetUtcNow(),
            userId,
            userName,
            request.Action,
            request.EntityType,
            request.EntityId,
            request.Description,
            SerializeMetadata(request.Metadata)));
    }

    public Task PersistAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<AuditQueryResult<AuditSearchResult>> SearchAsync(
        AuditSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!await IsAdministratorAsync().ConfigureAwait(false))
        {
            return AuditQueryResult<AuditSearchResult>.Forbidden();
        }

        query.Normalize();
        IQueryable<AuditEvent> events = ApplyFilters(dbContext.AuditEvents.AsNoTracking(), query);
        int total = await events.CountAsync(cancellationToken).ConfigureAwait(false);
        List<AuditEvent> page = await events
            .OrderByDescending(audit => audit.OccurredAtUtc)
            .ThenByDescending(audit => audit.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return AuditQueryResult<AuditSearchResult>.Success(new AuditSearchResult
        {
            Items = [.. page.Select(ToDto)],
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        });
    }

    public async Task<AuditQueryResult<IReadOnlyList<AuditEventDto>>> ListForEntityAsync(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        if (!await CanViewEntityAsync(entityType).ConfigureAwait(false))
        {
            return AuditQueryResult<IReadOnlyList<AuditEventDto>>.Forbidden();
        }

        List<AuditEvent> events = await dbContext.AuditEvents.AsNoTracking()
            .Where(audit => audit.EntityType == entityType && audit.EntityId == entityId)
            .OrderByDescending(audit => audit.OccurredAtUtc)
            .ThenByDescending(audit => audit.Id)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return AuditQueryResult<IReadOnlyList<AuditEventDto>>.Success([.. events.Select(ToDto)]);
    }

    public async Task<AuditQueryResult<IReadOnlyList<AuditActorOption>>> ListActorsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!await IsAdministratorAsync().ConfigureAwait(false))
        {
            return AuditQueryResult<IReadOnlyList<AuditActorOption>>.Forbidden();
        }

        var rows = await dbContext.AuditEvents.AsNoTracking()
            .Where(audit => audit.UserId != null)
            .Select(audit => new { audit.UserId, audit.UserName })
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AuditActorOption> actors = [.. rows
            .Select(row => new AuditActorOption
            {
                UserId = row.UserId!.Value,
                UserName = row.UserName ?? row.UserId.Value.ToString()
            })
            .OrderBy(actor => actor.UserName)];

        return AuditQueryResult<IReadOnlyList<AuditActorOption>>.Success(actors);
    }

    private static IQueryable<AuditEvent> ApplyFilters(IQueryable<AuditEvent> events, AuditSearchQuery query)
    {
        if (query.From is DateOnly from)
        {
            DateTimeOffset start = new(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            events = events.Where(audit => audit.OccurredAtUtc >= start);
        }

        if (query.To is DateOnly to)
        {
            DateTimeOffset end = new(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            events = events.Where(audit => audit.OccurredAtUtc < end);
        }

        if (query.UserId is Guid userId)
        {
            events = events.Where(audit => audit.UserId == userId);
        }

        if (query.Action is not null)
        {
            events = events.Where(audit => audit.Action == query.Action);
        }

        if (query.EntityType is not null)
        {
            events = events.Where(audit => audit.EntityType == query.EntityType);
        }

        if (query.EntityId is Guid entityId)
        {
            events = events.Where(audit => audit.EntityId == entityId);
        }

        if (query.Search is not null)
        {
            string search = query.Search;
            events = events.Where(audit =>
                audit.Description.Contains(search)
                || (audit.UserName != null && audit.UserName.Contains(search))
                || audit.Action.Contains(search));
        }

        return events;
    }

    private static AuditEventDto ToDto(AuditEvent audit) => new()
    {
        Id = audit.Id,
        OccurredAtUtc = audit.OccurredAtUtc,
        UserId = audit.UserId,
        UserName = audit.UserName,
        Action = audit.Action,
        ActionLabel = AuditActions.Label(audit.Action),
        EntityType = audit.EntityType,
        EntityTypeLabel = AuditEntityTypes.Label(audit.EntityType),
        EntityId = audit.EntityId,
        Description = audit.Description,
        Metadata = DeserializeMetadata(audit.MetadataJson)
    };

    private static string? SerializeMetadata(IReadOnlyDictionary<string, string?>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return null;
        }

        var safe = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string? value) in metadata)
        {
            if (string.IsNullOrWhiteSpace(key) || ForbiddenMetadataKeys.Contains(key))
            {
                continue;
            }

            safe[key] = value;
        }

        return safe.Count == 0 ? null : JsonSerializer.Serialize(safe, JsonOptions);
    }

    private static IReadOnlyDictionary<string, string?> DeserializeMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json, JsonOptions)
                ?? new Dictionary<string, string?>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string?>();
        }
    }

    private async Task<bool> IsAdministratorAsync()
    {
        AuthorizationResult authorization = await authorizationService
            .AuthorizeAsync(currentUser.Principal, AuthorizationPolicies.Administrator)
            .ConfigureAwait(false);
        return authorization.Succeeded;
    }

    private async Task<bool> CanViewEntityAsync(string entityType)
    {
        if (await IsAdministratorAsync().ConfigureAwait(false))
        {
            return true;
        }

        string? policy = entityType switch
        {
            AuditEntityTypes.Invoice => AuthorizationPolicies.ManageInvoices,
            AuditEntityTypes.Estimate => AuthorizationPolicies.ManageEstimates,
            AuditEntityTypes.Client => AuthorizationPolicies.ManageClients,
            AuditEntityTypes.CatalogItem => AuthorizationPolicies.ManageCatalog,
            _ => null
        };

        if (policy is null)
        {
            return false;
        }

        AuthorizationResult authorization = await authorizationService
            .AuthorizeAsync(currentUser.Principal, policy)
            .ConfigureAwait(false);
        return authorization.Succeeded;
    }
}
