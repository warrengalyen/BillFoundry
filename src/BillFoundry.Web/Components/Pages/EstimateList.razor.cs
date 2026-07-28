using BillFoundry.Application.Estimates;
using Microsoft.AspNetCore.Components;

namespace BillFoundry.Web.Components.Pages;

public partial class EstimateList
{
    private bool _loading = true;

    [SupplyParameterFromQuery(Name = "q")]
    public string? Search { get; set; }

    [SupplyParameterFromQuery]
    public string? Status { get; set; }

    [SupplyParameterFromQuery]
    public string? Sort { get; set; }

    [SupplyParameterFromQuery]
    public string? Dir { get; set; }

    [SupplyParameterFromQuery]
    public int? PageNumber { get; set; }

    [SupplyParameterFromQuery(Name = "pageSize")]
    public int? QueryPageSize { get; set; }

    private EstimateListQuery Filters { get; set; } = new();

    private PagedEstimateResult<EstimateListItemDto>? Page { get; set; }

    private string? ErrorMessage { get; set; }

    private bool HasFilters =>
        !string.IsNullOrWhiteSpace(Filters.Search)
        || Filters.Status != EstimateStatusFilter.All;

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
                return "0 estimates";
            }

            int start = ((Page.Page - 1) * Page.PageSize) + 1;
            int end = Math.Min(Page.Page * Page.PageSize, Page.TotalCount);
            return $"{start}–{end} of {Page.TotalCount} estimates";
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        Filters.Search = Search;
        Filters.Status = ParseStatus(Status);
        Filters.SortBy = ParseSort(Sort);
        Filters.SortDescending = Dir is null
            ? Filters.SortBy is EstimateSortField.IssueDate or EstimateSortField.CreatedAt or EstimateSortField.Total
            : string.Equals(Dir, "desc", StringComparison.OrdinalIgnoreCase);
        Filters.Page = PageNumber ?? 1;
        Filters.PageSize = QueryPageSize ?? EstimateListQuery.DefaultPageSize;
        await LoadAsync();
    }

    private void ApplyFiltersAsync()
    {
        Filters.Page = 1;
        Navigate();
    }

    private void SortAsync(EstimateSortField field)
    {
        if (Filters.SortBy == field)
        {
            Filters.SortDescending = !Filters.SortDescending;
        }
        else
        {
            Filters.SortBy = field;
            Filters.SortDescending = field is EstimateSortField.IssueDate
                or EstimateSortField.CreatedAt
                or EstimateSortField.Total;
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

    private string SortIndicator(EstimateSortField field)
    {
        if (Filters.SortBy != field)
        {
            return string.Empty;
        }

        return Filters.SortDescending ? "(descending)" : "(ascending)";
    }

    private string AriaSort(EstimateSortField field)
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
            ["status"] = Filters.Status == EstimateStatusFilter.All ? null : Filters.Status.ToString().ToLowerInvariant(),
            ["sort"] = Filters.SortBy == EstimateSortField.IssueDate ? null : Filters.SortBy.ToString().ToLowerInvariant(),
            ["dir"] = Filters.SortDescending ? "desc" : "asc",
            ["pageNumber"] = Filters.Page <= 1 ? null : Filters.Page,
            ["pageSize"] = Filters.PageSize == EstimateListQuery.DefaultPageSize ? null : Filters.PageSize
        };

        if (Filters.SortBy == EstimateSortField.IssueDate && Filters.SortDescending)
        {
            values["dir"] = null;
        }

        Navigation.NavigateTo(Navigation.GetUriWithQueryParameters(values));
    }

    private async Task LoadAsync()
    {
        _loading = true;
        ErrorMessage = null;
        EstimateListResult result = await Estimates.ListAsync(Filters);
        _loading = false;

        if (result.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (!result.Succeeded || result.Page is null)
        {
            ErrorMessage = result.Errors.Count > 0 ? result.Errors[0] : "Estimates could not be loaded.";
            Page = null;
            return;
        }

        Page = result.Page;
    }

    private static EstimateStatusFilter ParseStatus(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out EstimateStatusFilter status)
            ? status
            : EstimateStatusFilter.All;

    private static EstimateSortField ParseSort(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out EstimateSortField sort)
            ? sort
            : EstimateSortField.IssueDate;
}
