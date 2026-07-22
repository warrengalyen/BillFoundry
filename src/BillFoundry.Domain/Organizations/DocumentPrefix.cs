using System.Text.RegularExpressions;

namespace BillFoundry.Domain.Organizations;

/// <summary>
/// A short alphanumeric prefix used when numbering invoices or estimates.
/// </summary>
public readonly record struct DocumentPrefix
{
    public const int MaxLength = 10;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private DocumentPrefix(string value) => Value = value;

    public string Value { get; }

    public static DocumentPrefix InvoiceDefault { get; } = new("INV");

    public static DocumentPrefix EstimateDefault { get; } = new("EST");

    public static bool TryCreate(string? value, out DocumentPrefix prefix)
    {
        prefix = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            return false;
        }

        if (!Regex.IsMatch(trimmed, "^[A-Za-z][A-Za-z0-9]{0,9}$", RegexOptions.CultureInvariant, MatchTimeout))
        {
            return false;
        }

        prefix = new DocumentPrefix(trimmed.ToUpperInvariant());
        return true;
    }

    public static DocumentPrefix Parse(string value)
    {
        if (!TryCreate(value, out DocumentPrefix prefix))
        {
            throw new ArgumentException(
                "Prefix must start with a letter and contain only letters and digits (maximum 10 characters).",
                nameof(value));
        }

        return prefix;
    }

    public override string ToString() => Value;
}
