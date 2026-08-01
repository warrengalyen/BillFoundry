using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Estimates;
using BillFoundry.Domain.Invoices;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Tests;

public sealed class InvoiceCalculatorTests
{
    [Fact]
    public void Line_amount_matches_shared_document_rounding()
    {
        Assert.Equal(1.51m, InvoiceCalculator.LineAmount(1.5m, 1.005m));
        Assert.Equal(1.00m, InvoiceCalculator.LineAmount(3m, 0.3333m));
    }

    [Fact]
    public void Unpaid_invoice_balance_equals_total()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(
            [new InvoiceLineAmount(1m, 10.00m, true)],
            discount: 0m,
            taxRatePercent: 8.25m,
            amountPaid: 0m,
            isVoid: false);

        Assert.Equal(0.83m, totals.Tax);
        Assert.Equal(10.83m, totals.Total);
        Assert.Equal(0m, totals.AmountPaid);
        Assert.Equal(10.83m, totals.BalanceDue);
    }

    [Fact]
    public void Void_invoice_has_zero_balance_due()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(
            [new InvoiceLineAmount(1m, 40.00m, false)],
            discount: 0m,
            taxRatePercent: 0m,
            amountPaid: 0m,
            isVoid: true);

        Assert.Equal(40.00m, totals.Total);
        Assert.Equal(0m, totals.BalanceDue);
    }

    [Fact]
    public void Amount_paid_reduces_balance_after_tax()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(
            [new InvoiceLineAmount(1m, 100.00m, false)],
            discount: 0m,
            taxRatePercent: 0m,
            amountPaid: 40.00m,
            isVoid: false);

        Assert.Equal(100.00m, totals.Total);
        Assert.Equal(40.00m, totals.AmountPaid);
        Assert.Equal(60.00m, totals.BalanceDue);
    }

    [Fact]
    public void Amount_paid_cannot_exceed_total()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InvoiceCalculator.Calculate(
                [new InvoiceLineAmount(1m, 10m, false)],
                0m,
                0m,
                amountPaid: 10.01m,
                isVoid: false));
    }
}

public sealed class InvoiceStatusRulesTests
{
    [Theory]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Sent, true)]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Void, true)]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Paid, false)]
    [InlineData(InvoiceStatus.Sent, InvoiceStatus.Draft, false)]
    [InlineData(InvoiceStatus.Sent, InvoiceStatus.Void, true)]
    [InlineData(InvoiceStatus.Sent, InvoiceStatus.Overdue, false)]
    [InlineData(InvoiceStatus.PartiallyPaid, InvoiceStatus.Void, false)]
    [InlineData(InvoiceStatus.Paid, InvoiceStatus.Void, false)]
    [InlineData(InvoiceStatus.Void, InvoiceStatus.Draft, false)]
    public void Transition_matrix_is_explicit(InvoiceStatus from, InvoiceStatus to, bool allowed)
    {
        Assert.Equal(allowed, InvoiceStatusRules.CanTransition(from, to));
    }

    [Fact]
    public void Only_draft_invoices_can_be_edited()
    {
        Assert.True(InvoiceStatusRules.CanEdit(InvoiceStatus.Draft));
        Assert.False(InvoiceStatusRules.CanEdit(InvoiceStatus.Sent));
        Assert.False(InvoiceStatusRules.CanEdit(InvoiceStatus.Overdue));
        Assert.False(InvoiceStatusRules.CanEdit(InvoiceStatus.Void));
    }

    [Fact]
    public void Overdue_is_derived_from_due_date_and_balance()
    {
        DateOnly today = new(2026, 8, 22);
        DateOnly due = new(2026, 8, 21);

        Assert.True(InvoiceStatusRules.IsOverdue(InvoiceStatus.Sent, due, 10m, today));
        Assert.True(InvoiceStatusRules.IsOverdue(InvoiceStatus.PartiallyPaid, due, 1m, today));
        Assert.False(InvoiceStatusRules.IsOverdue(InvoiceStatus.Sent, today, 10m, today));
        Assert.False(InvoiceStatusRules.IsOverdue(InvoiceStatus.Sent, due, 0m, today));
        Assert.False(InvoiceStatusRules.IsOverdue(InvoiceStatus.Draft, due, 10m, today));
        Assert.False(InvoiceStatusRules.IsOverdue(InvoiceStatus.Paid, due, 0m, today));
        Assert.False(InvoiceStatusRules.IsOverdue(InvoiceStatus.Void, due, 10m, today));
        Assert.Equal(
            InvoiceStatus.Overdue,
            InvoiceStatusRules.EffectiveStatus(InvoiceStatus.Sent, due, 10m, today));
        Assert.Equal(
            InvoiceStatus.Sent,
            InvoiceStatusRules.EffectiveStatus(InvoiceStatus.Sent, today, 10m, today));
    }

    [Fact]
    public void User_facing_targets_do_not_include_overdue_or_paid()
    {
        Assert.Equal(
            [InvoiceStatus.Sent, InvoiceStatus.Void],
            InvoiceStatusRules.UserFacingTargets(InvoiceStatus.Draft));
        Assert.DoesNotContain(InvoiceStatus.Overdue, InvoiceStatusRules.UserFacingTargets(InvoiceStatus.Sent));
        Assert.Empty(InvoiceStatusRules.UserFacingTargets(InvoiceStatus.PartiallyPaid));
        Assert.Empty(InvoiceStatusRules.UserFacingTargets(InvoiceStatus.Paid));
    }

    [Fact]
    public void Payments_are_recorded_on_sent_or_partially_paid_invoices()
    {
        Assert.True(InvoiceStatusRules.CanRecordPayment(InvoiceStatus.Sent));
        Assert.True(InvoiceStatusRules.CanRecordPayment(InvoiceStatus.PartiallyPaid));
        Assert.False(InvoiceStatusRules.CanRecordPayment(InvoiceStatus.Draft));
        Assert.False(InvoiceStatusRules.CanRecordPayment(InvoiceStatus.Paid));
        Assert.False(InvoiceStatusRules.CanRecordPayment(InvoiceStatus.Void));
        Assert.False(InvoiceStatusRules.CanTransition(InvoiceStatus.Sent, InvoiceStatus.PartiallyPaid));
        Assert.False(InvoiceStatusRules.CanTransition(InvoiceStatus.Sent, InvoiceStatus.Paid));
    }
}

