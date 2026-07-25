namespace BillFoundry.Application.Catalog;

public sealed class CatalogItemResult
{
    public bool Succeeded { get; private init; }

    public bool IsForbidden { get; private init; }

    public bool IsNotFound { get; private init; }

    public bool IsConcurrencyConflict { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public CatalogItemDetailsDto? Item { get; private init; }

    public static CatalogItemResult Success(CatalogItemDetailsDto item) =>
        new()
        {
            Succeeded = true,
            Item = item
        };

    public static CatalogItemResult Forbidden() =>
        new()
        {
            IsForbidden = true,
            Errors = ["You are not allowed to manage the service catalog."]
        };

    public static CatalogItemResult NotFound() =>
        new()
        {
            IsNotFound = true,
            Errors = ["The catalog item was not found."]
        };

    public static CatalogItemResult Invalid(IReadOnlyList<string> errors, CatalogItemDetailsDto? item = null) =>
        new()
        {
            Errors = errors,
            Item = item
        };

    public static CatalogItemResult ConcurrencyConflict(CatalogItemDetailsDto item) =>
        new()
        {
            IsConcurrencyConflict = true,
            Item = item,
            Errors = ["The catalog item was updated by another user. Review the current values and save again."]
        };
}

public sealed class CatalogListResult
{
    public bool Succeeded { get; private init; }

    public bool IsForbidden { get; private init; }

    public PagedCatalogResult<CatalogListItemDto>? Page { get; private init; }

    public string CurrencyCode { get; private init; } = string.Empty;

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public static CatalogListResult Success(PagedCatalogResult<CatalogListItemDto> page, string currencyCode) =>
        new()
        {
            Succeeded = true,
            Page = page,
            CurrencyCode = currencyCode
        };

    public static CatalogListResult Forbidden() =>
        new()
        {
            IsForbidden = true,
            Errors = ["You are not allowed to manage the service catalog."]
        };
}
