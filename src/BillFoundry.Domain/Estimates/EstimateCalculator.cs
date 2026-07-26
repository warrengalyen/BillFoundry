namespace BillFoundry.Domain.Estimates;

public readonly record struct EstimateLineAmount(decimal Quantity, decimal UnitPrice, bool IsTaxable)
{
    public decimal LineAmount => EstimateCalculator.LineAmount(Quantity, UnitPrice);
}

public readonly record struct EstimateTotals(
    decimal Subtotal,
    decimal Discount,
    decimal TaxableSubtotal,
    decimal Tax,
    decimal Total);

/// <summary>
/// Document-level estimate math. Line amounts are rounded first; discount is
/// allocated proportionally across taxable lines; tax is then rounded.
/// </summary>
public static class EstimateCalculator
{
    public static decimal LineAmount(decimal quantity, decimal unitPrice) =>
        MoneyRounding.Amount(quantity * unitPrice);

    public static EstimateTotals Calculate(
        IEnumerable<EstimateLineAmount> lines,
        decimal discount,
        decimal taxRatePercent)
    {
        ArgumentNullException.ThrowIfNull(lines);

        decimal subtotal = 0m;
        decimal taxableSum = 0m;
        foreach (EstimateLineAmount line in lines)
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

        if (!MoneyRounding.HasAmountScale(discount))
        {
            throw new ArgumentException("Discount cannot have more than two decimal places.", nameof(discount));
        }

        if (taxRatePercent < 0m || taxRatePercent > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(taxRatePercent), "Tax rate must be between 0 and 100 percent.");
        }

        if (!MoneyRounding.HasRateScale(taxRatePercent))
        {
            throw new ArgumentException("Tax rate cannot have more than four decimal places.", nameof(taxRatePercent));
        }

        decimal taxableSubtotal = subtotal == 0m
            ? 0m
            : MoneyRounding.Amount(taxableSum * (subtotal - discount) / subtotal);

        decimal tax = MoneyRounding.Amount(taxableSubtotal * taxRatePercent / 100m);
        decimal total = subtotal - discount + tax;
        return new EstimateTotals(subtotal, discount, taxableSubtotal, tax, total);
    }
}
