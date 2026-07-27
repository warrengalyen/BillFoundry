namespace BillFoundry.Application.Estimates;

/// <summary>
/// Creates, updates, and lists estimates. Mutations require the
/// <c>ManageEstimates</c> policy. Accepted and converted estimates cannot be
/// edited. Estimate-to-invoice conversion is not available in this phase.
/// </summary>
public interface IEstimateService
{
    Task<EstimateListResult> ListAsync(EstimateListQuery query, CancellationToken cancellationToken = default);

    Task<EstimateResult> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EstimateOptionsResult> GetOptionsAsync(CancellationToken cancellationToken = default);

    Task<EstimateResult> CreateAsync(SaveEstimateCommand command, CancellationToken cancellationToken = default);

    Task<EstimateResult> UpdateHeaderAsync(UpdateEstimateCommand command, CancellationToken cancellationToken = default);

    Task<EstimateResult> AddLineAsync(SaveEstimateLineCommand command, CancellationToken cancellationToken = default);

    Task<EstimateResult> UpdateLineAsync(UpdateEstimateLineCommand command, CancellationToken cancellationToken = default);

    Task<EstimateResult> RemoveLineAsync(RemoveEstimateLineCommand command, CancellationToken cancellationToken = default);

    Task<EstimateResult> ReorderLinesAsync(ReorderEstimateLinesCommand command, CancellationToken cancellationToken = default);

    Task<EstimateResult> DuplicateAsync(DuplicateEstimateCommand command, CancellationToken cancellationToken = default);

    Task<EstimateResult> TransitionAsync(TransitionEstimateCommand command, CancellationToken cancellationToken = default);
}
