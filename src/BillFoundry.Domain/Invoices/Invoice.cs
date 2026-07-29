using BillFoundry.Domain.Auditing;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Estimates;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Invoices;

/// <summary>
/// A bill to a client. Totals are recalculated from line snapshots, discount,
/// and tax rate. Amount paid starts at zero until payments are recorded.
/// Overdue is derived from due date and balance; it is not stored as a
/// replacement for Sent.
/// </summary>
public sealed class Invoice : IAuditable
{
    public const int NotesMaxLength = 4000;
    public const int PaymentInstructionsMaxLength = 4000;
    public const int PurchaseOrderMaxLength = 80;
    public const int VoidReasonMaxLength = 2000;
    public const int MaxLines = 100;
    public const int AmountPrecision = 19;
    public const int AmountScale = 2;
    public const decimal MaxDiscount = 99_999_999.99m;
    public const decimal MaxTaxRatePercent = 100m;

    private readonly List<InvoiceLine> _lines = [];

    private Invoice()
    {
        Number = string.Empty;
        Currency = CurrencyCode.Usd;
        ClientSnapshot = InvoiceClientSnapshot.Capture("Pending", "X", null);
        RowVersion = [];
    }

    public Guid Id { get; private set; }

    public int Sequence { get; private set; }

    public string Number { get; private set; }

    public Guid ClientId { get; private set; }

    public InvoiceClientSnapshot ClientSnapshot { get; private set; }

    public DateOnly IssueDate { get; private set; }

    public DateOnly DueDate { get; private set; }

    public InvoiceStatus Status { get; private set; }

    public string? PurchaseOrder { get; private set; }

    public string? Notes { get; private set; }

    public string? PaymentInstructions { get; private set; }

    public decimal Discount { get; private set; }

