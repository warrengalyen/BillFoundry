using BillFoundry.Domain.Documents;

namespace BillFoundry.Domain.Estimates;

public readonly record struct EstimateLineAmount(decimal Quantity, decimal UnitPrice, bool IsTaxable)
{
    public decimal LineAmount => EstimateCalculator.LineAmount(Quantity, UnitPrice);

    internal DocumentLineAmount ToDocumentAmount() => new(Quantity, UnitPrice, IsTaxable);
}

public readonly record struct EstimateTotals(
    decimal Subtotal,
    decimal Discount,
    decimal TaxableSubtotal,
    decimal Tax,
    decimal Total);

/// <summary>
/// Document-level estimate math. Delegates to the shared document calculator
/// so invoices use the same rounding rules.
/// </summary>
public static class EstimateCalculator
{
    public static decimal LineAmount(decimal quantity, decimal unitPrice) =>
        DocumentCalculator.LineAmount(quantity, unitPrice);

    public static EstimateTotals Calculate(
        IEnumerable<EstimateLineAmount> lines,
        decimal discount,
        decimal taxRatePercent)
    {
        ArgumentNullException.ThrowIfNull(lines);
        DocumentTotals totals = DocumentCalculator.Calculate(
            lines.Select(line => line.ToDocumentAmount()),
            discount,
            taxRatePercent);
        return new EstimateTotals(
            totals.Subtotal,
            totals.Discount,
            totals.TaxableSubtotal,
            totals.Tax,
            totals.Total);
    }
}
