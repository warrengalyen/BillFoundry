using BillFoundry.Domain.Catalog;

namespace BillFoundry.Application.Catalog;

public sealed class CatalogListItemDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Sku { get; init; }

    public required CatalogUnitType UnitType { get; init; }

    public required string UnitTypeLabel { get; init; }

    public required decimal DefaultUnitPrice { get; init; }

    public required bool IsTaxable { get; init; }

    public required bool IsActive { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class CatalogItemDetailsDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Sku { get; init; }

    public required CatalogUnitType UnitType { get; init; }

    public required string UnitTypeLabel { get; init; }

    public required decimal DefaultUnitPrice { get; init; }

    public required bool IsTaxable { get; init; }

    public required bool IsActive { get; init; }

    public required string CurrencyCode { get; init; }

    public required byte[] RowVersion { get; init; }

    public static CatalogItemDetailsDto From(CatalogItem item, string currencyCode, byte[]? rowVersion = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
        byte[] token = rowVersion ?? item.RowVersion;

        return new CatalogItemDetailsDto
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            Sku = item.Sku,
            UnitType = item.UnitType,
            UnitTypeLabel = CatalogUnitTypeDisplay.Label(item.UnitType),
            DefaultUnitPrice = item.DefaultUnitPrice,
            IsTaxable = item.IsTaxable,
            IsActive = item.IsActive,
            CurrencyCode = currencyCode,
            RowVersion = [.. token]
        };
    }
}
