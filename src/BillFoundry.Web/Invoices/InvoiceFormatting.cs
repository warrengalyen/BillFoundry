using System.Globalization;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Invoices;

namespace BillFoundry.Web.Invoices;

internal static class InvoiceFormatting
{
    public static string FormatAmount(decimal amount, string currencyCode)
    {
        string number = amount.ToString("N2", CultureInfo.CurrentCulture);
        return string.IsNullOrWhiteSpace(currencyCode) ? number : $"{currencyCode} {number}";
    }

    public static string FormatPrice(decimal amount, string currencyCode)
    {
        string number = amount == decimal.Round(amount, 2, MidpointRounding.AwayFromZero)
            ? amount.ToString("N2", CultureInfo.CurrentCulture)
            : amount.ToString("N4", CultureInfo.CurrentCulture);
        return string.IsNullOrWhiteSpace(currencyCode) ? number : $"{currencyCode} {number}";
    }

    public static string FormatQuantity(decimal quantity) =>
        quantity == decimal.Round(quantity, 2, MidpointRounding.AwayFromZero)
            ? quantity.ToString("N2", CultureInfo.CurrentCulture)
            : quantity.ToString("N4", CultureInfo.CurrentCulture);

    public static string FormatRate(decimal taxRatePercent) =>
        taxRatePercent.ToString("N4", CultureInfo.CurrentCulture).TrimEnd('0').TrimEnd('.') + "%";

    public static string StatusClass(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "is-draft",
        InvoiceStatus.Sent => "is-sent",
        InvoiceStatus.PartiallyPaid => "is-sent",
        InvoiceStatus.Paid => "is-active",
        InvoiceStatus.Overdue => "is-expired",
        InvoiceStatus.Void => "is-inactive",
        _ => "is-draft"
    };

    public static IReadOnlyList<CatalogUnitType> UnitTypes { get; } =
        [CatalogUnitType.Hour, CatalogUnitType.Day, CatalogUnitType.Item, CatalogUnitType.FlatFee];

    public static IReadOnlyList<PaymentMethod> PaymentMethods => PaymentMethodDisplay.All;
}
