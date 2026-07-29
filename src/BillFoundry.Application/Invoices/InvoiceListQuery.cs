using BillFoundry.Domain.Invoices;

namespace BillFoundry.Application.Invoices;

public enum InvoiceSortField
{
    IssueDate = 0,
    Number = 1,
    Client = 2,
    DueDate = 3,
    Total = 4,
    BalanceDue = 5,
    Status = 6,
    CreatedAt = 7
}

public enum InvoiceStatusFilter
{
    All = -1,
    Draft = 0,
    Sent = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Overdue = 4,
    Void = 5
}

public sealed class InvoiceListQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public string? Search { get; set; }

    public Guid? ClientId { get; set; }

    public InvoiceStatusFilter Status { get; set; } = InvoiceStatusFilter.All;

    public DateOnly? IssueFrom { get; set; }

    public DateOnly? IssueTo { get; set; }

    public DateOnly? DueFrom { get; set; }

    public DateOnly? DueTo { get; set; }

    public decimal? MinTotal { get; set; }

    public decimal? MaxTotal { get; set; }

    public bool OverdueOnly { get; set; }

    public InvoiceSortField SortBy { get; set; } = InvoiceSortField.IssueDate;

    public bool SortDescending { get; set; } = true;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    public void Normalize()
    {
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
        if (Search is { Length: > 200 })
        {
            Search = Search[..200];
        }

        if (ClientId == Guid.Empty)
        {
            ClientId = null;
        }

        if (IssueFrom is DateOnly issueFrom && issueFrom == default)
        {
            IssueFrom = null;
        }

        if (IssueTo is DateOnly issueTo && issueTo == default)
        {
            IssueTo = null;
        }

        if (DueFrom is DateOnly dueFromValue && dueFromValue == default)
        {
            DueFrom = null;
        }

        if (DueTo is DateOnly dueToValue && dueToValue == default)
        {
            DueTo = null;
        }

        if (!Enum.IsDefined(Status))
        {
            Status = InvoiceStatusFilter.All;
        }

        if (IssueFrom is DateOnly from && IssueTo is DateOnly to && to < from)
        {
            IssueTo = from;
        }

        if (DueFrom is DateOnly dueFrom && DueTo is DateOnly dueTo && dueTo < dueFrom)
        {
            DueTo = dueFrom;
        }

        if (MinTotal is decimal min && min < 0m)
        {
            MinTotal = 0m;
        }

        if (MaxTotal is decimal max && MinTotal is decimal floor && max < floor)
        {
            MaxTotal = floor;
        }

        if (!Enum.IsDefined(SortBy))
        {
            SortBy = InvoiceSortField.IssueDate;
            SortDescending = true;
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

    public bool UsesComputedOverdue =>
        OverdueOnly || Status is InvoiceStatusFilter.Overdue;
}