public sealed class InvoiceNumberTests
{
    [Fact]
    public void Format_pads_to_four_digits_until_the_sequence_grows()
    {
        Assert.Equal("INV-0001", InvoiceNumber.Format(DocumentPrefix.InvoiceDefault, 1).Value);
        Assert.Equal("INV-10000", InvoiceNumber.Format(DocumentPrefix.InvoiceDefault, 10000).Value);
    }
}

public sealed class InvoiceTests
{
    [Fact]
    public void Create_starts_as_draft_with_zero_totals_and_balance()
    {
        Invoice invoice = Draft();

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.True(invoice.CanEdit);
        Assert.Equal("INV-0001", invoice.Number);
        Assert.Equal(0m, invoice.Total);
        Assert.Equal(0m, invoice.AmountPaid);
        Assert.Equal(0m, invoice.BalanceDue);
        Assert.Null(invoice.SourceEstimateId);
        Assert.Equal("USD", invoice.Currency.Value);
        Assert.Equal("Acme", invoice.ClientSnapshot.Name);
    }

    [Fact]
    public void Adding_lines_recalculates_persisted_totals()
    {
        Invoice invoice = Draft();
        invoice.AddLine(null, "Design", 2m, CatalogUnitType.Hour, 100m, true);
        invoice.UpdateHeader(
            invoice.ClientId,
            invoice.ClientSnapshot,
            invoice.IssueDate,
            invoice.DueDate,
            null,
            null,
            null,
            20m,
            10m);

        Assert.Equal(200.00m, invoice.Subtotal);
        Assert.Equal(20.00m, invoice.Discount);
        Assert.Equal(180.00m, invoice.TaxableSubtotal);
        Assert.Equal(18.00m, invoice.Tax);
        Assert.Equal(198.00m, invoice.Total);
        Assert.Equal(198.00m, invoice.BalanceDue);
        Assert.Equal(200.00m, invoice.Lines[0].LineAmount);
    }

