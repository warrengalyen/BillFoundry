namespace BillFoundry.Application.Documents;

/// <summary>
/// Creates an invoice PDF from a snapshot of persisted invoice values.
/// Implementations must not recompute totals from catalog prices.
/// </summary>
public interface IInvoiceDocumentGenerator
{
    GeneratedDocument Generate(InvoiceDocumentModel model);
}

/// <summary>
/// Creates an estimate PDF from a snapshot of persisted estimate values.
/// Implementations must not recompute totals from catalog prices.
/// </summary>
public interface IEstimateDocumentGenerator
{
    GeneratedDocument Generate(EstimateDocumentModel model);
}

/// <summary>
/// Authorizes the caller and builds an invoice PDF from stored financial data.
/// </summary>
public interface IInvoiceDocumentService
{
    Task<DocumentResult> GenerateAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Authorizes the caller and builds an estimate PDF from stored financial data.
/// </summary>
public interface IEstimateDocumentService
{
    Task<DocumentResult> GenerateAsync(Guid estimateId, CancellationToken cancellationToken = default);
}
