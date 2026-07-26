using BillFoundry.Domain.Auditing;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Estimates;

/// <summary>
/// A historical snapshot of a billed line. Catalog price changes do not update it.
/// </summary>
public sealed class EstimateLine : IAuditable
{
    public const int DescriptionMaxLength = 2000;
    public const decimal MaxQuantity = 999_999.9999m;
    public const decimal MaxUnitPrice = CatalogItem.MaxUnitPrice;

    private EstimateLine()
    {
        Description = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid EstimateId { get; private set; }

    public Guid? CatalogItemId { get; private set; }

    public string Description { get; private set; }

    public decimal Quantity { get; private set; }

    public CatalogUnitType Unit { get; private set; }

    public decimal UnitPrice { get; private set; }

    public bool IsTaxable { get; private set; }

    public int SortOrder { get; private set; }

    public decimal LineAmount { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    internal static EstimateLine Create(
        Guid estimateId,
        Guid? catalogItemId,
        string description,
        decimal quantity,
        CatalogUnitType unit,
        decimal unitPrice,
        bool isTaxable,
        int sortOrder)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(estimateId, Guid.Empty);

        var line = new EstimateLine
        {
            Id = Guid.NewGuid(),
            EstimateId = estimateId,
            CatalogItemId = catalogItemId == Guid.Empty ? null : catalogItemId
        };
        line.Apply(description, quantity, unit, unitPrice, isTaxable, sortOrder);
        return line;
    }

    internal void Update(
        string description,
        decimal quantity,
        CatalogUnitType unit,
        decimal unitPrice,
        bool isTaxable) =>
        Apply(description, quantity, unit, unitPrice, isTaxable, SortOrder);

    internal void SetSortOrder(int sortOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sortOrder);
        SortOrder = sortOrder;
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

    internal EstimateLineAmount ToAmount() => new(Quantity, UnitPrice, IsTaxable);

    private void Apply(
        string description,
        decimal quantity,
        CatalogUnitType unit,
        decimal unitPrice,
        bool isTaxable,
        int sortOrder)
    {
        if (!CatalogUnitTypeDisplay.IsDefined(unit))
        {
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "The unit type is not supported.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(sortOrder);

        if (quantity <= 0m || quantity > MaxQuantity)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero and at most 999,999.9999.");
        }

        if (!MoneyRounding.HasQuantityScale(quantity))
        {
            throw new ArgumentException("Quantity cannot have more than four decimal places.", nameof(quantity));
        }

        if (unitPrice < 0m || unitPrice > MaxUnitPrice)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative or exceed the maximum.");
        }

        if (!MoneyRounding.HasPriceScale(unitPrice))
        {
            throw new ArgumentException("Unit price cannot have more than four decimal places.", nameof(unitPrice));
        }

        Description = OrganizationText.Required(description, nameof(description), DescriptionMaxLength);
        Quantity = quantity;
        Unit = unit;
        UnitPrice = unitPrice;
        IsTaxable = isTaxable;
        SortOrder = sortOrder;
        LineAmount = EstimateCalculator.LineAmount(quantity, unitPrice);
    }
}
