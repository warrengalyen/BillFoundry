using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Invoices;

namespace BillFoundry.Application.Invoices;

public class SaveInvoiceCommand
{
    public Guid ClientId { get; set; }

    public DateOnly IssueDate { get; set; }

    public DateOnly DueDate { get; set; }

    public string? PurchaseOrder { get; set; }

    public string? Notes { get; set; }

    public string? PaymentInstructions { get; set; }

    public decimal Discount { get; set; }

    public decimal TaxRatePercent { get; set; }
}

public sealed class UpdateInvoiceCommand : SaveInvoiceCommand
{
    public Guid Id { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public class InvoiceConcurrencyCommand
{
    public Guid Id { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public class SaveInvoiceLineCommand : InvoiceConcurrencyCommand
{
    public Guid? CatalogItemId { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1m;

    public CatalogUnitType Unit { get; set; } = CatalogUnitType.Hour;

    public decimal UnitPrice { get; set; }

    public bool IsTaxable { get; set; }
}

public sealed class UpdateInvoiceLineCommand : SaveInvoiceLineCommand
{
    public Guid LineId { get; set; }
}

public sealed class RemoveInvoiceLineCommand : InvoiceConcurrencyCommand
{
    public Guid LineId { get; set; }
}

public sealed class ReorderInvoiceLinesCommand : InvoiceConcurrencyCommand
{
    public IReadOnlyList<Guid> LineIds { get; set; } = [];
}

public sealed class VoidInvoiceCommand : InvoiceConcurrencyCommand
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class DuplicateInvoiceCommand
{
    public Guid Id { get; set; }
}

public sealed class ReversePaymentCommand : InvoiceConcurrencyCommand
{
    public Guid PaymentId { get; set; }

    public string Reason { get; set; } = string.Empty;
}

public sealed class RecordPaymentCommand : InvoiceConcurrencyCommand
{
    public DateOnly PaymentDate { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }
}

public sealed class ConvertEstimateCommand
{
    public Guid EstimateId { get; set; }

    public byte[] EstimateRowVersion { get; set; } = [];

    public DateOnly? IssueDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public string? PurchaseOrder { get; set; }

    public string? Notes { get; set; }

    public string? PaymentInstructions { get; set; }
}
