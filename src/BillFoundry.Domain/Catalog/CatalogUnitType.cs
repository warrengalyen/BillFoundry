namespace BillFoundry.Domain.Catalog;

/// <summary>
/// How a catalog item is measured when it is billed.
/// </summary>
public enum CatalogUnitType
{
    Hour = 0,
    Day = 1,
    Item = 2,
    FlatFee = 3
}

public static class CatalogUnitTypeDisplay
{
    public static string Label(CatalogUnitType unitType) => unitType switch
    {
        CatalogUnitType.Hour => "Hour",
        CatalogUnitType.Day => "Day",
        CatalogUnitType.Item => "Item",
        CatalogUnitType.FlatFee => "Flat fee",
        _ => throw new ArgumentOutOfRangeException(nameof(unitType), unitType, "The unit type is not supported.")
    };

    public static bool IsDefined(CatalogUnitType unitType) =>
        Enum.IsDefined(unitType);
}
