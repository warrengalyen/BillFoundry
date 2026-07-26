namespace BillFoundry.Domain.Documents;

/// <summary>
/// Monotonic allocator used to issue unique public document numbers.
/// Callers must increment a row under a transaction lock.
/// </summary>
public sealed class DocumentSequence
{
    public const string EstimateKind = "Estimate";
    public const int KindMaxLength = 32;

    private DocumentSequence()
    {
        Kind = string.Empty;
    }

    public string Kind { get; private set; }

    public int NextValue { get; private set; }

    public static DocumentSequence Create(string kind, int nextValue = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nextValue);
        if (kind.Length > KindMaxLength)
        {
            throw new ArgumentException($"Kind must be at most {KindMaxLength} characters.", nameof(kind));
        }

        return new DocumentSequence
        {
            Kind = kind,
            NextValue = nextValue
        };
    }

    public int Allocate()
    {
        if (NextValue == int.MaxValue)
        {
            throw new InvalidOperationException("The document sequence is exhausted.");
        }

        int current = NextValue;
        NextValue = current + 1;
        return current;
    }
}
