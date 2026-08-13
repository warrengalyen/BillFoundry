# Architecture

BillFoundry is a modular monolith. Features share one ASP.NET Core process and
one SQL Server database. Project boundaries exist so the Community Edition stays
maintainable and so a later Pro edition can extend the same core without
introducing microservices or a mediator pipeline for demonstration.

## Solution layout

```
src/
  BillFoundry.Domain
  BillFoundry.Application
  BillFoundry.Infrastructure
  BillFoundry.Web
tests/
  BillFoundry.Domain.Tests
  BillFoundry.Application.Tests
  BillFoundry.IntegrationTests
```

## Project responsibilities

- **Domain** contains business entities, enums, value objects, and domain rules.
  It has no BillFoundry project dependencies and no EF Core or ASP.NET packages.
- **Application** contains use cases, application services, and contracts.
  It references Domain only.
- **Infrastructure** contains EF Core, persistence, and other external
  implementations. It references Application and Domain.
- **Web** contains the Blazor UI and the composition root. It references
  Application and Infrastructure so it can register services at startup.

```
BillFoundry.Web --> BillFoundry.Application
BillFoundry.Web --> BillFoundry.Infrastructure
BillFoundry.Application --> BillFoundry.Domain
BillFoundry.Infrastructure --> BillFoundry.Application
BillFoundry.Infrastructure --> BillFoundry.Domain
```

Web may reference Infrastructure only to compose the application. Razor
components must not contain business rules; they delegate meaningful work to
application services.

## Persistence

EF Core is used directly. Repository abstractions over `DbContext` are not
introduced unless a concrete requirement appears.

`BillFoundryDbContext` is an Identity `DbContext` (`ApplicationUser` and
`IdentityRole<Guid>`). It also stores the Community Edition organization
profile. Configurations are applied from the Infrastructure assembly.
The Identity schema is the `AddIdentity` migration. Organization columns are
the `AddOrganization` migration. Clients and contacts are the `AddClients`
migration. The service catalog is the `AddCatalogItems` migration. Estimates,
estimate lines, and document sequences are the `AddEstimates` migration.

Community Edition has one organization per installation. The `Organization`
entity uses a well-known singleton identifier. There is no tenant identifier
and no multi-organization support.

Clients belong to that single installation. They are deactivated rather than
permanently deleted so later invoices and other financial records can keep a
stable reference. Contacts belong to a client. A filtered unique index allows
at most one primary contact per client.

The service catalog stores reusable billable items with a strongly typed unit
(`Hour`, `Day`, `Item`, or `FlatFee`), a `decimal(19,4)` default unit price, and
a taxable flag. Community Edition prices use the organization's default currency;
catalog items do not store a per-item currency. Items are deactivated rather than
permanently deleted so later financial documents can keep a stable reference.
An optional SKU is unique when present.

Estimates are an aggregate of header fields and ordered line snapshots. Each
estimate has a generated public number, a client, issue and optional expiration
dates, status, notes, terms, a document-level discount and tax rate, persisted
totals, and a currency snapshotted from the organization at create time.
Line items copy description, quantity, unit, unit price, and taxable status;
later catalog price changes do not rewrite saved estimates. Number allocation
uses a locked `DocumentSequences` row inside a transaction. See
[estimates.md](estimates.md). The `AddEstimates` migration adds
`DocumentSequences`, `Estimates`, and `EstimateLines`. Invoices, invoice lines,
and the invoice sequence seed are the `AddInvoices` migration. Invoice payments
are the `AddInvoicePayments` migration. Reporting indexes are the
`AddReportingIndexes` migration. Business audit events are the `AddAuditEvents`
migration. See [invoice-lifecycle.md](invoice-lifecycle.md),
[payments.md](payments.md), [reporting.md](reporting.md), and
[audit.md](audit.md).

