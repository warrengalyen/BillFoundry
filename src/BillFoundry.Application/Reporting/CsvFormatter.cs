using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BillFoundry.Application.Reporting;

/// <summary>
/// Builds RFC 4180 CSV with invariant dates and amounts, and prefixes cells
/// that would otherwise be interpreted as spreadsheet formulas.
/// </summary>
public static partial class CsvFormatter
{
    public const string ContentType = "text/csv; charset=utf-8";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [GeneratedRegex(@"^-?\d+(\.\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex PlainNumber();

    public static byte[] ToUtf8(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        var builder = new StringBuilder();
        AppendRow(builder, headers);
        foreach (IReadOnlyList<string> row in rows)
        {
            AppendRow(builder, row);
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    public static string Money(decimal amount) => amount.ToString("0.00", Invariant);

    public static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", Invariant);

    public static string Integer(int value) => value.ToString(Invariant);

    public static string Boolean(bool value) => value ? "Yes" : "No";

    public static string Cell(string? value)
    {
        string text = value ?? string.Empty;
        string safe = MitigateFormulaInjection(text);
        if (NeedsQuotes(safe))
        {
            return "\"" + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return safe;
    }

    public static string FileName(string reportKey, DateOnly asOf)
    {
        string token = string.IsNullOrWhiteSpace(reportKey) ? "report" : reportKey.Trim().ToLowerInvariant();
        var builder = new StringBuilder(token.Length);
        foreach (char character in token)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '-')
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        string stem = builder.ToString().Trim('-');
        if (stem.Length == 0)
        {
            stem = "report";
        }

        return $"billfoundry-{stem}-{asOf:yyyyMMdd}.csv";
    }

    internal static string MitigateFormulaInjection(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        char first = value[0];
        if (first is '=' or '+' or '@' or '\t' or '\r' or '\n')
        {
            return "'" + value;
        }

        if (first == '-' && !PlainNumber().IsMatch(value))
        {
            return "'" + value;
        }

        return value;
    }

    private static void AppendRow(StringBuilder builder, IReadOnlyList<string> cells)
    {
        for (int index = 0; index < cells.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(Cell(cells[index]));
        }

        builder.Append("\r\n");
    }

    private static bool NeedsQuotes(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n');
}
