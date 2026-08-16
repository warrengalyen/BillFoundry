# Database

BillFoundry uses SQL Server through EF Core. The context is
`BillFoundryDbContext` in the Infrastructure project. Migrations live beside
the context. Apply them with:

```bash
dotnet ef database update --project src/BillFoundry.Infrastructure --startup-project src/BillFoundry.Web
```

or set `Database:ApplyMigrationsOnStartup` to `true`. That option applies
pending migrations only. It never drops or recreates the database.

## Engine

- SQL Server (LocalDB for development and CI, SQL Server 2022 in Compose)
- Money as `decimal` (typically `decimal(19,4)` for unit prices and
  `decimal(19,2)` for document amounts)
- `rowversion` concurrency on organization, clients, catalog items, estimates,
  and invoices
- Filtered unique indexes where SQL Server needs “at most one” among live rows
  (primary contact, optional SKU)

## Main tables

| Area | Tables |
| --- | --- |
| Identity | ASP.NET Core Identity tables for `ApplicationUser` and roles |
| Organization | `Organizations` (singleton row) |
| Clients | `Clients`, `ClientContacts` |
| Catalog | `CatalogItems` |
| Sequences | `DocumentSequences` |
| Estimates | `Estimates`, `EstimateLines` |
| Invoices | `Invoices`, `InvoiceLines`, `InvoicePayments` |
| Audit | `AuditEvents` |

Configurations are explicit `IEntityTypeConfiguration` classes. Sequences for
estimates and invoices are seeded so allocation can lock a row.

## Environments

- Development: LocalDB database `BillFoundry` unless you override the
  connection string
- Integration tests: LocalDB `BillFoundry_IT_{guid}`, created and dropped by
  the test fixture
- Compose: SQL Server database `BillFoundry` on the `db` service
- Production: you supply `ConnectionStrings:BillFoundry`

Do not commit production connection strings. Relative logo paths resolve from
the web content root; the default directory is gitignored.