Invoices store billed-to identity as a client snapshot, required issue and due
dates, status, optional purchase order, notes, payment instructions, persisted
totals including amount paid and balance due, optional `SourceEstimateId`, and
payment receipts with linked reversals. Line snapshots follow the same
historical-value rules as estimates. Overdue is derived from due date,
outstanding balance, and `TimeProvider`; it is not stored as a replacement for
Sent. Invoices and payments are not deleted; invoices are voided and payments
are reversed. See [payments.md](payments.md). US Letter PDF invoices and
estimates are generated in memory from persisted values; see [pdf.md](pdf.md).

Postal address, currency, document prefixes, and logo metadata are modeled as
value objects. Logo bytes are not stored in SQL Server; metadata points at an
`IOrganizationLogoStore` implementation. The default store writes generated
file names under `OrganizationLogoStorage:RootPath` (relative paths are
resolved from the web content root). Submitted upload names are never used.

Organization updates use SQL Server rowversion optimistic concurrency.
Client profile and contact changes use the same rowversion token on `Client`.
Catalog item edits use a rowversion token on `CatalogItem`.
Estimate header, line, and status changes use a rowversion token on `Estimate`.
Invoice header, line, send, void, payment, reversal, and conversion changes use
a rowversion token on `Invoice` (conversion also uses the estimate token).

PDF generation lives in Infrastructure (`PDFsharp`). Application defines
`IInvoiceDocumentGenerator`, `IEstimateDocumentGenerator`, and document
download services. Downloads are authorized with `ManageInvoices` or
`ManageEstimates`. Physical storage paths are never returned. See
[pdf.md](pdf.md).

Migrations live in Infrastructure. Generate and apply them with Web as the
startup project.

```bash
dotnet ef migrations add MigrationName --project src/BillFoundry.Infrastructure --startup-project src/BillFoundry.Web
dotnet ef database update --project src/BillFoundry.Infrastructure --startup-project src/BillFoundry.Web
```

## Configuration and time

Strongly typed `DatabaseOptions` bind database command timeout settings.
The SQL Server connection string uses the standard
`ConnectionStrings:BillFoundry` key.

Application code should use `TimeProvider` rather than `DateTime.Now` or
`DateTime.UtcNow`. `TimeProvider.System` is registered in Application DI.
`ICurrentUser` is the Application identity abstraction; the Web host supplies the
HTTP implementation. See [security.md](security.md).

## Hosting concerns

`BillFoundry.Web` is the composition root. `Program.cs` registers Application
and Infrastructure services, Identity cookies, Razor components with Interactive
Server support, ProblemDetails, a centralized `IExceptionHandler`, logging, and
health checks.

- `/health` is a liveness probe, allows anonymous access, and does not require SQL Server.
- `/health/ready` includes an EF Core database check and allows anonymous access.
- Optional `Database:ApplyMigrationsOnStartup` applies pending EF Core migrations
  at process start. It never drops the database. Default is false.
- Optional `DataProtection:KeyPath` persists the ASP.NET Core key ring so
  authentication cookies survive container replacement.
- `SecurityHeadersMiddleware` adds `X-Content-Type-Options`, `X-Frame-Options`,
  `Referrer-Policy`, `Permissions-Policy`, and `Content-Security-Policy`.
- `ForwardedHeaders:Enabled` is off by default. Enable it only behind a reverse
  proxy that overwrites `X-Forwarded-*` headers. See [deployment.md](deployment.md).

Interactive Server is enabled per component, not globally. Identity account pages
use static SSR. The shell navigation is interactive so the mobile menu can work.

Application pages require authentication unless marked `[AllowAnonymous]`.

## Testing

- Domain and Application tests cover rules, identity abstractions, authorization policies,
  organization validation, logo content inspection, client/contact invariants,
  catalog pricing rules, estimate rounding, estimate status transitions,
  invoice rounding, overdue derivation, invoice lifecycle, and payment integrity.
- Integration tests use `WebApplicationFactory` against the Web host.
- Authentication tests disable development identity seeding and do not require SQL Server
  except when a test explicitly exercises the database.
- Organization, client, catalog, estimate, invoice, payment, and reporting
  persistence and aggregation tests require SQL Server LocalDB.
