namespace BillFoundry.Application.Invoices;

/// <summary>
/// Creates, updates, lists, and converts invoices. Mutations require the
/// <c>ManageInvoices</c> policy. Sent, paid, and void invoices cannot be
/// edited. Invoices are not deleted. Payments are not recorded in this phase.
/// </summary>
public interface IInvoiceService
{
    Task<InvoiceListResult> ListAsync(InvoiceListQuery query, CancellationToken cancellationToken = default);

    Task<InvoiceResult> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<InvoiceOptionsResult> GetOptionsAsync(CancellationToken cancellationToken = default);

    Task<InvoiceResult> CreateAsync(SaveInvoiceCommand command, CancellationToken cancellationToken = default);

    Task<InvoiceResult> UpdateHeaderAsync(UpdateInvoiceCommand command, CancellationToken cancellationToken = default);

    Task<InvoiceResult> AddLineAsync(SaveInvoiceLineCommand command, CancellationToken cancellationToken = default);

    Task<InvoiceResult> UpdateLineAsync(UpdateInvoiceLineCommand command, CancellationToken cancellationToken = default);

    Task<InvoiceResult> RemoveLineAsync(RemoveInvoiceLineCommand command, CancellationToken cancellationToken = default);

    Task<InvoiceResult> ReorderLinesAsync(ReorderInvoiceLinesCommand command, CancellationToken cancellationToken = default);

    Task<InvoiceResult> DuplicateAsync(DuplicateInvoiceCommand command, CancellationToken cancellationToken = default);

    Task<InvoiceResult> MarkSentAsync(InvoiceConcurrencyCommand command, CancellationToken cancellationToken = default);

    Task<InvoiceResult> VoidAsync(VoidInvoiceCommand command, CancellationToken cancellationToken = default);

    Task<InvoiceResult> ConvertFromEstimateAsync(ConvertEstimateCommand command, CancellationToken cancellationToken = default);
}
