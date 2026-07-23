namespace BillFoundry.Application.Clients;

public enum ClientSortField
{
    Name = 0,
    Code = 1,
    Email = 2,
    CreatedAt = 3
}

public enum ClientStatusFilter
{
    All = 0,
    Active = 1,
    Inactive = 2
}

public sealed class ClientListQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public string? Search { get; set; }

    public ClientStatusFilter Status { get; set; } = ClientStatusFilter.Active;

    public ClientSortField SortBy { get; set; } = ClientSortField.Name;

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
            Status = ClientStatusFilter.Active;
        }

        if (!Enum.IsDefined(SortBy))
        {
            SortBy = ClientSortField.Name;
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

public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public required int TotalCount { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