    public decimal TaxRatePercent { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal TaxableSubtotal { get; private set; }

    public decimal Tax { get; private set; }

    public decimal Total { get; private set; }

    public decimal AmountPaid { get; private set; }

    public decimal BalanceDue { get; private set; }

    public CurrencyCode Currency { get; private set; }

    public Guid? SourceEstimateId { get; private set; }

    public string? VoidReason { get; private set; }

    public byte[] RowVersion { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    public bool CanEdit => InvoiceStatusRules.CanEdit(Status);

    public static Invoice Create(
        int sequence,
        InvoiceNumber number,
        Guid clientId,
        InvoiceClientSnapshot clientSnapshot,
        DateOnly issueDate,
        DateOnly dueDate,
        string? purchaseOrder,
        string? notes,
        string? paymentInstructions,
        decimal discount,
        decimal taxRatePercent,
        CurrencyCode currency,
        Guid? sourceEstimateId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        ArgumentOutOfRangeException.ThrowIfEqual(clientId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(clientSnapshot);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Sequence = sequence,
            Number = number.Value,
            ClientId = clientId,
            ClientSnapshot = clientSnapshot,
            Status = InvoiceStatus.Draft,
            Currency = currency,
            SourceEstimateId = sourceEstimateId == Guid.Empty ? null : sourceEstimateId
        };
        invoice.ApplyHeader(issueDate, dueDate, purchaseOrder, notes, paymentInstructions, discount, taxRatePercent);
        invoice.Recalculate();
        return invoice;
    }

    public static Invoice FromEstimate(
        Estimate estimate,
        InvoiceClientSnapshot clientSnapshot,
        int sequence,
        InvoiceNumber number,
        DateOnly issueDate,
        DateOnly dueDate,
        string? purchaseOrder,
        string? notes,
        string? paymentInstructions)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        if (estimate.Status is not EstimateStatus.Accepted)
        {
            throw new InvalidOperationException("Only an accepted estimate can be converted to an invoice.");
        }

        if (estimate.Lines.Count == 0)
        {
            throw new InvalidOperationException("An estimate must have at least one line before it is converted.");
        }

        Invoice invoice = Create(
            sequence,
            number,
            estimate.ClientId,
            clientSnapshot,
            issueDate,
            dueDate,
            purchaseOrder,
            notes ?? estimate.Notes,
            paymentInstructions,
            discount: 0m,
            taxRatePercent: 0m,
            estimate.Currency,
            estimate.Id);

        foreach (EstimateLine line in estimate.Lines.OrderBy(item => item.SortOrder))
        {
            invoice.AddLine(line.CatalogItemId, line.Description, line.Quantity, line.Unit, line.UnitPrice, line.IsTaxable);
        }

        invoice.UpdateHeader(
            estimate.ClientId,
            clientSnapshot,
            issueDate,
            dueDate,
            purchaseOrder,
            notes ?? estimate.Notes,
            paymentInstructions,
            estimate.Discount,
            estimate.TaxRatePercent);
        return invoice;
    }

    public void UpdateHeader(
        Guid clientId,
        InvoiceClientSnapshot clientSnapshot,
        DateOnly issueDate,
        DateOnly dueDate,
        string? purchaseOrder,
        string? notes,
        string? paymentInstructions,
        decimal discount,
        decimal taxRatePercent)
    {
        EnsureEditable();
        ArgumentOutOfRangeException.ThrowIfEqual(clientId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(clientSnapshot);
        ClientId = clientId;
        ClientSnapshot = clientSnapshot;
        ApplyHeader(issueDate, dueDate, purchaseOrder, notes, paymentInstructions, discount, taxRatePercent);
        Recalculate();
    }

    public InvoiceLine AddLine(
        Guid? catalogItemId,
        string description,
        decimal quantity,
        CatalogUnitType unit,
        decimal unitPrice,
        bool isTaxable)
    {
        EnsureEditable();
        if (_lines.Count >= MaxLines)
        {
            throw new InvalidOperationException($"An invoice can have at most {MaxLines} lines.");
        }

        InvoiceLine line = InvoiceLine.Create(
            Id,
            catalogItemId,
            description,
            quantity,
            unit,
            unitPrice,
            isTaxable,
            _lines.Count);
        _lines.Add(line);
        Recalculate(clampDiscount: true);
        return line;
    }

    public void UpdateLine(
        Guid lineId,
        string description,
        decimal quantity,
        CatalogUnitType unit,
        decimal unitPrice,
        bool isTaxable)
    {
        EnsureEditable();
        GetLine(lineId).Update(description, quantity, unit, unitPrice, isTaxable);
        Recalculate(clampDiscount: true);
    }

    public void RemoveLine(Guid lineId)
    {
        EnsureEditable();
        InvoiceLine line = GetLine(lineId);
        _lines.Remove(line);
        CompactSortOrder();
        Recalculate(clampDiscount: true);
    }

    public void ReorderLines(IReadOnlyList<Guid> lineIds)
    {
        EnsureEditable();
        ApplyLineOrder(lineIds, offset: 0);
    }

    public void StageLineReorder(IReadOnlyList<Guid> lineIds)
    {
        EnsureEditable();
        ApplyLineOrder(lineIds, offset: MaxLines);
    }

    public void MarkSent()
    {
        if (!InvoiceStatusRules.CanTransition(Status, InvoiceStatus.Sent))
        {
            throw new InvalidOperationException(
                $"An invoice cannot move from {InvoiceStatusRules.Label(Status)} to sent.");
        }

        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("An invoice must have at least one line before it is sent.");
        }

        Status = InvoiceStatus.Sent;
    }

    public void Void(string reason)
    {
        if (!InvoiceStatusRules.CanVoid(Status))
        {
            throw new InvalidOperationException(
                $"A {InvoiceStatusRules.Label(Status).ToLowerInvariant()} invoice cannot be voided.");
        }

        if (AmountPaid > 0m)
        {
            throw new InvalidOperationException("An invoice with recorded payments cannot be voided.");
        }

        VoidReason = OrganizationText.Required(reason, nameof(reason), VoidReasonMaxLength);
        Status = InvoiceStatus.Void;
        Recalculate();
    }

    public Invoice Duplicate(int sequence, InvoiceNumber number, DateOnly issueDate, DateOnly dueDate)
    {
        InvoiceClientSnapshot snapshot = ClientSnapshot.Clone();
        Invoice copy = Create(
            sequence,
            number,
            ClientId,
            snapshot,
            issueDate,
            dueDate,
            PurchaseOrder,
            Notes,
            PaymentInstructions,
            discount: 0m,
            taxRatePercent: 0m,
            Currency);

        foreach (InvoiceLine line in _lines.OrderBy(item => item.SortOrder))
        {
            copy.AddLine(line.CatalogItemId, line.Description, line.Quantity, line.Unit, line.UnitPrice, line.IsTaxable);
        }

        copy.UpdateHeader(
            ClientId,
            snapshot,
            issueDate,
            dueDate,
            PurchaseOrder,
            Notes,
            PaymentInstructions,
            Discount,
            TaxRatePercent);
        return copy;
    }

    public InvoiceStatus EffectiveStatus(DateOnly today) =>
        InvoiceStatusRules.EffectiveStatus(Status, DueDate, BalanceDue, today);

    public bool IsOverdue(DateOnly today) =>
        InvoiceStatusRules.IsOverdue(Status, DueDate, BalanceDue, today);

    public void SetCreated(DateTimeOffset atUtc, Guid? byUserId)
    {
        CreatedAtUtc = atUtc;
        CreatedByUserId = byUserId;
    }

    public void SetUpdated(DateTimeOffset atUtc, Guid? byUserId)
    {
        UpdatedAtUtc = atUtc;
        UpdatedByUserId = byUserId;
    }

    internal void EnsureEditable()
    {
        if (!CanEdit)
        {
            throw new InvalidOperationException(
                $"A {InvoiceStatusRules.Label(Status).ToLowerInvariant()} invoice cannot be edited.");
        }
    }

    private void ApplyHeader(
        DateOnly issueDate,
        DateOnly dueDate,
        string? purchaseOrder,
        string? notes,
        string? paymentInstructions,
        decimal discount,
        decimal taxRatePercent)
    {
        if (dueDate < issueDate)
        {
            throw new ArgumentException("Due date cannot be earlier than the issue date.", nameof(dueDate));
        }

        if (discount < 0m || discount > MaxDiscount)
        {
            throw new ArgumentOutOfRangeException(nameof(discount), "Discount cannot be negative or exceed the maximum.");
        }

        if (!MoneyRounding.HasAmountScale(discount))
        {
            throw new ArgumentException("Discount cannot have more than two decimal places.", nameof(discount));
        }

        if (taxRatePercent < 0m || taxRatePercent > MaxTaxRatePercent)
        {
            throw new ArgumentOutOfRangeException(nameof(taxRatePercent), "Tax rate must be between 0 and 100 percent.");
        }

        if (!MoneyRounding.HasRateScale(taxRatePercent))
        {
            throw new ArgumentException("Tax rate cannot have more than four decimal places.", nameof(taxRatePercent));
        }

        IssueDate = issueDate;
        DueDate = dueDate;
        PurchaseOrder = OrganizationText.Optional(purchaseOrder, nameof(purchaseOrder), PurchaseOrderMaxLength);
        Notes = OrganizationText.Optional(notes, nameof(notes), NotesMaxLength);
        PaymentInstructions = OrganizationText.Optional(paymentInstructions, nameof(paymentInstructions), PaymentInstructionsMaxLength);
        Discount = discount;
        TaxRatePercent = taxRatePercent;
    }

    private void Recalculate(bool clampDiscount = false)
    {
        decimal lineSubtotal = _lines.Sum(line => line.LineAmount);
        if (clampDiscount && Discount > lineSubtotal)
        {
            Discount = lineSubtotal;
        }

        InvoiceTotals totals = InvoiceCalculator.Calculate(
            _lines.Select(line => line.ToAmount()),
            Discount,
            TaxRatePercent,
            AmountPaid,
            isVoid: Status is InvoiceStatus.Void);
        Subtotal = totals.Subtotal;
        TaxableSubtotal = totals.TaxableSubtotal;
        Tax = totals.Tax;
        Total = totals.Total;
        AmountPaid = totals.AmountPaid;
        BalanceDue = totals.BalanceDue;
    }

    private InvoiceLine GetLine(Guid lineId) =>
        _lines.SingleOrDefault(line => line.Id == lineId)
        ?? throw new InvalidOperationException("The line was not found on this invoice.");

    private void ApplyLineOrder(IReadOnlyList<Guid> lineIds, int offset)
    {
        ArgumentNullException.ThrowIfNull(lineIds);
        if (lineIds.Count != _lines.Count || lineIds.Distinct().Count() != _lines.Count)
        {
            throw new ArgumentException("The line order must include each existing line exactly once.", nameof(lineIds));
        }

        var byId = _lines.ToDictionary(line => line.Id);
        for (int index = 0; index < lineIds.Count; index++)
        {
            if (!byId.TryGetValue(lineIds[index], out InvoiceLine? line))
            {
                throw new InvalidOperationException("The line was not found on this invoice.");
            }

            line.SetSortOrder(offset + index);
        }

        _lines.Sort((left, right) => left.SortOrder.CompareTo(right.SortOrder));
    }

    private void CompactSortOrder()
    {
        _lines.Sort((left, right) => left.SortOrder.CompareTo(right.SortOrder));
        for (int index = 0; index < _lines.Count; index++)
        {
            _lines[index].SetSortOrder(index);
        }
    }
}
