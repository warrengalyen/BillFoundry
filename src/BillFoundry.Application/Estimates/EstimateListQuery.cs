using BillFoundry.Domain.Estimates;

namespace BillFoundry.Application.Estimates;

public enum EstimateSortField
{
    IssueDate = 0,
    Number = 1,
    Client = 2,
    Total = 3,
    Status = 4,
    CreatedAt = 5
}

public enum EstimateStatusFilter
{
    All = -1,
    Draft = 0,
    Sent = 1,
    Accepted = 2,
    Declined = 3,
    Expired = 4,
    Converted = 5
}

public sealed class EstimateListQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public string? Search { get; set; }

    public EstimateStatusFilter Status { get; set; } = EstimateStatusFilter.All;

    public EstimateSortField SortBy { get; set; } = EstimateSortField.IssueDate;

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

        if (!Enum.IsDefined(Status))
        {
            Status = EstimateStatusFilter.All;
        }

        if (!Enum.IsDefined(SortBy))
        {
            SortBy = EstimateSortField.IssueDate;
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

    public EstimateStatus? StatusValue() => Status is EstimateStatusFilter.All
        ? null
        : (EstimateStatus)Status;
}

public sealed class PagedEstimateResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public required int TotalCount { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
