using BillFoundry.Application.Auditing;
using BillFoundry.Application.Catalog;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Organizations;
using BillFoundry.Infrastructure.Auditing;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BillFoundry.Infrastructure.Catalog;

internal sealed class CatalogService(
    BillFoundryDbContext dbContext,
    IAuthorizationService authorizationService,
    ICurrentUser currentUser,
    IAuditRecorder auditRecorder) : ICatalogService
{
    public async Task<CatalogListResult> ListAsync(CatalogListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return CatalogListResult.Forbidden();
        }

        query.Normalize();
        IQueryable<CatalogItem> items = dbContext.CatalogItems.AsNoTracking();

        items = query.Status switch
        {
            CatalogStatusFilter.Active => items.Where(item => item.IsActive),
            CatalogStatusFilter.Inactive => items.Where(item => !item.IsActive),
            _ => items
        };

        if (query.UnitType is not CatalogUnitTypeFilter.All)
        {
            var unitType = (CatalogUnitType)query.UnitType;
            items = items.Where(item => item.UnitType == unitType);
        }

        if (query.Search is not null)
        {
            string search = query.Search;
            items = items.Where(item =>
                item.Name.Contains(search)
                || (item.Sku != null && item.Sku.Contains(search))
                || (item.Description != null && item.Description.Contains(search)));
        }

        int totalCount = await items.CountAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<CatalogItem> sorted = query.SortBy switch
        {
            CatalogSortField.Sku when query.SortDescending => items.OrderByDescending(item => item.Sku).ThenBy(item => item.Name),
            CatalogSortField.Sku => items.OrderBy(item => item.Sku).ThenBy(item => item.Name),
            CatalogSortField.UnitType when query.SortDescending => items.OrderByDescending(item => item.UnitType).ThenBy(item => item.Name),
            CatalogSortField.UnitType => items.OrderBy(item => item.UnitType).ThenBy(item => item.Name),
            CatalogSortField.UnitPrice when query.SortDescending => items.OrderByDescending(item => item.DefaultUnitPrice).ThenBy(item => item.Name),
            CatalogSortField.UnitPrice => items.OrderBy(item => item.DefaultUnitPrice).ThenBy(item => item.Name),
            CatalogSortField.CreatedAt when query.SortDescending => items.OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.Name),
            CatalogSortField.CreatedAt => items.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Name),
            _ when query.SortDescending => items.OrderByDescending(item => item.Name).ThenBy(item => item.Sku),
            _ => items.OrderBy(item => item.Name).ThenBy(item => item.Sku)
        };

        List<CatalogListItemDto> pageItems = await sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new CatalogListItemDto
            {
                Id = item.Id,
                Name = item.Name,
                Sku = item.Sku,
                UnitType = item.UnitType,
                UnitTypeLabel = item.UnitType == CatalogUnitType.Hour ? "Hour"
                    : item.UnitType == CatalogUnitType.Day ? "Day"
                    : item.UnitType == CatalogUnitType.Item ? "Item"
                    : "Flat fee",
                DefaultUnitPrice = item.DefaultUnitPrice,
                IsTaxable = item.IsTaxable,
                IsActive = item.IsActive,
                CreatedAtUtc = item.CreatedAtUtc
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string currency = await GetCurrencyCodeAsync(cancellationToken).ConfigureAwait(false);
        return CatalogListResult.Success(
            new PagedCatalogResult<CatalogListItemDto>
            {
                Items = pageItems,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            },
            currency);
    }

    public async Task<CatalogItemResult> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return CatalogItemResult.Forbidden();
        }

        CatalogItem? item = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        return item is null
            ? CatalogItemResult.NotFound()
            : CatalogItemResult.Success(await ToDetailsAsync(item, cancellationToken).ConfigureAwait(false));
    }

    public async Task<CatalogItemResult> CreateAsync(SaveCatalogItemCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return CatalogItemResult.Forbidden();
        }

        IReadOnlyList<string> errors = CatalogItemValidator.Validate(command, requireRowVersion: false);
        if (errors.Count > 0)
        {
            return CatalogItemResult.Invalid(errors);
        }

        string? sku = NormalizeSku(command.Sku);
        if (sku is not null
            && await dbContext.CatalogItems.AnyAsync(item => item.Sku == sku, cancellationToken).ConfigureAwait(false))
        {
            return CatalogItemResult.Invalid(["A catalog item with this SKU already exists."]);
        }

        var item = CatalogItem.Create(
            command.Name,
            command.Description,
            sku,
            command.UnitType,
            command.DefaultUnitPrice,
            command.IsTaxable);

        dbContext.CatalogItems.Add(item);
        auditRecorder.Record(AuditWrites.Event(
            AuditActions.CatalogItemCreated,
            AuditEntityTypes.CatalogItem,
            item.Id,
            $"Created service item {item.Name}."));
        return await SaveAsync(item, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CatalogItemResult> UpdateAsync(UpdateCatalogItemCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return CatalogItemResult.Forbidden();
        }

        CatalogItem? item = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return CatalogItemResult.NotFound();
        }

        IReadOnlyList<string> errors = CatalogItemValidator.Validate(command, requireRowVersion: true);
        if (errors.Count > 0)
        {
            return CatalogItemResult.Invalid(errors, await ToDetailsAsync(item, cancellationToken).ConfigureAwait(false));
        }

        string? sku = NormalizeSku(command.Sku);
        if (sku is not null
            && await dbContext.CatalogItems.AnyAsync(
                existing => existing.Id != item.Id && existing.Sku == sku,
                cancellationToken).ConfigureAwait(false))
        {
            return CatalogItemResult.Invalid(
                ["A catalog item with this SKU already exists."],
                await ToDetailsAsync(item, cancellationToken).ConfigureAwait(false));
        }

        ApplyRowVersion(item, command.RowVersion);
        item.Update(command.Name, command.Description, sku, command.UnitType, command.DefaultUnitPrice, command.IsTaxable);
        auditRecorder.Record(AuditWrites.Event(
            AuditActions.CatalogItemUpdated,
            AuditEntityTypes.CatalogItem,
            item.Id,
            $"Updated service item {item.Name}."));
        return await SaveAsync(item, cancellationToken).ConfigureAwait(false);
    }

    public Task<CatalogItemResult> ActivateAsync(CatalogConcurrencyCommand command, CancellationToken cancellationToken = default) =>
        SetActiveAsync(command, active: true, cancellationToken);

    public Task<CatalogItemResult> DeactivateAsync(CatalogConcurrencyCommand command, CancellationToken cancellationToken = default) =>
        SetActiveAsync(command, active: false, cancellationToken);

    private async Task<CatalogItemResult> SetActiveAsync(
        CatalogConcurrencyCommand command,
        bool active,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return CatalogItemResult.Forbidden();
        }

        CatalogItem? item = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return CatalogItemResult.NotFound();
        }

        if (command.RowVersion is not { Length: > 0 })
        {
            return CatalogItemResult.Invalid(
                ["The catalog item version is missing. Reload the page and try again."],
                await ToDetailsAsync(item, cancellationToken).ConfigureAwait(false));
        }

        ApplyRowVersion(item, command.RowVersion);
        if (active)
        {
            item.Activate();
        }
        else
        {
            item.Deactivate();
        }

        auditRecorder.Record(AuditWrites.Event(
            active ? AuditActions.CatalogItemActivated : AuditActions.CatalogItemDeactivated,
            AuditEntityTypes.CatalogItem,
            item.Id,
            active
                ? $"Activated service item {item.Name}."
                : $"Deactivated service item {item.Name}."));
        return await SaveAsync(item, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsForbiddenAsync()
    {
        AuthorizationResult authorization = await authorizationService
            .AuthorizeAsync(currentUser.Principal, AuthorizationPolicies.ManageCatalog)
            .ConfigureAwait(false);
        return !authorization.Succeeded;
    }

    private Task<CatalogItem?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.CatalogItems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    private async Task<string> GetCurrencyCodeAsync(CancellationToken cancellationToken)
    {
        Organization? organization = await dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == Organization.SingletonId, cancellationToken)
            .ConfigureAwait(false);
        return organization?.DefaultCurrency.Value ?? CurrencyCode.Usd.Value;
    }

    private async Task<CatalogItemDetailsDto> ToDetailsAsync(CatalogItem item, CancellationToken cancellationToken)
    {
        byte[] rowVersion = dbContext.Entry(item).Property(entity => entity.RowVersion).CurrentValue
            ?? item.RowVersion;
        string currency = await GetCurrencyCodeAsync(cancellationToken).ConfigureAwait(false);
        return CatalogItemDetailsDto.From(item, currency, rowVersion);
    }

    private void ApplyRowVersion(CatalogItem item, byte[] rowVersion)
    {
        dbContext.Entry(item).Property(entity => entity.RowVersion).OriginalValue = rowVersion;
    }

    private async Task<CatalogItemResult> SaveAsync(CatalogItem item, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return CatalogItemResult.Success(await ToDetailsAsync(item, cancellationToken).ConfigureAwait(false));
        }
        catch (DbUpdateConcurrencyException)
        {
            AuditChangeTracker.DiscardPending(dbContext);
            dbContext.ChangeTracker.Clear();
            CatalogItem? current = await LoadAsync(item.Id, cancellationToken).ConfigureAwait(false);
            return current is null
                ? CatalogItemResult.NotFound()
                : CatalogItemResult.ConcurrencyConflict(await ToDetailsAsync(current, cancellationToken).ConfigureAwait(false));
        }
        catch (DbUpdateException exception) when (UniqueConstraint.IsViolation(exception))
        {
            AuditChangeTracker.DiscardPending(dbContext);
            return CatalogItemResult.Invalid(
                ["A catalog item with this SKU already exists."],
                await ToDetailsAsync(item, cancellationToken).ConfigureAwait(false));
        }
    }

    private static string? NormalizeSku(string? sku) =>
        string.IsNullOrWhiteSpace(sku) ? null : CatalogSku.Parse(sku).Value;
}
