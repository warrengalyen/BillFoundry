using System.Globalization;
using System.Text;

namespace BillFoundry.Application.Documents;

/// <summary>
/// Builds download file names that contain no path segments.
/// </summary>
public static class DocumentFileName
{
    public const int MaxLength = 80;

    public static string ForInvoice(string? number) => Compose("invoice", number);

    public static string ForEstimate(string? number) => Compose("estimate", number);

    private static string Compose(string prefix, string? number)
    {
        string token = Sanitize(number);
        string stem = token.Length == 0 ? prefix : $"{prefix}-{token}";
        if (stem.Length > MaxLength - 4)
        {
            stem = stem[..(MaxLength - 4)];
        }

        return stem + ".pdf";
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (char character in value.Trim())
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_')
            {
                builder.Append(character);
            }
            else if (char.IsWhiteSpace(character) || character is '/' or '\\' or ':' or '*')
            {
                if (builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }
            }
        }

        return builder.ToString().Trim('-', '.', '_');
    }
}

public static class DocumentText
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public static string Money(decimal amount, string currencyCode)
    {
        string number = amount.ToString("N2", Us);
        return string.IsNullOrWhiteSpace(currencyCode) ? number : $"{currencyCode} {number}";
    }

    public static string Quantity(decimal quantity)
    {
        decimal rounded = decimal.Round(quantity, 4, MidpointRounding.AwayFromZero);
        return rounded == decimal.Round(rounded, 2, MidpointRounding.AwayFromZero)
            ? rounded.ToString("N2", Us)
            : rounded.ToString("N4", Us);
    }

    public static string Date(DateOnly value) => value.ToString("MMMM d, yyyy", Us);

    public static string Percent(decimal rate) =>
        rate.ToString("N4", Us).TrimEnd('0').TrimEnd('.') + "%";
}
