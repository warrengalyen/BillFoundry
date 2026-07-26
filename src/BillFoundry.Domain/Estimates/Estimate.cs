using BillFoundry.Domain.Auditing;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Estimates;

/// <summary>
/// A priced offer to a client. Totals are recalculated from line snapshots,
/// discount, and tax rate whenever the document changes.
/// </summary>
public sealed class Estimate : IAuditable
{
    public const int NotesMaxLength = 4000;
    public const int TermsMaxLength = 4000;
    public const int MaxLines = 100;
    public const int AmountPrecision = 19;
    public const int AmountScale = 2;
    public const decimal MaxDiscount = 99_999_999.99m;
    public const decimal MaxTaxRatePercent = 100m;

    private readonly List<EstimateLine> _lines = [];

    private Estimate()
    {
        Number = string.Empty;
        Currency = CurrencyCode.Usd;
        RowVersion = [];
    }

    public Guid Id { get; private set; }

    public int Sequence { get; private set; }

    public string Number { get; private set; }

    public Guid ClientId { get; private set; }

    public DateOnly IssueDate { get; private set; }

    public DateOnly? ExpirationDate { get; private set; }

    public EstimateStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public string? Terms { get; private set; }

    public decimal Discount { get; private set; }

    public decimal TaxRatePercent { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal TaxableSubtotal { get; private set; }

    public decimal Tax { get; private set; }

    public decimal Total { get; private set; }

    public CurrencyCode Currency { get; private set; }

    public byte[] RowVersion { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public IReadOnlyList<EstimateLine> Lines => _lines;

    public bool CanEdit => EstimateStatusRules.CanEdit(Status);

    public static Estimate Create(
        int sequence,
        EstimateNumber number,
        Guid clientId,
        DateOnly issueDate,
        DateOnly? expirationDate,
        string? notes,
        string? terms,
        decimal discount,
        decimal taxRatePercent,
        CurrencyCode currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        ArgumentOutOfRangeException.ThrowIfEqual(clientId, Guid.Empty);

        var estimate = new Estimate
        {
            Id = Guid.NewGuid(),
            Sequence = sequence,
            Number = number.Value,
            ClientId = clientId,
            Status = EstimateStatus.Draft,
            Currency = currency
        };
        estimate.ApplyHeader(issueDate, expirationDate, notes, terms, discount, taxRatePercent);
        estimate.Recalculate();
        return estimate;
    }

    public void UpdateHeader(
        Guid clientId,
        DateOnly issueDate,
        DateOnly? expirationDate,
        string? notes,
        string? terms,
        decimal discount,
        decimal taxRatePercent)
    {
        EnsureEditable();
        ArgumentOutOfRangeException.ThrowIfEqual(clientId, Guid.Empty);
        ClientId = clientId;
        ApplyHeader(issueDate, expirationDate, notes, terms, discount, taxRatePercent);
        Recalculate();
    }

    public EstimateLine AddLine(
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
            throw new InvalidOperationException($"An estimate can have at most {MaxLines} lines.");
        }

        EstimateLine line = EstimateLine.Create(
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
        EstimateLine line = GetLine(lineId);
        _lines.Remove(line);
        CompactSortOrder();
        Recalculate(clampDiscount: true);
    }

    public void ReorderLines(IReadOnlyList<Guid> lineIds)
    {
        EnsureEditable();
        ApplyLineOrder(lineIds, offset: 0);
    }

    /// <summary>
    /// Writes a unique staging order so persistence can save a swap without
    /// colliding on the (EstimateId, SortOrder) unique index.
    /// </summary>
    public void StageLineReorder(IReadOnlyList<Guid> lineIds)
    {
        EnsureEditable();
        ApplyLineOrder(lineIds, offset: MaxLines);
    }

    public void TransitionTo(EstimateStatus target)
    {
        if (!EstimateStatusRules.CanTransition(Status, target))
        {
            throw new InvalidOperationException(
                $"An estimate cannot move from {EstimateStatusRules.Label(Status)} to {EstimateStatusRules.Label(target)}.");
        }

        if (target is EstimateStatus.Sent && _lines.Count == 0)
        {
            throw new InvalidOperationException("An estimate must have at least one line before it is sent.");
        }

        Status = target;
    }

    public Estimate Duplicate(int sequence, EstimateNumber number, DateOnly issueDate, DateOnly? expirationDate)
    {
        Estimate copy = Create(
            sequence,
            number,
            ClientId,
            issueDate,
            expirationDate,
            Notes,
            Terms,
            discount: 0m,
            taxRatePercent: 0m,
            Currency);

        foreach (EstimateLine line in _lines.OrderBy(item => item.SortOrder))
        {
            copy.AddLine(line.CatalogItemId, line.Description, line.Quantity, line.Unit, line.UnitPrice, line.IsTaxable);
        }

        copy.UpdateHeader(ClientId, issueDate, expirationDate, Notes, Terms, Discount, TaxRatePercent);
        return copy;
    }

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
                $"A {EstimateStatusRules.Label(Status).ToLowerInvariant()} estimate cannot be edited.");
        }
    }

    private void ApplyHeader(
        DateOnly issueDate,
        DateOnly? expirationDate,
        string? notes,
        string? terms,
        decimal discount,
        decimal taxRatePercent)
    {
        if (expirationDate is DateOnly expiration && expiration < issueDate)
        {
            throw new ArgumentException("Expiration date cannot be earlier than the issue date.", nameof(expirationDate));
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
        ExpirationDate = expirationDate;
        Notes = OrganizationText.Optional(notes, nameof(notes), NotesMaxLength);
        Terms = OrganizationText.Optional(terms, nameof(terms), TermsMaxLength);
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

        EstimateTotals totals = EstimateCalculator.Calculate(
            _lines.Select(line => line.ToAmount()),
            Discount,
            TaxRatePercent);
        Subtotal = totals.Subtotal;
        TaxableSubtotal = totals.TaxableSubtotal;
        Tax = totals.Tax;
        Total = totals.Total;
    }

    private EstimateLine GetLine(Guid lineId) =>
        _lines.SingleOrDefault(line => line.Id == lineId)
        ?? throw new InvalidOperationException("The line was not found on this estimate.");

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
            if (!byId.TryGetValue(lineIds[index], out EstimateLine? line))
            {
                throw new InvalidOperationException("The line was not found on this estimate.");
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
