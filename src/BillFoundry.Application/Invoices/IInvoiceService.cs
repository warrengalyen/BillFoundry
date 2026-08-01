namespace BillFoundry.Application.Invoices;

/// <summary>
/// Creates, updates, lists, converts invoices, and records payments.
/// Mutations require the <c>ManageInvoices</c> policy. Sent, paid, and void
/// invoices cannot be edited. Invoices and payments are not deleted.
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

    Task<InvoiceResult> RecordPaymentAsync(RecordPaymentCommand command, CancellationToken cancellationToken = default);

    Task<InvoiceResult> ReversePaymentAsync(ReversePaymentCommand command, CancellationToken cancellationToken = default);
}
