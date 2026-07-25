using BillFoundry.Application.Catalog;
using Microsoft.AspNetCore.Components;

namespace BillFoundry.Web.Components.Pages;

public partial class CatalogList
{
    private bool _loading = true;

    [SupplyParameterFromQuery(Name = "q")]
    public string? Search { get; set; }

    [SupplyParameterFromQuery]
    public string? Status { get; set; }

    [SupplyParameterFromQuery]
    public string? Unit { get; set; }

    [SupplyParameterFromQuery]
    public string? Sort { get; set; }

    [SupplyParameterFromQuery]
    public string? Dir { get; set; }

    [SupplyParameterFromQuery]
    public int? PageNumber { get; set; }

    [SupplyParameterFromQuery(Name = "pageSize")]
    public int? QueryPageSize { get; set; }

    private CatalogListQuery Filters { get; set; } = new();

    private PagedCatalogResult<CatalogListItemDto>? Page { get; set; }

    private string CurrencyCode { get; set; } = string.Empty;

    private string? ErrorMessage { get; set; }

    private bool HasFilters =>
        !string.IsNullOrWhiteSpace(Filters.Search)
        || Filters.Status != CatalogStatusFilter.Active
        || Filters.UnitType != CatalogUnitTypeFilter.All;

    private string ResultsSummary
    {
        get
        {
            if (_loading || Page is null)
            {
                return "Loading results.";
            }

            if (Page.TotalCount == 0)
            {
                return "0 items";
            }

            int start = ((Page.Page - 1) * Page.PageSize) + 1;
            int end = Math.Min(Page.Page * Page.PageSize, Page.TotalCount);
            return $"{start}–{end} of {Page.TotalCount} items";
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        Filters.Search = Search;
        Filters.Status = ParseStatus(Status);
        Filters.UnitType = ParseUnit(Unit);
        Filters.SortBy = ParseSort(Sort);
        Filters.SortDescending = string.Equals(Dir, "desc", StringComparison.OrdinalIgnoreCase);
        Filters.Page = PageNumber ?? 1;
        Filters.PageSize = QueryPageSize ?? CatalogListQuery.DefaultPageSize;
        await LoadAsync();
    }

    private void ApplyFiltersAsync()
    {
        Filters.Page = 1;
        Navigate();
    }

    private void SortAsync(CatalogSortField field)
    {
        if (Filters.SortBy == field)
        {
            Filters.SortDescending = !Filters.SortDescending;
        }
        else
        {
            Filters.SortBy = field;
            Filters.SortDescending = field is CatalogSortField.CreatedAt or CatalogSortField.UnitPrice;
        }

        Filters.Page = 1;
        Navigate();
    }

    private void PreviousPageAsync()
    {
        Filters.Page = Math.Max(1, Filters.Page - 1);
        Navigate();
    }

    private void NextPageAsync()
    {
        Filters.Page++;
        Navigate();
    }

    private string SortIndicator(CatalogSortField field)
    {
        if (Filters.SortBy != field)
        {
            return string.Empty;
        }

        return Filters.SortDescending ? "(descending)" : "(ascending)";
    }

    private string AriaSort(CatalogSortField field)
    {
        if (Filters.SortBy != field)
        {
            return "none";
        }

        return Filters.SortDescending ? "descending" : "ascending";
    }

    private void Navigate()
    {
        var values = new Dictionary<string, object?>
        {
            ["q"] = string.IsNullOrWhiteSpace(Filters.Search) ? null : Filters.Search,
            ["status"] = Filters.Status == CatalogStatusFilter.Active ? null : Filters.Status.ToString().ToLowerInvariant(),
            ["unit"] = Filters.UnitType == CatalogUnitTypeFilter.All ? null : Filters.UnitType.ToString().ToLowerInvariant(),
            ["sort"] = Filters.SortBy == CatalogSortField.Name ? null : Filters.SortBy.ToString().ToLowerInvariant(),
            ["dir"] = Filters.SortDescending ? "desc" : null,
            ["pageNumber"] = Filters.Page <= 1 ? null : Filters.Page,
            ["pageSize"] = Filters.PageSize == CatalogListQuery.DefaultPageSize ? null : Filters.PageSize
        };

        Navigation.NavigateTo(Navigation.GetUriWithQueryParameters(values));
    }

    private async Task LoadAsync()
    {
        _loading = true;
        ErrorMessage = null;
        CatalogListResult result = await Catalog.ListAsync(Filters);
        _loading = false;

        if (result.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (!result.Succeeded || result.Page is null)
        {
            ErrorMessage = result.Errors.Count > 0 ? result.Errors[0] : "The catalog could not be loaded.";
            Page = null;
            return;
        }

        Page = result.Page;
        CurrencyCode = result.CurrencyCode;
    }

    private static CatalogStatusFilter ParseStatus(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out CatalogStatusFilter status)
            ? status
            : CatalogStatusFilter.Active;

    private static CatalogUnitTypeFilter ParseUnit(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out CatalogUnitTypeFilter unit)
            ? unit
            : CatalogUnitTypeFilter.All;

    private static CatalogSortField ParseSort(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out CatalogSortField sort)
            ? sort
            : CatalogSortField.Name;
}