    [Fact]
    public void Line_snapshots_keep_the_values_supplied_at_add_time()
    {
        Guid catalogId = Guid.NewGuid();
        Invoice invoice = Draft();
        invoice.AddLine(catalogId, "Copied description", 1.5m, CatalogUnitType.Hour, 125.5m, true);

        InvoiceLine line = invoice.Lines[0];
        Assert.Equal(catalogId, line.CatalogItemId);
        Assert.Equal("Copied description", line.Description);
        Assert.Equal(1.5m, line.Quantity);
        Assert.Equal(125.5m, line.UnitPrice);
        Assert.True(line.IsTaxable);
    }

    [Fact]
    public void Send_requires_at_least_one_line_and_locks_editing()
    {
        Invoice invoice = Draft();
        Assert.Throws<InvalidOperationException>(invoice.MarkSent);

        invoice.AddLine(null, "Work", 1m, CatalogUnitType.Hour, 50m, false);
        invoice.MarkSent();

        Assert.Equal(InvoiceStatus.Sent, invoice.Status);
        Assert.False(invoice.CanEdit);
        Assert.Throws<InvalidOperationException>(() =>
            invoice.AddLine(null, "More", 1m, CatalogUnitType.Hour, 10m, false));
        Assert.Throws<InvalidOperationException>(() => invoice.MarkSent());
    }

    [Fact]
    public void Sent_invoice_is_overdue_when_due_date_has_passed_with_a_balance()
    {
        Invoice invoice = Draft(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10));
        invoice.AddLine(null, "Work", 1m, CatalogUnitType.Hour, 50m, false);
        invoice.MarkSent();

