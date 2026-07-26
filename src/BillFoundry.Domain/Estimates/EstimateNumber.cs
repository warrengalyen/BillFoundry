using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Estimates;

/// <summary>
/// A unique public estimate number such as EST-0001.
/// </summary>
public readonly record struct EstimateNumber
{
    public const int MaxLength = 24;

    private EstimateNumber(string value) => Value = value;

    public string Value { get; }

    public static EstimateNumber Format(DocumentPrefix prefix, int sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        return new EstimateNumber($"{prefix.Value}-{sequence:D4}");
    }

    public override string ToString() => Value;
}
