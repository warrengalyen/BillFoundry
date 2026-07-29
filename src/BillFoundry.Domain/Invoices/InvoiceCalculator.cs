using BillFoundry.Domain.Documents;
using BillFoundry.Domain.Estimates;

namespace BillFoundry.Domain.Invoices;

public readonly record struct InvoiceLineAmount(decimal Quantity, decimal UnitPrice, bool IsTaxable)
{
    public decimal LineAmount => InvoiceCalculator.LineAmount(Quantity, UnitPrice);
}

public readonly record struct InvoiceTotals(
    decimal Subtotal,
    decimal Discount,
    decimal TaxableSubtotal,
    decimal Tax,
    decimal Total,
    decimal AmountPaid,
    decimal BalanceDue);

/// <summary>
/// Invoice totals use the same rounding as estimates. Amount paid is applied
/// after document tax; voided invoices have a zero balance due.
/// </summary>
public static class InvoiceCalculator
{
    public static decimal LineAmount(decimal quantity, decimal unitPrice) =>
        DocumentCalculator.LineAmount(quantity, unitPrice);

    public static InvoiceTotals Calculate(
        IEnumerable<InvoiceLineAmount> lines,
        decimal discount,
        decimal taxRatePercent,
        decimal amountPaid,
        bool isVoid)
    {
        ArgumentNullException.ThrowIfNull(lines);

        DocumentTotals document = DocumentCalculator.Calculate(
            lines.Select(line => new DocumentLineAmount(line.Quantity, line.UnitPrice, line.IsTaxable)),
            discount,
            taxRatePercent);

        if (amountPaid < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amountPaid), "Amount paid cannot be negative.");
        }

        if (!MoneyRounding.HasAmountScale(amountPaid))
        {
            throw new ArgumentException("Amount paid cannot have more than two decimal places.", nameof(amountPaid));
        }

        if (amountPaid > document.Total)
        {
            throw new ArgumentOutOfRangeException(nameof(amountPaid), "Amount paid cannot exceed the invoice total.");
        }

        decimal balanceDue = isVoid ? 0m : document.Total - amountPaid;
        return new InvoiceTotals(
            document.Subtotal,
            document.Discount,
            document.TaxableSubtotal,
            document.Tax,
            document.Total,
            amountPaid,
            balanceDue);
    }
}
