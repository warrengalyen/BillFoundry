# Invoice lifecycle

Invoices are bills to a client. Each invoice is an aggregate with header
fields, status, persisted totals, billed-to identity, and ordered line
snapshots. This document is the durable description of Community Edition
invoice behavior. Payment recording is described in [payments.md](payments.md).
PDF generation is not implemented yet.

## Aggregate

An invoice stores:

- A generated public number such as `INV-0001`
- The client identifier plus a **client snapshot** (name, code, email)
- Issue date and required due date
- Status
- Optional purchase order / reference
- Notes and payment instructions
- Document-level discount and tax rate
- Persisted subtotal, taxable subtotal, tax, total, amount paid, and balance due
- Currency snapshotted from the organization when the invoice is created
- Optional `SourceEstimateId` when the invoice was converted from an estimate
- Optional void reason
- Ordered **payments** (receipts and reversals); see [payments.md](payments.md)
- Audit metadata and a SQL Server `rowversion` concurrency token

Lines store a historical snapshot:

- Description
- Quantity (four decimal places)
- Unit (`Hour`, `Day`, `Item`, or `FlatFee`)
- Unit price (four decimal places)
- Taxable flag
- Sort order
- Persisted line amount (two decimal places)

An optional `CatalogItemId` records which catalog item was copied. Catalog
prices are never re-read after the line is saved. Later catalog edits do not
change existing invoices. Ad-hoc lines (no catalog item) are allowed.

The client snapshot is copied at create, duplicate, header client change, and
estimate conversion. Later client name, code, or email edits do not rewrite
historical billed-to identity.

New invoices can only be created for an **active** client. Existing invoices
keep their client if that client is later deactivated. Invoices are not
deleted. Voiding is the way to cancel a document while keeping the number and
history.

## Status

Initial statuses:

| Status | Meaning |
| --- | --- |
| Draft | Editable working copy |
| Sent | Issued to the client |
| PartiallyPaid | Issued bill with a partial net amount paid |
| Paid | Issued bill with net amount paid equal to total |
| Overdue | Derived display status; not stored as a replacement for Sent |
| Void | Cancelled with a required reason |

Valid stored transitions:

- Draft → Sent, Void
- Sent → Void (only when net amount paid is zero)
- Sent → PartiallyPaid or Paid when a payment is recorded
- PartiallyPaid → Paid when remaining balance is received
- PartiallyPaid or Paid → Sent when reversals bring net amount paid to zero
- Paid, Void → none through user-facing actions

User-facing `CanTransition` does not include paid statuses. Those changes are
payment-driven. See [payments.md](payments.md).

A draft may be sent only when it has at least one line. There is no Sent →
Draft recall. Sent invoices are financial documents; they are not edited.

**Editing is allowed only while the invoice is Draft.** Sent, paid, overdue
(display), and void invoices cannot have header or line changes.

Void requires a non-empty reason. An invoice with recorded payments cannot be
voided. Voiding keeps totals and sets balance due to zero.

`PartiallyPaid` and `Paid` are stored when payments are recorded. Amount paid
is the net of receipts minus reversals. Community Edition does not allow
overpayments.

## Overdue

Overdue is computed, not persisted as the workflow status.

An invoice is overdue when:

- Stored status is `Sent` or `PartiallyPaid`
- Due date is earlier than today from `TimeProvider`
- Balance due is greater than zero

`EffectiveStatus` returns `Overdue` for display and list filters. The stored
status remains `Sent` (or `PartiallyPaid`) so payment recording does not
lose history. The Sent list filter excludes invoices that are currently
overdue so the two views are disjoint.

Reading an overdue invoice does not write `Overdue` to the database.

## Number generation

Invoice numbers come from a `DocumentSequences` row (`Kind = Invoice`) plus the
organization invoice prefix. Estimate numbers use a separate `Kind = Estimate`
row. Both allocations use the same locked-sequence helper.

1. Create, duplicate, and estimate conversion run inside a database transaction.
2. The sequence row is selected with `UPDLOCK, HOLDLOCK`.
3. `NextValue` is consumed and incremented.
4. The public number is `{prefix}-{sequence:D4}` (`INV-0001`, then `INV-0002`,
   and `INV-10000` once the value exceeds four digits).
