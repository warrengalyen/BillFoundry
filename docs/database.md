# Database

BillFoundry Community uses SQL Server through EF Core. That is the default and
documented engine for self-hosted installs. The public Render demo is a
separate deployment that uses PostgreSQL because Render's managed database is
Postgres. Community Edition is not a generic multi-database product.

The context is `BillFoundryDbContext` in the Infrastructure project. SQL Server
migrations live beside the context. PostgreSQL migrations for the demo live in
`Persistence/Migrations/PostgreSql` and are discovered through
`BillFoundryPostgreSqlDbContext`.

Apply the SQL Server set with:

```bash
dotnet ef database update --project src/BillFoundry.Infrastructure --startup-project src/BillFoundry.Web
```

Apply the PostgreSQL demo set with:

```bash
dotnet ef database update --context BillFoundryPostgreSqlDbContext --project src/BillFoundry.Infrastructure --startup-project src/BillFoundry.Web
```

or set `Database:ApplyMigrationsOnStartup` to `true`. That option applies
pending migrations for the configured provider only. It never drops or
recreates the database.

## Engine

- SQL Server (LocalDB for development and CI, SQL Server 2022 in Compose) is
  the Community default
- PostgreSQL is used only by the hosted public demo on Render (and by
  `compose.demo.postgres.yaml` for local validation of that path)
- Money as `decimal` (typically `decimal(19,4)` for unit prices and
  `decimal(19,2)` for document amounts; `numeric` with the same precision on
  PostgreSQL)
- Optimistic concurrency: SQL Server `rowversion`; PostgreSQL `bytea` tokens
  stamped on save
- Filtered unique indexes where the store needs “at most one” among live rows
  (primary contact, optional SKU)

## Provider selection

`Database:Provider` defaults to `SqlServer`. The Render demo sets
`Database__Provider=PostgreSql`. Selection happens only in Infrastructure
startup. Do not infer the provider from the connection string.

The connection string key is `ConnectionStrings:BillFoundry` for both stores.

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
- Render public demo / local Postgres validation: PostgreSQL when
  `Database:Provider` is `PostgreSql`
- Production Community installs: you supply `ConnectionStrings:BillFoundry` for
  SQL Server

Do not commit production connection strings. Relative logo paths resolve from
the web content root; the default directory is gitignored.
