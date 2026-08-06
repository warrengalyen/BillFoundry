# Payments

Payments are externally received amounts recorded against an invoice. Community
Edition does not connect to a payment processor. Operators enter cash, check,
bank transfer, card, PayPal, or other receipts that already happened outside
BillFoundry.

This document is the durable description of Community Edition payment behavior.
See [invoice-lifecycle.md](invoice-lifecycle.md) for invoice status, totals, and
voiding.

## Financial records

A payment stores:

- Invoice identifier
- Payment date
- Positive amount (two decimal places)
- Method (`Cash`, `Check`, `BankTransfer`, `CreditCard`, `PayPal`, `Other`)
- Optional reference / transaction number
- Optional notes
- Created audit metadata (`CreatedAtUtc`, `CreatedByUserId`)
- Optional reversal metadata (`ReversesPaymentId`, `ReversalReason`)

Payments belong to the invoice aggregate. They are not a separate bounded
context and are not deleted. Correct a mistake by reversing the receipt. The
original row remains in the history.

Amount is always stored as a positive number. A reversal is a second row that
points at the original receipt and copies its amount, method, and reference.
The reversal date is today from `TimeProvider`. A reversal cannot be reversed;
record a new payment instead. Each receipt can have at most one reversal.

## When a payment can be recorded

A payment can be recorded only when:

- Stored status is `Sent` or `PartiallyPaid`
- Balance due is greater than zero
- The invoice is not Draft or Void
- Amount is greater than zero, has at most two decimal places, and does not
  exceed the current balance due
- Payment date is not in the future and is not earlier than the invoice issue
  date
- Method is one of the supported values

Draft invoices are working copies, not bills. Void invoices are cancelled
history. Paid invoices have no remaining balance. Community Edition rejects
overpayments rather than creating credit balances.

Recording and reversing payments does not re-open header or line editing.
Sent, partially paid, and paid invoices stay financially locked.

## Amount paid and balance due

`AmountPaid` is the net of receipts minus reversals, floored at zero.
`BalanceDue` is `Total − AmountPaid`, or zero when the invoice is void.

Totals are recalculated from the loaded payment collection whenever a payment
is recorded or reversed, then persisted on the invoice with the new payment
row in the same `SaveChanges` call.

## Status after settlement

User-facing `CanTransition` still does not include Sent → PartiallyPaid or
Sent → Paid. Settlement is payment-driven:

| Net amount paid | Stored status |
| --- | --- |
| `0` | `Sent` |
| Greater than `0` and less than total | `PartiallyPaid` |
| Equal to total | `Paid` |

Draft and Void are not changed by payment operations. After every receipt on
an invoice is reversed, stored status returns to `Sent` (not Draft) so the
document remains an issued bill.

Overdue stays a derived display status: `Sent` or `PartiallyPaid`, due date
earlier than today, and balance due greater than zero. A paid invoice is not
overdue.

An invoice with a positive net amount paid cannot be voided. After a full
reversal, voiding a Sent invoice is allowed again.

## Corrections

There is no delete action for payments.

To correct a receipt:

1. Reverse the original payment with a required reason.
2. If money was still received, record a new payment for the correct amount,
   date, method, or reference.

The history shows both the original row and the linked reversal. Invoice
detail does not allow reversing a reversal or reversing the same receipt
twice. The database also has a unique filtered index on `ReversesPaymentId`.

## Concurrency and transactions

Payment recording and reversal use the invoice `rowversion`. The payment insert
and the invoice amount/status update commit together. A stale token returns a
concurrency conflict and does not insert a payment.

Concurrent full-balance attempts against the same token allow one winner. The
loser must reload and, if a balance remains, record a new payment. That
prevents two receipts from overpaying the invoice.

## Duplication

Duplicating an invoice copies line snapshots and header fields into a new
Draft. It does not copy payments, amount paid, or paid status.

## Authorization

`IInvoiceService.RecordPaymentAsync` and `ReversePaymentAsync` require the
`ManageInvoices` policy (Administrator or User). Invoice detail shows payment
history for authorized operators. Authorization is enforced in the service, not
only in the UI. Dashboard payment totals and payment reports use the same
policy; see [reporting.md](reporting.md).