5. `Invoices.Number` and `Invoices.Sequence` are unique.

Prefix is read from organization settings at create, duplicate, or conversion
time. Changing the prefix later does not rewrite existing numbers.

A failed transaction rolls back sequence consumption.

## Rounding and totals

Invoice math uses `InvoiceCalculator`, which delegates line, discount, and tax
rounding to `DocumentCalculator` / `MoneyRounding` so estimates and invoices
stay consistent. Razor components display results; they do not implement the
formulas.

| Value | Scale | Midpoint |
| --- | --- | --- |
| Line amount, discount, taxable subtotal, tax, total, amount paid, balance due | 2 decimal places | Away from zero (`0.005` → `0.01`) |
| Quantity, unit price, tax rate percent | 4 decimal places | Away from zero |

Calculation order:

1. Line amount = round(quantity × unit price, 2)
2. Subtotal = sum of line amounts
3. Discount is a document-level amount, `0 ≤ discount ≤ subtotal`, two decimal places
4. Taxable subtotal = if subtotal is 0 then 0, else round(taxable line sum × (subtotal − discount) / subtotal, 2)
5. Tax = round(taxable subtotal × tax rate percent / 100, 2)
6. Total = subtotal − discount + tax
7. Amount paid is the net of recorded payments minus reversals (see
   [payments.md](payments.md))
8. Balance due = 0 when void; otherwise total − amount paid

Discount is allocated proportionally across taxable lines. Non-taxable lines
never generate tax. New invoices start at 0% tax unless the operator sets a
rate (including rates copied from a converted estimate). Removing lines that
would leave discount above the new subtotal clamps discount down to that
subtotal. Setting a header discount above the current subtotal is rejected.

Totals are recalculated whenever the draft changes and are persisted on the
invoice for listing and detail views.

## Estimate conversion

Only an **Accepted** estimate can be converted, and only through
`IInvoiceService.ConvertFromEstimateAsync`. Direct `Converted` transitions on
`IEstimateService` are rejected.

Conversion is transactional:

1. Reject if the estimate is already converted or an invoice already points at
   it (`SourceEstimateId` unique filtered index).
2. Lock and allocate the next invoice number.
3. Copy currency, discount, tax rate, notes (unless overridden), and every line
   snapshot onto a new **Draft** invoice.
4. Store `SourceEstimateId`.
5. Mark the estimate `Converted`.
6. Commit both documents together.

The converted invoice starts as Draft so the operator can review issue date,
due date, purchase order, notes, and payment instructions before sending.
Duplicate conversion is prevented by the estimate status check, an existing
invoice lookup, and the unique index. A failed conversion rolls back the
invoice number and leaves the estimate Accepted.

Conversion is allowed even if the client is later inactive; billed-to identity
is copied from the current client row at conversion time.

Duplicating an invoice allocates a new number and does **not** copy
`SourceEstimateId` or payments.

## Workflows

Authorized Administrator or User roles (`ManageInvoices`) can:

- Create a draft (number allocated atomically)
- Edit the header while Draft
- Add, edit, remove, and reorder lines while Draft. Reorder writes a temporary
  unique sort order, saves, then writes the final 0-based order so SQL Server's
  unique `(InvoiceId, SortOrder)` index can accept swaps
- View detail for any status
- Duplicate any invoice into a new Draft with copied snapshots and a new number
  when the original client is still active
- Mark Draft as Sent
- Record and reverse received payments on Sent or PartiallyPaid invoices (see
  [payments.md](payments.md))
- Void Draft or Sent with a required reason when net amount paid is zero
- Convert an accepted estimate (from the estimate page)
- Search, filter, sort, and page the invoice list on the server (number, client
  snapshot name/code, purchase order, notes, client, status including overdue,
  issue-date range, due-date range, and total range)

Optimistic concurrency uses the invoice `rowversion` for header, line, send,
void, payment, and reversal saves. Estimate conversion also uses the estimate
`rowversion`. Conflicting saves return the current document and ask the
operator to review it.

Create, duplicate, conversion, and number allocation use transactions so a
failed save does not consume a sequence value. Recording or reversing a
payment updates invoice totals and status in the same save as the payment
row.
