using BillFoundry.Domain.Invoices;

namespace BillFoundry.Application.Invoices;

public sealed class InvoiceResult
{
    public bool Succeeded { get; private init; }

    public bool IsForbidden { get; private init; }

    public bool IsNotFound { get; private init; }

    public bool IsConcurrencyConflict { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public InvoiceDetailsDto? Invoice { get; private init; }

    public static InvoiceResult Success(InvoiceDetailsDto invoice) =>
        new()
        {
            Succeeded = true,
            Invoice = invoice
        };

    public static InvoiceResult Forbidden() =>
        new()
        {
            IsForbidden = true,
            Errors = ["You are not allowed to manage invoices."]
        };

    public static InvoiceResult NotFound() =>
        new()
        {
            IsNotFound = true,
            Errors = ["The invoice was not found."]
        };

    public static InvoiceResult Invalid(IReadOnlyList<string> errors, InvoiceDetailsDto? invoice = null) =>
        new()
        {
            Errors = errors,
            Invoice = invoice
        };

    public static InvoiceResult ConcurrencyConflict(InvoiceDetailsDto invoice) =>
        new()
        {
            IsConcurrencyConflict = true,
            Invoice = invoice,
            Errors = ["The invoice was updated by another user. Review the current values and save again."]
        };
}

public sealed class InvoiceListResult
{
    public bool Succeeded { get; private init; }

    public bool IsForbidden { get; private init; }

    public PagedInvoiceResult<InvoiceListItemDto>? Page { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public static InvoiceListResult Success(PagedInvoiceResult<InvoiceListItemDto> page) =>
        new()
        {
            Succeeded = true,
            Page = page
        };

    public static InvoiceListResult Forbidden() =>
        new()
        {
            IsForbidden = true,
            Errors = ["You are not allowed to manage invoices."]
        };
}

public sealed class InvoiceOptionsResult
{
    public bool Succeeded { get; private init; }

    public bool IsForbidden { get; private init; }

    public InvoiceFormOptions? Options { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public static InvoiceOptionsResult Success(InvoiceFormOptions options) =>
        new()
        {
            Succeeded = true,
            Options = options
        };

    public static InvoiceOptionsResult Forbidden() =>
        new()
        {
            IsForbidden = true,
            Errors = ["You are not allowed to manage invoices."]
        };
}
