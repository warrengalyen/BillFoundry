using BillFoundry.Domain.Auditing;
using BillFoundry.Domain.Estimates;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Invoices;

/// <summary>
/// An externally received payment or a reversal of one. Amount is always
/// positive. Reversals point at the original receipt and are never themselves
/// reversed. Payments are not deleted.
/// </summary>
public sealed class InvoicePayment : IAuditable
{
    public const int ReferenceMaxLength = 80;
    public const int NotesMaxLength = 2000;
    public const int ReversalReasonMaxLength = 2000;
    public const decimal MaxAmount = Invoice.MaxDiscount;

    private InvoicePayment()
    {
        Method = PaymentMethod.Other;
    }

    public Guid Id { get; private set; }

    public Guid InvoiceId { get; private set; }

    public DateOnly PaymentDate { get; private set; }

    public decimal Amount { get; private set; }

    public PaymentMethod Method { get; private set; }

    public string? Reference { get; private set; }

    public string? Notes { get; private set; }

    public Guid? ReversesPaymentId { get; private set; }

    public string? ReversalReason { get; private set; }

    public bool IsReversal => ReversesPaymentId is not null;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    internal static InvoicePayment Record(
        Guid invoiceId,
        DateOnly paymentDate,
        decimal amount,
        PaymentMethod method,
        string? reference,
        string? notes)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(invoiceId, Guid.Empty);
        var payment = new InvoicePayment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId
        };
        payment.Apply(paymentDate, amount, method, reference, notes, reversesPaymentId: null, reversalReason: null);
        return payment;
    }

    internal static InvoicePayment Reverse(
        Guid invoiceId,
        InvoicePayment original,
        DateOnly paymentDate,
        string reason)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(invoiceId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(original);
        if (original.InvoiceId != invoiceId)
        {
            throw new InvalidOperationException("The payment was not found on this invoice.");
        }

        if (original.IsReversal)
        {
            throw new InvalidOperationException("A reversal cannot be reversed. Record a new payment instead.");
        }

        var payment = new InvoicePayment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId
        };
        payment.Apply(
            paymentDate,
            original.Amount,
            original.Method,
            original.Reference,
            notes: null,
            original.Id,
            reason);
        return payment;
    }

    public void SetCreated(DateTimeOffset atUtc, Guid? byUserId)
    {
        CreatedAtUtc = atUtc;
        CreatedByUserId = byUserId;
    }

    public void SetUpdated(DateTimeOffset atUtc, Guid? byUserId)
    {
        UpdatedAtUtc = atUtc;
        UpdatedByUserId = byUserId;
    }

    private void Apply(
        DateOnly paymentDate,
        decimal amount,
        PaymentMethod method,
        string? reference,
        string? notes,
        Guid? reversesPaymentId,
        string? reversalReason)
    {
        if (paymentDate == default)
        {
            throw new ArgumentException("Payment date is required.", nameof(paymentDate));
        }

        if (!PaymentMethodDisplay.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "The payment method is not supported.");
        }

        if (amount <= 0m || amount > MaxAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be greater than zero and at most the maximum.");
        }

        if (!MoneyRounding.HasAmountScale(amount))
        {
            throw new ArgumentException("Payment amount cannot have more than two decimal places.", nameof(amount));
        }

        PaymentDate = paymentDate;
        Amount = amount;
        Method = method;
        Reference = OrganizationText.Optional(reference, nameof(reference), ReferenceMaxLength);
        Notes = OrganizationText.Optional(notes, nameof(notes), NotesMaxLength);
        ReversesPaymentId = reversesPaymentId == Guid.Empty ? null : reversesPaymentId;
        ReversalReason = ReversesPaymentId is null
            ? null
            : OrganizationText.Required(reversalReason, "reason", ReversalReasonMaxLength);
    }
}
