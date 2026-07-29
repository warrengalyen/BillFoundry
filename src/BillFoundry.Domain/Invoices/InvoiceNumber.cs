using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Invoices;

/// <summary>
/// A unique public invoice number such as INV-0001.
/// </summary>
public readonly record struct InvoiceNumber
{
    public const int MaxLength = 24;

    private InvoiceNumber(string value) => Value = value;

    public string Value { get; }

    public static InvoiceNumber Format(DocumentPrefix prefix, int sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        return new InvoiceNumber($"{prefix.Value}-{sequence:D4}");
    }

    public override string ToString() => Value;
}
