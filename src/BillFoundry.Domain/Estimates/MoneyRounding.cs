namespace BillFoundry.Domain.Estimates;

/// <summary>
/// Monetary rounding used by estimates and later invoices.
/// Document amounts use two decimal places. Quantities, unit prices, and tax
/// rates use four. Midpoints round away from zero (0.005 becomes 0.01).
/// </summary>
public static class MoneyRounding
{
    public const int AmountScale = 2;
    public const int QuantityScale = 4;
    public const int PriceScale = 4;
    public const int RateScale = 4;

    public static MidpointRounding Mode { get; } = MidpointRounding.AwayFromZero;

    public static decimal Amount(decimal value) => decimal.Round(value, AmountScale, Mode);

    public static decimal Quantity(decimal value) => decimal.Round(value, QuantityScale, Mode);

    public static decimal Price(decimal value) => decimal.Round(value, PriceScale, Mode);

    public static decimal Rate(decimal value) => decimal.Round(value, RateScale, Mode);

    public static bool HasAmountScale(decimal value) => Amount(value) == value;

    public static bool HasQuantityScale(decimal value) => Quantity(value) == value;

    public static bool HasPriceScale(decimal value) => Price(value) == value;

    public static bool HasRateScale(decimal value) => Rate(value) == value;
}
