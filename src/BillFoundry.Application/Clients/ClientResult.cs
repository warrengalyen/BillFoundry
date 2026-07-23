namespace BillFoundry.Application.Clients;

public sealed class ClientResult
{
    public bool Succeeded { get; private init; }

    public bool IsForbidden { get; private init; }

    public bool IsNotFound { get; private init; }

    public bool IsConcurrencyConflict { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public ClientDetailsDto? Client { get; private init; }

    public static ClientResult Success(ClientDetailsDto client) =>
        new()
        {
            Succeeded = true,
            Client = client
        };

    public static ClientResult Forbidden() =>
        new()
        {
            IsForbidden = true,
            Errors = ["You are not allowed to manage clients."]
        };

    public static ClientResult NotFound() =>
        new()
        {
            IsNotFound = true,
            Errors = ["The client was not found."]
        };

    public static ClientResult Invalid(IReadOnlyList<string> errors, ClientDetailsDto? client = null) =>
        new()
        {
            Errors = errors,
            Client = client
        };

    public static ClientResult ConcurrencyConflict(ClientDetailsDto client) =>
        new()
        {
            IsConcurrencyConflict = true,
            Client = client,
            Errors = ["The client was updated by another user. Review the current values and save again."]
        };
}

public sealed class ClientListResult
{
    public bool Succeeded { get; private init; }

    public bool IsForbidden { get; private init; }

    public PagedResult<ClientListItemDto>? Page { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public static ClientListResult Success(PagedResult<ClientListItemDto> page) =>
        new()
        {
            Succeeded = true,
            Page = page
        };

    public static ClientListResult Forbidden() =>
        new()
        {
            IsForbidden = true,
            Errors = ["You are not allowed to manage clients."]
        };
}
