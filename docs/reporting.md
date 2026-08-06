# Reporting

BillFoundry Community Edition reports on stored invoice balances and payment
rows. Catalog prices and organization defaults are not recalculated at report
time. Calendar "today" is the UTC date from `TimeProvider`.

## Authorization

`IReportingService` requires `ManageInvoices`. Dashboard metrics, report pages,
and CSV endpoints use the same policy. CSV download is a read, so Demo Mode
does not block it. Responses include only a Content-Disposition file name.

## Metrics

Open receivables are invoices in `Sent` or `PartiallyPaid` with `BalanceDue`
greater than zero. Draft, paid, and void invoices are excluded.

| Metric | Definition |
| --- | --- |
| Outstanding receivables | Sum of `BalanceDue` on open invoices |
| Overdue receivables | Same set, due date earlier than today |
| Open invoice count | Count of that set |
| Overdue invoice count | Count of overdue invoices in that set |
| Payments this month / year | Signed payment amounts (`Amount` or `-Amount` for reversals) whose `PaymentDate` falls in the UTC calendar month or year through today |

## Reports

- **Aging** uses due date relative to an as-of date (default today). Current
  includes due today. Buckets are 1-30, 31-60, 61-90, and 90+ (91 days or
  more). Day 90 is the last day of 61-90.
- **Payments by month** nets receipts and reversals by `PaymentDate` year and
  month. A reversal is counted in the month of its own `PaymentDate` (the UTC
  date when it was recorded), not the original receipt month. Default range is
  1 January of the current UTC year through today. Optional `ClientId` is
  applied in SQL.
- **Revenue by client** uses the same payment date range and groups by invoice
  `ClientId` plus the billed-to snapshot name/code.
- **Outstanding invoices** lists the open set. Optional From/To filter due
  date. Days overdue are computed from the as-of date.
- **Payment history** lists receipt and reversal rows in the payment date
  range. Reversal amounts are negative.

Report pages use native GET inputs (`name` matching query parameters) so date
and client filters round-trip in the URL without an interactive circuit.

Community Edition dashboard totals use the organization default currency.
Open invoices in other currencies are included in the same figures; they are
not split by currency.

## Query and index design

Aggregates use `IQueryable` projections (`Sum`, `Count`, `GroupBy`). List
reports project column sets and cap at 10,000 rows. They do not load invoice
line collections to compute totals.

Existing indexes on `Invoices.Status`, `Invoices.DueDate`, and
`InvoicePayments (InvoiceId, PaymentDate)` support filters. Reporting adds:

- `IX_Invoices_Status_DueDate` on `(Status, DueDate)` including `BalanceDue`
  for open/overdue/aging scans
- `IX_InvoicePayments_PaymentDate` on `PaymentDate` for month/year receipts

Inspect generated SQL with EF Core `ToQueryString()` in reporting tests.
Typical dashboard SQL contains `SUM` over `BalanceDue` and does not join
`InvoiceLines`.

## CSV

Exports are UTF-8 with a BOM. Dates are `yyyy-MM-dd`. Amounts are invariant
`0.00`. Cells that look like spreadsheet formulas (`=`, `+`, `@`, or a
non-numeric leading `-`) are prefixed with a single quote. File names are
`billfoundry-{report}-{yyyyMMdd}.csv` with no path characters.

## Charts

Aging and monthly payments render a CSS bar chart marked `aria-hidden="true"`.
The same figures appear in a data table on the same page.
