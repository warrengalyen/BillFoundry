using System.Globalization;
using BillFoundry.Domain.Catalog;

namespace BillFoundry.Web.Catalog;

internal static class CatalogFormatting
{
    public static string FormatPrice(decimal amount, string currencyCode)
    {
        string number = amount == decimal.Round(amount, 2, MidpointRounding.AwayFromZero)
            ? amount.ToString("N2", CultureInfo.CurrentCulture)
            : amount.ToString("N4", CultureInfo.CurrentCulture);
        return string.IsNullOrWhiteSpace(currencyCode) ? number : $"{currencyCode} {number}";
    }

    public static IReadOnlyList<CatalogUnitType> UnitTypes { get; } =
        [CatalogUnitType.Hour, CatalogUnitType.Day, CatalogUnitType.Item, CatalogUnitType.FlatFee];
}
