using System.Collections.Frozen;

namespace BillFoundry.Domain.Organizations;

/// <summary>
/// An ISO 4217 currency code from the Community Edition allowlist.
/// </summary>
public readonly record struct CurrencyCode
{
    public const int Length = 3;

    private static readonly FrozenSet<string> Allowed = FrozenSet.ToFrozenSet(
        [
            "AED", "AUD", "BRL", "CAD", "CHF", "CNY", "CZK", "DKK", "EUR", "GBP",
            "HKD", "HUF", "ILS", "INR", "JPY", "KRW", "MXN", "NOK", "NZD", "PLN",
            "RON", "SAR", "SEK", "SGD", "TRY", "USD", "ZAR"
        ],
        StringComparer.Ordinal);

    private CurrencyCode(string value) => Value = value;

    public string Value { get; }

    public static CurrencyCode Usd { get; } = new("USD");

    public static IReadOnlyCollection<string> SupportedCodes => Allowed;

    public static bool IsSupported(string? value) => TryParse(value, out _);

    public static bool TryParse(string? value, out CurrencyCode code)
    {
        code = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().ToUpperInvariant();
        if (!Allowed.Contains(normalized))
        {
            return false;
        }

        code = new CurrencyCode(normalized);
        return true;
    }

    public static CurrencyCode Parse(string value)
    {
        if (!TryParse(value, out CurrencyCode code))
        {
            throw new ArgumentException("Currency is not a supported ISO 4217 code.", nameof(value));
        }

        return code;
    }

    public override string ToString() => Value;
}
