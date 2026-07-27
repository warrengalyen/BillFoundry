using BillFoundry.Domain.Estimates;

namespace BillFoundry.Application.Estimates;

public sealed class EstimateResult
{
    public bool Succeeded { get; private init; }

    public bool IsForbidden { get; private init; }

    public bool IsNotFound { get; private init; }

    public bool IsConcurrencyConflict { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public EstimateDetailsDto? Estimate { get; private init; }

    public static EstimateResult Success(EstimateDetailsDto estimate) =>
        new()
        {
            Succeeded = true,
            Estimate = estimate
        };

    public static EstimateResult Forbidden() =>
        new()
        {
            IsForbidden = true,
            Errors = ["You are not allowed to manage estimates."]
        };

    public static EstimateResult NotFound() =>
        new()
        {
            IsNotFound = true,
            Errors = ["The estimate was not found."]
        };

    public static EstimateResult Invalid(IReadOnlyList<string> errors, EstimateDetailsDto? estimate = null) =>
        new()
        {
            Errors = errors,
            Estimate = estimate
        };

    public static EstimateResult ConcurrencyConflict(EstimateDetailsDto estimate) =>
        new()
        {
            IsConcurrencyConflict = true,
            Estimate = estimate,
            Errors = ["The estimate was updated by another user. Review the current values and save again."]
        };
}

public sealed class EstimateListResult
{
    public bool Succeeded { get; private init; }

    public bool IsForbidden { get; private init; }

    public PagedEstimateResult<EstimateListItemDto>? Page { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public static EstimateListResult Success(PagedEstimateResult<EstimateListItemDto> page) =>
        new()
        {
            Succeeded = true,
            Page = page
        };

    public static EstimateListResult Forbidden() =>
        new()
        {
            IsForbidden = true,
            Errors = ["You are not allowed to manage estimates."]
        };
}

public sealed class EstimateOptionsResult
{
    public bool Succeeded { get; private init; }

    public bool IsForbidden { get; private init; }

    public EstimateFormOptions? Options { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public static EstimateOptionsResult Success(EstimateFormOptions options) =>
        new()
        {
            Succeeded = true,
            Options = options
        };

    public static EstimateOptionsResult Forbidden() =>
        new()
        {
            IsForbidden = true,
            Errors = ["You are not allowed to manage estimates."]
        };
}
