using BillFoundry.Domain.Catalog;

namespace BillFoundry.Application.Catalog;

public class SaveCatalogItemCommand
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Sku { get; set; }

    public CatalogUnitType UnitType { get; set; } = CatalogUnitType.Hour;

    public decimal DefaultUnitPrice { get; set; }

    public bool IsTaxable { get; set; }
}

public sealed class UpdateCatalogItemCommand : SaveCatalogItemCommand
{
    public Guid Id { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public sealed class CatalogConcurrencyCommand
{
    public Guid Id { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
