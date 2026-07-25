using System.Text.RegularExpressions;

namespace BillFoundry.Domain.Catalog;

/// <summary>
/// An optional stock-keeping code for a catalog item, such as WEB-001.
/// </summary>
public readonly record struct CatalogSku
{
    public const int MaxLength = 40;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private CatalogSku(string value) => Value = value;

    public string Value { get; }

    public static bool TryCreate(string? value, out CatalogSku sku)
    {
        sku = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            return false;
        }

        if (!Regex.IsMatch(
                trimmed,
                "^[A-Za-z0-9][A-Za-z0-9._-]{0,39}$",
                RegexOptions.CultureInvariant,
                MatchTimeout))
        {
            return false;
        }

        sku = new CatalogSku(trimmed);
        return true;
    }

    public static CatalogSku Parse(string value)
    {
        if (!TryCreate(value, out CatalogSku sku))
        {
            throw new ArgumentException(
                "SKU must start with a letter or digit and may contain letters, digits, periods, underscores, or hyphens (maximum 40 characters).",
                nameof(value));
        }

        return sku;
    }

    public override string ToString() => Value;
}
