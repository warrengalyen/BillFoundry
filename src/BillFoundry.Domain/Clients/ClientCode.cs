using System.Text.RegularExpressions;

namespace BillFoundry.Domain.Clients;

/// <summary>
/// A unique business identifier for a client, such as C0001.
/// </summary>
public readonly record struct ClientCode
{
    public const int MaxLength = 20;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private ClientCode(string value) => Value = value;

    public string Value { get; }

    public static bool TryCreate(string? value, out ClientCode code)
    {
        code = default;
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
                "^[A-Za-z0-9][A-Za-z0-9._-]{0,19}$",
                RegexOptions.CultureInvariant,
                MatchTimeout))
        {
            return false;
        }

        code = new ClientCode(trimmed.ToUpperInvariant());
        return true;
    }

    public static ClientCode Parse(string value)
    {
        if (!TryCreate(value, out ClientCode code))
        {
            throw new ArgumentException(
                "Client code must start with a letter or digit and may contain letters, digits, periods, underscores, or hyphens (maximum 20 characters).",
                nameof(value));
        }

        return code;
    }

    public static ClientCode FromNumber(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        return Parse($"C{number:D4}");
    }

    public override string ToString() => Value;
}
