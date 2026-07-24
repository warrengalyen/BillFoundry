using BillFoundry.Application.Clients;
using Microsoft.AspNetCore.Components;

namespace BillFoundry.Web.Components.Pages;

public partial class ClientList
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

    private ClientListQuery Filters { get; set; } = new();

    private PagedResult<ClientListItemDto>? Page { get; set; }

    private string? ErrorMessage { get; set; }

    private bool HasFilters =>
        !string.IsNullOrWhiteSpace(Filters.Search) || Filters.Status != ClientStatusFilter.Active;

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
                return "0 clients";
            }

            int start = ((Page.Page - 1) * Page.PageSize) + 1;
            int end = Math.Min(Page.Page * Page.PageSize, Page.TotalCount);
            return $"{start}–{end} of {Page.TotalCount} clients";
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        Filters.Search = Search;
        Filters.Status = ParseStatus(Status);
        Filters.SortBy = ParseSort(Sort);
        Filters.SortDescending = string.Equals(Dir, "desc", StringComparison.OrdinalIgnoreCase);
        Filters.Page = PageNumber ?? 1;
        Filters.PageSize = QueryPageSize ?? ClientListQuery.DefaultPageSize;
        await LoadAsync();
    }

    private void ApplyFiltersAsync()
    {
        Filters.Page = 1;
        Navigate();
    }

    private void SortAsync(ClientSortField field)
    {
        if (Filters.SortBy == field)
        {
            Filters.SortDescending = !Filters.SortDescending;
        }
        else
        {
            Filters.SortBy = field;
            Filters.SortDescending = field is ClientSortField.CreatedAt;
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

    private string SortIndicator(ClientSortField field)
    {
        if (Filters.SortBy != field)
        {
            return string.Empty;
        }

        return Filters.SortDescending ? "(descending)" : "(ascending)";
    }

    private string AriaSort(ClientSortField field)
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
            ["status"] = Filters.Status == ClientStatusFilter.Active ? null : Filters.Status.ToString().ToLowerInvariant(),
            ["sort"] = Filters.SortBy == ClientSortField.Name ? null : Filters.SortBy.ToString().ToLowerInvariant(),
            ["dir"] = Filters.SortDescending ? "desc" : null,
            ["pageNumber"] = Filters.Page <= 1 ? null : Filters.Page,
            ["pageSize"] = Filters.PageSize == ClientListQuery.DefaultPageSize ? null : Filters.PageSize
        };

        Navigation.NavigateTo(Navigation.GetUriWithQueryParameters(values));
    }

    private async Task LoadAsync()
    {
        _loading = true;
        ErrorMessage = null;
        ClientListResult result = await Clients.ListAsync(Filters);
        _loading = false;

        if (result.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (!result.Succeeded || result.Page is null)
        {
            ErrorMessage = result.Errors.Count > 0 ? result.Errors[0] : "The client list could not be loaded.";
            Page = null;
            return;
        }

        Page = result.Page;
    }

    private static ClientStatusFilter ParseStatus(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out ClientStatusFilter status)
            ? status
            : ClientStatusFilter.Active;

    private static ClientSortField ParseSort(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out ClientSortField sort)
            ? sort
            : ClientSortField.Name;
}
