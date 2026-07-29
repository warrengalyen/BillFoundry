using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Invoices;

namespace BillFoundry.Application.Invoices;

public sealed class PagedInvoiceResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public required int TotalCount { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed class InvoiceListItemDto
{
    public required Guid Id { get; init; }

    public required string Number { get; init; }

    public required Guid ClientId { get; init; }

    public required string ClientName { get; init; }

    public required DateOnly IssueDate { get; init; }

    public required DateOnly DueDate { get; init; }

    public required InvoiceStatus Status { get; init; }

    public required InvoiceStatus EffectiveStatus { get; init; }

    public required string StatusLabel { get; init; }

    public required decimal Total { get; init; }

    public required decimal BalanceDue { get; init; }

    public required string CurrencyCode { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class InvoiceLineDto
{
    public required Guid Id { get; init; }

    public Guid? CatalogItemId { get; init; }

    public required string Description { get; init; }

    public required decimal Quantity { get; init; }

    public required CatalogUnitType Unit { get; init; }

    public required string UnitLabel { get; init; }

    public required decimal UnitPrice { get; init; }

    public required bool IsTaxable { get; init; }

    public required int SortOrder { get; init; }

    public required decimal LineAmount { get; init; }
}

public sealed class InvoiceDetailsDto
{
    public required Guid Id { get; init; }

    public required int Sequence { get; init; }

    public required string Number { get; init; }

    public required Guid ClientId { get; init; }

    public required string ClientName { get; init; }

    public required string ClientCode { get; init; }

    public string? ClientEmail { get; init; }

    public required bool ClientIsActive { get; init; }

    public required DateOnly IssueDate { get; init; }

    public required DateOnly DueDate { get; init; }

    public required InvoiceStatus Status { get; init; }

    public required InvoiceStatus EffectiveStatus { get; init; }

    public required string StatusLabel { get; init; }

    public string? PurchaseOrder { get; init; }

    public string? Notes { get; init; }

    public string? PaymentInstructions { get; init; }

    public string? VoidReason { get; init; }

    public required decimal Discount { get; init; }

    public required decimal TaxRatePercent { get; init; }

    public required decimal Subtotal { get; init; }

    public required decimal TaxableSubtotal { get; init; }

    public required decimal Tax { get; init; }

    public required decimal Total { get; init; }

    public required decimal AmountPaid { get; init; }

    public required decimal BalanceDue { get; init; }

    public required string CurrencyCode { get; init; }

    public Guid? SourceEstimateId { get; init; }

    public required bool CanEdit { get; init; }

    public required bool CanVoid { get; init; }

    public required IReadOnlyList<InvoiceStatus> AllowedTransitions { get; init; }

    public required byte[] RowVersion { get; init; }

    public required IReadOnlyList<InvoiceLineDto> Lines { get; init; }

    public static InvoiceDetailsDto From(
        Invoice invoice,
        bool clientIsActive,
        DateOnly today,
        byte[]? rowVersion = null)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        byte[] token = rowVersion ?? invoice.RowVersion;
        InvoiceStatus effective = invoice.EffectiveStatus(today);

        return new InvoiceDetailsDto
        {
            Id = invoice.Id,
            Sequence = invoice.Sequence,
            Number = invoice.Number,
            ClientId = invoice.ClientId,
            ClientName = invoice.ClientSnapshot.Name,
            ClientCode = invoice.ClientSnapshot.Code,
            ClientEmail = invoice.ClientSnapshot.Email,
            ClientIsActive = clientIsActive,
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            Status = invoice.Status,
            EffectiveStatus = effective,
            StatusLabel = InvoiceStatusRules.Label(effective),
            PurchaseOrder = invoice.PurchaseOrder,
            Notes = invoice.Notes,
            PaymentInstructions = invoice.PaymentInstructions,
            VoidReason = invoice.VoidReason,
            Discount = invoice.Discount,
            TaxRatePercent = invoice.TaxRatePercent,
            Subtotal = invoice.Subtotal,
            TaxableSubtotal = invoice.TaxableSubtotal,
            Tax = invoice.Tax,
            Total = invoice.Total,
            AmountPaid = invoice.AmountPaid,
            BalanceDue = invoice.BalanceDue,
            CurrencyCode = invoice.Currency.Value,
            SourceEstimateId = invoice.SourceEstimateId,
            CanEdit = invoice.CanEdit,
            CanVoid = InvoiceStatusRules.CanVoid(invoice.Status),
            AllowedTransitions = InvoiceStatusRules.UserFacingTargets(invoice.Status),
            RowVersion = [.. token],
            Lines = [.. invoice.Lines
                .OrderBy(line => line.SortOrder)
                .Select(line => new InvoiceLineDto
                {
                    Id = line.Id,
                    CatalogItemId = line.CatalogItemId,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    Unit = line.Unit,
                    UnitLabel = CatalogUnitTypeDisplay.Label(line.Unit),
                    UnitPrice = line.UnitPrice,
                    IsTaxable = line.IsTaxable,
                    SortOrder = line.SortOrder,
                    LineAmount = line.LineAmount
                })]
        };
    }
}

public sealed class InvoiceClientOption
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Code { get; init; }
}

public sealed class InvoiceCatalogOption
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required CatalogUnitType Unit { get; init; }

    public required string UnitLabel { get; init; }

    public required decimal UnitPrice { get; init; }

    public required bool IsTaxable { get; init; }
}

public sealed class InvoiceFormOptions
{
    public required IReadOnlyList<InvoiceClientOption> Clients { get; init; }

    public required IReadOnlyList<InvoiceCatalogOption> CatalogItems { get; init; }

    public required string CurrencyCode { get; init; }

    public required int DefaultPaymentTermsDays { get; init; }

    public string? DefaultNotes { get; init; }

    public string? DefaultPaymentInstructions { get; init; }

    public required DateOnly Today { get; init; }
}
