namespace BillFoundry.Domain.Documents;

public readonly record struct DocumentLineAmount(decimal Quantity, decimal UnitPrice, bool IsTaxable)
{
    public decimal LineAmount => DocumentCalculator.LineAmount(Quantity, UnitPrice);
}

public readonly record struct DocumentTotals(
    decimal Subtotal,
    decimal Discount,
    decimal TaxableSubtotal,
    decimal Tax,
    decimal Total);

/// <summary>
/// Shared document math for estimates and invoices. Line amounts are rounded
/// first; discount is allocated proportionally across taxable lines; tax is
/// then rounded. See <c>MoneyRounding</c>.
/// </summary>
public static class DocumentCalculator
{
    public static decimal LineAmount(decimal quantity, decimal unitPrice) =>
        Estimates.MoneyRounding.Amount(quantity * unitPrice);

    public static DocumentTotals Calculate(
        IEnumerable<DocumentLineAmount> lines,
        decimal discount,
        decimal taxRatePercent)
    {
        ArgumentNullException.ThrowIfNull(lines);

        decimal subtotal = 0m;
        decimal taxableSum = 0m;
        foreach (DocumentLineAmount line in lines)
        {
            decimal amount = line.LineAmount;
            subtotal += amount;
            if (line.IsTaxable)
            {
                taxableSum += amount;
            }
        }

        if (discount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(discount), "Discount cannot be negative.");
        }

        if (discount > subtotal)
        {
            throw new ArgumentOutOfRangeException(nameof(discount), "Discount cannot exceed the subtotal.");
        }

        if (!Estimates.MoneyRounding.HasAmountScale(discount))
        {
            throw new ArgumentException("Discount cannot have more than two decimal places.", nameof(discount));
        }

        if (taxRatePercent < 0m || taxRatePercent > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(taxRatePercent), "Tax rate must be between 0 and 100 percent.");
        }

        if (!Estimates.MoneyRounding.HasRateScale(taxRatePercent))
        {
            throw new ArgumentException("Tax rate cannot have more than four decimal places.", nameof(taxRatePercent));
        }

        decimal taxableSubtotal = subtotal == 0m
            ? 0m
            : Estimates.MoneyRounding.Amount(taxableSum * (subtotal - discount) / subtotal);

        decimal tax = Estimates.MoneyRounding.Amount(taxableSubtotal * taxRatePercent / 100m);
        decimal total = subtotal - discount + tax;
        return new DocumentTotals(subtotal, discount, taxableSubtotal, tax, total);
    }
}
