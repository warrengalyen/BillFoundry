namespace BillFoundry.Application.Catalog;

/// <summary>
/// Creates, updates, and lists billable catalog items. Mutations require the
/// <c>ManageCatalog</c> policy. Items are deactivated rather than deleted.
/// </summary>
public interface ICatalogService
{
    Task<CatalogListResult> ListAsync(CatalogListQuery query, CancellationToken cancellationToken = default);

    Task<CatalogItemResult> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CatalogItemResult> CreateAsync(SaveCatalogItemCommand command, CancellationToken cancellationToken = default);

    Task<CatalogItemResult> UpdateAsync(UpdateCatalogItemCommand command, CancellationToken cancellationToken = default);

    Task<CatalogItemResult> ActivateAsync(CatalogConcurrencyCommand command, CancellationToken cancellationToken = default);

    Task<CatalogItemResult> DeactivateAsync(CatalogConcurrencyCommand command, CancellationToken cancellationToken = default);
}
