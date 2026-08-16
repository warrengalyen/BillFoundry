using BillFoundry.Application.Invoices;
using Microsoft.AspNetCore.Components;

namespace BillFoundry.Web.Components.Pages;

public partial class InvoiceList
{
    private bool _loading = true;
    private bool _moreFiltersOpen;

    [SupplyParameterFromQuery(Name = "q")]
    public string? Search { get; set; }

    [SupplyParameterFromQuery]
    public Guid? ClientId { get; set; }

    [SupplyParameterFromQuery]
    public string? Status { get; set; }

    [SupplyParameterFromQuery]
    public DateOnly? IssueFrom { get; set; }

    [SupplyParameterFromQuery]
    public DateOnly? IssueTo { get; set; }

    [SupplyParameterFromQuery]
    public DateOnly? DueFrom { get; set; }

    [SupplyParameterFromQuery]
    public DateOnly? DueTo { get; set; }

    [SupplyParameterFromQuery]
    public decimal? MinTotal { get; set; }

    [SupplyParameterFromQuery]
    public decimal? MaxTotal { get; set; }

    [SupplyParameterFromQuery]
    public string? Sort { get; set; }

    [SupplyParameterFromQuery]
    public string? Dir { get; set; }

    [SupplyParameterFromQuery]
    public int? PageNumber { get; set; }

    [SupplyParameterFromQuery(Name = "pageSize")]
    public int? QueryPageSize { get; set; }

    private InvoiceListQuery Filters { get; set; } = new();

    private Guid SelectedClientId { get; set; }

    private IReadOnlyList<InvoiceClientOption> Clients { get; set; } = [];

    private PagedInvoiceResult<InvoiceListItemDto>? Page { get; set; }

    private string? ErrorMessage { get; set; }

    private bool HasFilters =>
        !string.IsNullOrWhiteSpace(Filters.Search)
        || Filters.ClientId is not null
        || Filters.Status != InvoiceStatusFilter.All
        || Filters.IssueFrom is not null
        || Filters.IssueTo is not null
        || Filters.DueFrom is not null
        || Filters.DueTo is not null
        || Filters.MinTotal is not null
        || Filters.MaxTotal is not null;

    private int AdvancedFilterCount
    {
        get
        {
            int count = 0;
            if (Filters.IssueFrom is not null) count++;
            if (Filters.IssueTo is not null) count++;
            if (Filters.DueFrom is not null) count++;
            if (Filters.DueTo is not null) count++;
            if (Filters.MinTotal is not null) count++;
            if (Filters.MaxTotal is not null) count++;
            return count;
        }
    }

    private void ToggleMoreFilters() => _moreFiltersOpen = !_moreFiltersOpen;

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
                return "0 invoices";
            }

            int start = ((Page.Page - 1) * Page.PageSize) + 1;
            int end = Math.Min(Page.Page * Page.PageSize, Page.TotalCount);
            return $"{start}–{end} of {Page.TotalCount} invoices";
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        Filters.Search = Search;
        Filters.ClientId = ClientId is Guid id && id != Guid.Empty ? id : null;
        SelectedClientId = Filters.ClientId ?? Guid.Empty;
        Filters.Status = ParseStatus(Status);
        Filters.IssueFrom = IssueFrom;
        Filters.IssueTo = IssueTo;
        Filters.DueFrom = DueFrom;
        Filters.DueTo = DueTo;
        Filters.MinTotal = MinTotal;
        Filters.MaxTotal = MaxTotal;
        Filters.SortBy = ParseSort(Sort);
        Filters.SortDescending = Dir is null
            ? Filters.SortBy is InvoiceSortField.IssueDate
                or InvoiceSortField.DueDate
                or InvoiceSortField.CreatedAt
                or InvoiceSortField.Total
                or InvoiceSortField.BalanceDue
            : string.Equals(Dir, "desc", StringComparison.OrdinalIgnoreCase);
        Filters.Page = PageNumber ?? 1;
        Filters.PageSize = QueryPageSize ?? InvoiceListQuery.DefaultPageSize;
        await LoadAsync();
    }

    private void ApplyFiltersAsync()
    {
        Filters.ClientId = SelectedClientId == Guid.Empty ? null : SelectedClientId;
        Filters.Page = 1;
        Navigate();
    }

    private void SortAsync(InvoiceSortField field)
    {
        if (Filters.SortBy == field)
        {
            Filters.SortDescending = !Filters.SortDescending;
        }
        else
        {
            Filters.SortBy = field;
            Filters.SortDescending = field is InvoiceSortField.IssueDate
                or InvoiceSortField.DueDate
                or InvoiceSortField.CreatedAt
                or InvoiceSortField.Total
                or InvoiceSortField.BalanceDue;
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

    private string SortIndicator(InvoiceSortField field)
    {
        if (Filters.SortBy != field)
        {
            return string.Empty;
        }

        return Filters.SortDescending ? "(descending)" : "(ascending)";
    }

    private string AriaSort(InvoiceSortField field)
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
            ["clientId"] = Filters.ClientId,
            ["status"] = Filters.Status == InvoiceStatusFilter.All ? null : Filters.Status.ToString().ToLowerInvariant(),
            ["issueFrom"] = Filters.IssueFrom,
            ["issueTo"] = Filters.IssueTo,
            ["dueFrom"] = Filters.DueFrom,
            ["dueTo"] = Filters.DueTo,
            ["minTotal"] = Filters.MinTotal,
            ["maxTotal"] = Filters.MaxTotal,
            ["sort"] = Filters.SortBy == InvoiceSortField.IssueDate ? null : Filters.SortBy.ToString().ToLowerInvariant(),
            ["dir"] = Filters.SortDescending ? "desc" : "asc",
            ["pageNumber"] = Filters.Page <= 1 ? null : Filters.Page,
            ["pageSize"] = Filters.PageSize == InvoiceListQuery.DefaultPageSize ? null : Filters.PageSize
        };

        if (Filters.SortBy == InvoiceSortField.IssueDate && Filters.SortDescending)
        {
            values["dir"] = null;
        }

        Navigation.NavigateTo(Navigation.GetUriWithQueryParameters(values));
    }

    private async Task LoadAsync()
    {
        _loading = true;
        ErrorMessage = null;
        InvoiceOptionsResult options = await Invoices.GetOptionsAsync();
        if (options.Succeeded && options.Options is not null)
        {
            Clients = options.Options.Clients;
        }

        InvoiceListResult result = await Invoices.ListAsync(Filters);
        _loading = false;

        if (result.IsForbidden || options.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (!result.Succeeded || result.Page is null)
        {
            ErrorMessage = result.Errors.Count > 0 ? result.Errors[0] : "Invoices could not be loaded.";
            Page = null;
            return;
        }

        Page = result.Page;
    }

    private static InvoiceStatusFilter ParseStatus(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out InvoiceStatusFilter status)
            ? status
            : InvoiceStatusFilter.All;

    private static InvoiceSortField ParseSort(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out InvoiceSortField sort)
            ? sort
            : InvoiceSortField.IssueDate;
}