        DateOnly today = new(2026, 8, 22);
        Assert.Equal(InvoiceStatus.Sent, invoice.Status);
        Assert.True(invoice.IsOverdue(today));
        Assert.Equal(InvoiceStatus.Overdue, invoice.EffectiveStatus(today));
        Assert.False(invoice.IsOverdue(new DateOnly(2026, 8, 10)));
    }

    [Fact]
    public void Void_requires_a_reason_and_clears_balance_due()
    {
        Invoice invoice = Draft();
        invoice.AddLine(null, "Work", 1m, CatalogUnitType.Item, 40m, false);
        invoice.MarkSent();
        invoice.Void("Client cancelled the engagement.");

        Assert.Equal(InvoiceStatus.Void, invoice.Status);
        Assert.Equal("Client cancelled the engagement.", invoice.VoidReason);
        Assert.Equal(40.00m, invoice.Total);
        Assert.Equal(0m, invoice.BalanceDue);
        Assert.False(invoice.CanEdit);
        Assert.False(invoice.IsOverdue(new DateOnly(2026, 9, 1)));
        Assert.Throws<InvalidOperationException>(() => invoice.Void("again"));
    }

    [Fact]
    public void Duplicate_copies_snapshots_without_the_source_estimate()
    {
        Invoice invoice = Draft();
        invoice.AddLine(Guid.NewGuid(), "Snapshot", 3m, CatalogUnitType.Day, 700m, true);
        invoice.UpdateHeader(
            invoice.ClientId,
            invoice.ClientSnapshot,
            invoice.IssueDate,
            invoice.DueDate,
            "PO-1",
            "Note",
            "Pay by transfer",
            50m,
            5m);

        Invoice copy = invoice.Duplicate(
            2,
            InvoiceNumber.Format(DocumentPrefix.InvoiceDefault, 2),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 15));

        Assert.NotEqual(invoice.Id, copy.Id);
        Assert.Equal("INV-0002", copy.Number);
        Assert.Equal(InvoiceStatus.Draft, copy.Status);
        Assert.Equal(invoice.ClientId, copy.ClientId);
        Assert.Equal("Acme", copy.ClientSnapshot.Name);
        Assert.Equal("PO-1", copy.PurchaseOrder);
        Assert.Equal(50m, copy.Discount);
        Assert.Equal(invoice.Lines[0].UnitPrice, copy.Lines[0].UnitPrice);
        Assert.NotEqual(invoice.Lines[0].Id, copy.Lines[0].Id);
        Assert.Equal(invoice.Total, copy.Total);
        Assert.Null(copy.SourceEstimateId);
        Assert.Empty(copy.Payments);
        Assert.Equal(0m, copy.AmountPaid);
    }

    [Fact]
    public void From_estimate_copies_financials_and_line_snapshots()
    {
        Estimate estimate = Estimate.Create(
            1,
            EstimateNumber.Format(DocumentPrefix.EstimateDefault, 1),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            null,
            "Estimate notes",
            "Net 15",
            0m,
            0m,
            CurrencyCode.Usd);
        estimate.AddLine(Guid.NewGuid(), "Copied line", 2m, CatalogUnitType.Hour, 125m, true);
        estimate.UpdateHeader(estimate.ClientId, estimate.IssueDate, null, "Estimate notes", "Net 15", 10m, 8.25m);
        estimate.TransitionTo(EstimateStatus.Sent);
        estimate.TransitionTo(EstimateStatus.Accepted);

        Invoice invoice = Invoice.FromEstimate(
            estimate,
            InvoiceClientSnapshot.Capture("Acme", "C001", "a@acme.test"),
            3,
            InvoiceNumber.Format(DocumentPrefix.InvoiceDefault, 3),
            new DateOnly(2026, 8, 22),
            new DateOnly(2026, 9, 21),
            "PO-9",
            null,
            "Pay by transfer");

        Assert.Equal(estimate.Id, invoice.SourceEstimateId);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(estimate.ClientId, invoice.ClientId);
        Assert.Equal(estimate.Discount, invoice.Discount);
        Assert.Equal(estimate.TaxRatePercent, invoice.TaxRatePercent);
        Assert.Equal(estimate.Total, invoice.Total);
        Assert.Equal(estimate.Total, invoice.BalanceDue);
        Assert.Equal("Copied line", invoice.Lines[0].Description);
        Assert.Equal(125m, invoice.Lines[0].UnitPrice);
        Assert.Equal(estimate.Lines[0].CatalogItemId, invoice.Lines[0].CatalogItemId);
        Assert.Equal("Estimate notes", invoice.Notes);
        Assert.Equal("Pay by transfer", invoice.PaymentInstructions);
        Assert.Equal("USD", invoice.Currency.Value);
    }

    [Fact]
    public void From_estimate_rejects_unaccepted_offers()
    {
        Estimate estimate = Estimate.Create(
            1,
            EstimateNumber.Format(DocumentPrefix.EstimateDefault, 1),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 22),
            null,
            null,
            null,
            0m,
            0m,
            CurrencyCode.Usd);
        estimate.AddLine(null, "Work", 1m, CatalogUnitType.Hour, 50m, false);

        Assert.Throws<InvalidOperationException>(() =>
            Invoice.FromEstimate(
                estimate,
                InvoiceClientSnapshot.Capture("Acme", "C001", null),
                1,
                InvoiceNumber.Format(DocumentPrefix.InvoiceDefault, 1),
                estimate.IssueDate,
                estimate.IssueDate.AddDays(30),
                null,
                null,
                null));
    }

    [Fact]
    public void Due_date_cannot_precede_issue_date()
    {
        Assert.Throws<ArgumentException>(() =>
            Invoice.Create(
                1,
                InvoiceNumber.Format(DocumentPrefix.InvoiceDefault, 1),
                Guid.NewGuid(),
                InvoiceClientSnapshot.Capture("Acme", "C001", null),
                new DateOnly(2026, 8, 22),
                new DateOnly(2026, 8, 21),
                null,
                null,
                null,
                0m,
                0m,
                CurrencyCode.Usd));
    }

    [Fact]
    public void Partial_payment_moves_sent_to_partially_paid()
    {
        Invoice invoice = Sent(100m);
        DateOnly today = invoice.IssueDate;
        InvoicePayment payment = invoice.RecordPayment(
            today,
            today,
            40m,
            PaymentMethod.Check,
            "CHK-1",
            "First installment");

        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(40.00m, invoice.AmountPaid);
        Assert.Equal(60.00m, invoice.BalanceDue);
        Assert.True(invoice.CanRecordPayment);
        Assert.False(invoice.CanVoid);
        Assert.False(invoice.CanEdit);
        Assert.Equal(PaymentMethod.Check, payment.Method);
        Assert.Equal("CHK-1", payment.Reference);
        Assert.False(payment.IsReversal);
        Assert.True(InvoiceStatusRules.IsOverdue(invoice.Status, invoice.DueDate, invoice.BalanceDue, today.AddDays(40)));
    }

    [Fact]
    public void Full_payment_marks_the_invoice_paid()
    {
        Invoice invoice = Sent(100m);
        invoice.RecordPayment(invoice.IssueDate, invoice.IssueDate, 40m, PaymentMethod.Cash, null, null);
        invoice.RecordPayment(invoice.IssueDate, invoice.IssueDate, 60m, PaymentMethod.BankTransfer, null, null);

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(100.00m, invoice.AmountPaid);
        Assert.Equal(0m, invoice.BalanceDue);
        Assert.False(invoice.CanRecordPayment);
        Assert.False(invoice.IsOverdue(invoice.DueDate.AddDays(1)));
        Assert.Equal(InvoiceStatus.Paid, invoice.EffectiveStatus(invoice.DueDate.AddDays(1)));
    }

    [Fact]
    public void Exact_balance_payment_from_sent_marks_paid()
    {
        Invoice invoice = Sent(75.50m);
        invoice.RecordPayment(invoice.IssueDate, invoice.IssueDate, 75.50m, PaymentMethod.PayPal, "TX-9", null);

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(75.50m, invoice.AmountPaid);
        Assert.Equal(0m, invoice.BalanceDue);
        Assert.Equal(PaymentMethod.PayPal, invoice.Payments[0].Method);
    }

    [Fact]
    public void Draft_and_void_invoices_cannot_receive_payments()
    {
        Invoice draft = Draft();
        draft.AddLine(null, "Work", 1m, CatalogUnitType.Hour, 50m, false);
        Assert.Throws<InvalidOperationException>(() =>
            draft.RecordPayment(draft.IssueDate, draft.IssueDate, 10m, PaymentMethod.Cash, null, null));

        Invoice voided = Sent(50m);
        voided.Void("Cancelled before payment.");
        Assert.Throws<InvalidOperationException>(() =>
            voided.RecordPayment(voided.IssueDate, voided.IssueDate, 10m, PaymentMethod.Cash, null, null));
    }

    [Fact]
    public void Zero_negative_and_overprecise_amounts_are_rejected()
    {
        Invoice invoice = Sent(50m);
        DateOnly today = invoice.IssueDate;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invoice.RecordPayment(today, today, 0m, PaymentMethod.Cash, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invoice.RecordPayment(today, today, -1m, PaymentMethod.Cash, null, null));
        Assert.Throws<ArgumentException>(() =>
            invoice.RecordPayment(today, today, 1.001m, PaymentMethod.Cash, null, null));
    }

    [Fact]
    public void Community_edition_rejects_overpayments()
    {
        Invoice invoice = Sent(50m);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            invoice.RecordPayment(invoice.IssueDate, invoice.IssueDate, 50.01m, PaymentMethod.Cash, null, null));
        Assert.Contains("overpayment", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(InvoiceStatus.Sent, invoice.Status);
        Assert.Equal(0m, invoice.AmountPaid);
        Assert.Empty(invoice.Payments);
    }

    [Fact]
    public void Payment_date_cannot_be_in_the_future_or_before_issue_date()
    {
        Invoice invoice = Sent(50m);
        DateOnly today = invoice.IssueDate;
        Assert.Throws<ArgumentException>(() =>
            invoice.RecordPayment(today, today.AddDays(1), 10m, PaymentMethod.Cash, null, null));
        Assert.Throws<ArgumentException>(() =>
            invoice.RecordPayment(today, today.AddDays(-1), 10m, PaymentMethod.Cash, null, null));
    }

    [Fact]
    public void Paid_invoice_cannot_receive_another_payment()
    {
        Invoice invoice = Sent(50m);
        invoice.RecordPayment(invoice.IssueDate, invoice.IssueDate, 50m, PaymentMethod.CreditCard, null, null);
        Assert.Throws<InvalidOperationException>(() =>
            invoice.RecordPayment(invoice.IssueDate, invoice.IssueDate, 1m, PaymentMethod.Cash, null, null));
    }

    [Fact]
    public void Invoice_with_recorded_payments_cannot_be_voided()
    {
        Invoice invoice = Sent(50m);
        invoice.RecordPayment(invoice.IssueDate, invoice.IssueDate, 10m, PaymentMethod.Cash, null, null);
        Assert.False(invoice.CanVoid);
        Assert.Throws<InvalidOperationException>(() => invoice.Void("Should not void a billed invoice with payments."));
    }

    [Fact]
    public void Reversal_restores_sent_status_and_keeps_the_original_row()
    {
        Invoice invoice = Sent(100m);
        InvoicePayment payment = invoice.RecordPayment(
            invoice.IssueDate,
            invoice.IssueDate,
            100m,
            PaymentMethod.BankTransfer,
            "WIRE-1",
            "Paid in full");
        InvoicePayment reversal = invoice.ReversePayment(payment.Id, invoice.IssueDate, "Client paid the wrong invoice.");

        Assert.True(reversal.IsReversal);
        Assert.Equal(payment.Id, reversal.ReversesPaymentId);
        Assert.Equal(100m, reversal.Amount);
        Assert.Equal(PaymentMethod.BankTransfer, reversal.Method);
        Assert.Equal("WIRE-1", reversal.Reference);
        Assert.Equal("Client paid the wrong invoice.", reversal.ReversalReason);
        Assert.Equal(InvoiceStatus.Sent, invoice.Status);
        Assert.Equal(0m, invoice.AmountPaid);
        Assert.Equal(100m, invoice.BalanceDue);
        Assert.Equal(2, invoice.Payments.Count);
        Assert.True(invoice.CanRecordPayment);
        Assert.True(invoice.CanVoid);
        Assert.True(invoice.HasReversal(payment.Id));
        Assert.Throws<InvalidOperationException>(() =>
            invoice.ReversePayment(payment.Id, invoice.IssueDate, "Already reversed."));
        Assert.Throws<InvalidOperationException>(() =>
            invoice.ReversePayment(reversal.Id, invoice.IssueDate, "Cannot reverse a reversal."));
    }

    [Fact]
    public void Reversal_of_a_partial_payment_leaves_remaining_receipts()
    {
        Invoice invoice = Sent(100m);
        InvoicePayment first = invoice.RecordPayment(invoice.IssueDate, invoice.IssueDate, 40m, PaymentMethod.Cash, null, null);
        invoice.RecordPayment(invoice.IssueDate, invoice.IssueDate, 25m, PaymentMethod.Check, null, null);
        invoice.ReversePayment(first.Id, invoice.IssueDate, "Cash was recorded twice.");

        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(25m, invoice.AmountPaid);
        Assert.Equal(75m, invoice.BalanceDue);
        Assert.Equal(3, invoice.Payments.Count);
    }

    [Fact]
    public void Duplicate_does_not_copy_payments()
    {
        Invoice invoice = Sent(80m);
        invoice.RecordPayment(invoice.IssueDate, invoice.IssueDate, 80m, PaymentMethod.Cash, null, null);
        Invoice copy = invoice.Duplicate(
            2,
            InvoiceNumber.Format(DocumentPrefix.InvoiceDefault, 2),
            invoice.IssueDate,
            invoice.DueDate);

        Assert.Equal(InvoiceStatus.Draft, copy.Status);
        Assert.Empty(copy.Payments);
        Assert.Equal(0m, copy.AmountPaid);
        Assert.Equal(invoice.Total, copy.BalanceDue);
    }

    [Fact]
    public void Unsupported_payment_method_is_rejected()
    {
        Invoice invoice = Sent(50m);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invoice.RecordPayment(invoice.IssueDate, invoice.IssueDate, 10m, (PaymentMethod)99, null, null));
    }

    private static Invoice Sent(decimal amount)
    {
        Invoice invoice = Draft();
        invoice.AddLine(null, "Work", 1m, CatalogUnitType.Hour, amount, false);
        invoice.MarkSent();
        return invoice;
    }

    private static Invoice Draft(DateOnly? issueDate = null, DateOnly? dueDate = null)
    {
        DateOnly issued = issueDate ?? new DateOnly(2026, 8, 22);
        return Invoice.Create(
            1,
            InvoiceNumber.Format(DocumentPrefix.InvoiceDefault, 1),
            Guid.NewGuid(),
            InvoiceClientSnapshot.Capture("Acme", "C001", "billing@acme.test"),
            issued,
            dueDate ?? issued.AddDays(30),
            null,
            null,
            null,
            0m,
            0m,
            CurrencyCode.Usd);
    }
}
