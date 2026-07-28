# Estimate lifecycle

Estimates are priced offers to a client. Each estimate is an aggregate with
header fields, status, persisted totals, and ordered line snapshots. This
document is the durable description of Community Edition estimate behavior.
Estimate-to-invoice conversion is modeled in status only; it is not implemented
in this phase.

## Aggregate

An estimate stores:

- A generated public number such as `EST-0001`
- The client
- Issue date and optional expiration date
- Status
- Notes and terms
- Document-level discount and tax rate
- Persisted subtotal, taxable subtotal, tax, and total
- Currency snapshotted from the organization when the estimate is created
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
change existing estimates. Ad-hoc lines (no catalog item) are allowed.

New estimates can only be created for an **active** client. Existing estimates
keep their client if that client is later deactivated. Estimates are not
deleted.

## Status

Initial statuses:

| Status | Meaning |
| --- | --- |
| Draft | Editable working copy |
| Sent | Issued to the client |
| Accepted | Client accepted the offer |
| Declined | Client declined; may be reopened |
| Expired | Explicitly marked expired; may be reopened |
| Converted | Reserved for later invoice conversion |

Valid transitions:

- Draft → Sent, Declined
- Sent → Draft (recall), Accepted, Declined, Expired
- Accepted → Converted (domain only; the application does not expose conversion yet)
- Declined → Draft
- Expired → Draft
- Converted → none

A draft may be sent only when it has at least one line. Expiration is an
explicit transition. Reading an estimate after its expiration date does not
change status.

**Editing is allowed only while the estimate is Draft.** Accepted and converted
estimates cannot be edited. Sent, declined, and expired estimates must be
returned to Draft before header or line changes.

## Number generation

Estimate numbers come from a `DocumentSequences` row (`Kind = Estimate`) plus
the organization estimate prefix.

1. Create and duplicate run inside a database transaction.
2. The sequence row is selected with `UPDLOCK, HOLDLOCK`.
3. `NextValue` is consumed and incremented.
4. The public number is `{prefix}-{sequence:D4}` (`EST-0001`, then `EST-0002`,
   and `EST-10000` once the value exceeds four digits).
5. `Estimates.Number` and `Estimates.Sequence` are unique.

Prefix is read from organization settings at create/duplicate time. Changing
the prefix later does not rewrite existing numbers.

## Rounding and totals

All financial math lives in `EstimateCalculator` / `MoneyRounding`. Razor
components display results; they do not implement the formulas.

| Value | Scale | Midpoint |
| --- | --- | --- |
| Line amount, discount, taxable subtotal, tax, total | 2 decimal places | Away from zero (`0.005` → `0.01`) |
| Quantity, unit price, tax rate percent | 4 decimal places | Away from zero |

Calculation order:

1. Line amount = round(quantity × unit price, 2)
2. Subtotal = sum of line amounts
3. Discount is a document-level amount, `0 ≤ discount ≤ subtotal`, two decimal places
4. Taxable subtotal = if subtotal is 0 then 0, else round(taxable line sum × (subtotal − discount) / subtotal, 2)
5. Tax = round(taxable subtotal × tax rate percent / 100, 2)
6. Total = subtotal − discount + tax

Discount is allocated proportionally across taxable lines. Non-taxable lines
never generate tax. The organization has no default tax rate; new estimates
start at 0%. Removing lines that would leave discount above the new subtotal
clamps discount down to that subtotal. Setting a header discount above the
current subtotal is rejected.

Totals are recalculated whenever the draft changes and are persisted on the
estimate for listing and detail views.

## Workflows

Authorized Administrator or User roles (`ManageEstimates`) can:

- Create a draft (number allocated atomically)
- Edit the header while Draft
Add, edit, remove, and reorder lines while Draft. Reorder writes a temporary
unique sort order, saves, then writes the final 0-based order so SQL Server's
unique `(EstimateId, SortOrder)` index can accept swaps.
- View detail for any status
- Duplicate any estimate into a new Draft with copied snapshots and a new number
- Apply the user-facing status transitions above
- Search, filter, sort, and page the estimate list on the server (number, client
  name, notes, status)

Optimistic concurrency uses the estimate `rowversion`. Conflicting saves return
the current document and ask the operator to review it.

Create, duplicate, and number allocation use transactions so a failed save does
not consume a sequence value.
