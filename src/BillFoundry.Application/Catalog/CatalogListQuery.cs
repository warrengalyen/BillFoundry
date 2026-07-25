namespace BillFoundry.Application.Catalog;

public enum CatalogSortField
{
    Name = 0,
    Sku = 1,
    UnitType = 2,
    UnitPrice = 3,
    CreatedAt = 4
}

public enum CatalogStatusFilter
{
    All = 0,
    Active = 1,
    Inactive = 2
}

public sealed class CatalogListQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public string? Search { get; set; }

    public CatalogStatusFilter Status { get; set; } = CatalogStatusFilter.Active;

    public CatalogUnitTypeFilter UnitType { get; set; } = CatalogUnitTypeFilter.All;

    public CatalogSortField SortBy { get; set; } = CatalogSortField.Name;

    public bool SortDescending { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    public void Normalize()
    {
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
        if (Search is { Length: > 200 })
        {
            Search = Search[..200];
        }

        if (!Enum.IsDefined(Status))
        {
            Status = CatalogStatusFilter.Active;
        }

        if (!Enum.IsDefined(UnitType))
        {
            UnitType = CatalogUnitTypeFilter.All;
        }

        if (!Enum.IsDefined(SortBy))
        {
            SortBy = CatalogSortField.Name;
        }

        if (Page < 1)
        {
            Page = 1;
        }

        if (PageSize < 1)
        {
            PageSize = DefaultPageSize;
        }
        else if (PageSize > MaxPageSize)
        {
            PageSize = MaxPageSize;
        }
    }
}

/// <summary>
/// List filter for catalog unit types. Values match <c>CatalogUnitType</c> plus All.
/// </summary>
public enum CatalogUnitTypeFilter
{
    All = -1,
    Hour = 0,
    Day = 1,
    Item = 2,
    FlatFee = 3
}

public sealed class PagedCatalogResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public required int TotalCount { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
