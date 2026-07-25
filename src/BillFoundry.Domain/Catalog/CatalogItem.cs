using BillFoundry.Domain.Auditing;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Catalog;

/// <summary>
/// A reusable billable service or item. Catalog items are deactivated rather than
/// permanently deleted so later financial documents can keep a stable reference.
/// Prices are in the installation organization's default currency.
/// </summary>
public sealed class CatalogItem : IAuditable
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int PricePrecision = 19;
    public const int PriceScale = 4;
    public const decimal MaxUnitPrice = 99_999_999.9999m;

    private CatalogItem()
    {
        Name = string.Empty;
        RowVersion = [];
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public string? Sku { get; private set; }

    public CatalogUnitType UnitType { get; private set; }

    public decimal DefaultUnitPrice { get; private set; }

    public bool IsTaxable { get; private set; }

    public bool IsActive { get; private set; }

    public byte[] RowVersion { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public static CatalogItem Create(
        string name,
        string? description,
        string? sku,
        CatalogUnitType unitType,
        decimal defaultUnitPrice,
        bool isTaxable)
    {
        var item = new CatalogItem
        {
            Id = Guid.NewGuid(),
            IsActive = true
        };
        item.Apply(name, description, sku, unitType, defaultUnitPrice, isTaxable);
        return item;
    }

    public void Update(
        string name,
        string? description,
        string? sku,
        CatalogUnitType unitType,
        decimal defaultUnitPrice,
        bool isTaxable) =>
        Apply(name, description, sku, unitType, defaultUnitPrice, isTaxable);

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

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

    private void Apply(
        string name,
        string? description,
        string? sku,
        CatalogUnitType unitType,
        decimal defaultUnitPrice,
        bool isTaxable)
    {
        if (!CatalogUnitTypeDisplay.IsDefined(unitType))
        {
            throw new ArgumentOutOfRangeException(nameof(unitType), unitType, "The unit type is not supported.");
        }

        Name = OrganizationText.Required(name, nameof(name), NameMaxLength);
        Description = OrganizationText.Optional(description, nameof(description), DescriptionMaxLength);
        Sku = NormalizeSku(sku);
        UnitType = unitType;
        DefaultUnitPrice = NormalizePrice(defaultUnitPrice);
        IsTaxable = isTaxable;
    }

    private static string? NormalizeSku(string? sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return null;
        }

        return CatalogSku.Parse(sku).Value;
    }

    private static decimal NormalizePrice(decimal price)
    {
        if (price < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Default unit price cannot be negative.");
        }

        if (price > MaxUnitPrice)
        {
            throw new ArgumentOutOfRangeException(
                nameof(price),
                $"Default unit price cannot be greater than {MaxUnitPrice}.");
        }

        decimal rounded = decimal.Round(price, PriceScale, MidpointRounding.AwayFromZero);
        if (rounded != price)
        {
            throw new ArgumentException(
                $"Default unit price cannot have more than {PriceScale} decimal places.",
                nameof(price));
        }

        return price;
    }
}
