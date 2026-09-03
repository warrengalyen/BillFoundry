# Domain model

Community Edition models one installation and one organization. There is no
tenant key and no multi-organization support.

## Organization

A singleton `Organization` holds legal and display names, postal address,
contact details, default currency, payment terms, document prefixes, default
notes, payment instructions, and optional logo metadata. Logo bytes live on
disk, not in the database. Updates use an optimistic concurrency token.

## Identity

`ApplicationUser` extends ASP.NET Core Identity with an `IsDisabled` flag.
Roles are `Administrator` and `User`. Disabled accounts cannot sign in.

## Clients and contacts

A `Client` has a public code, name, optional contact details, address, notes,
and an active flag. Clients are deactivated rather than deleted.

`ClientContact` belongs to a client. At most one contact per client is primary
(filtered unique index).

## Catalog

`CatalogItem` is a reusable billable row: name, description, optional SKU,
unit (`Hour`, `Day`, `Item`, `FlatFee`), default unit price, taxable flag, and
active flag. SKU is unique when present. Items are deactivated, not deleted.

## Document sequences

`DocumentSequence` rows (`Estimate`, `Invoice`) allocate the next public
number under a transaction lock. Community Edition formats numbers with the
organization prefix (for example `EST-0001`, `INV-0001`).

## Estimates

An `Estimate` is an aggregate: public number, client, dates, status, notes,
terms, discount, tax rate, persisted totals, currency, and ordered line
snapshots. Status values are Draft, Sent, Accepted, Declined, Expired, and
Converted. Accepted estimates convert to invoices through the invoice service.
See [estimates.md](estimates.md).

## Invoices and payments

An `Invoice` stores billed-to identity as a client snapshot, dates, status,
optional purchase order, notes, payment instructions, persisted totals
including amount paid and balance due, optional source estimate, and payments.
Overdue is derived from due date, outstanding balance, and `TimeProvider`.
Invoices are voided rather than deleted. Payments can be reversed. See
[invoice-lifecycle.md](invoice-lifecycle.md) and [payments.md](payments.md).

## Audit

Entities that implement `IAuditable` receive created/updated timestamps and
user ids. Business history is a separate append-only `AuditEvent` table. See
[audit.md](audit.md).
