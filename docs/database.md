# Database

BillFoundry Community supports PostgreSQL and SQL Server through Entity
Framework Core. PostgreSQL is the default for new Community installs.
SQL Server is a fully supported alternative. The public hosted demo runs
PostgreSQL on Render; that environment does not switch providers.

The runtime context is `BillFoundryDbContext` in the Infrastructure project.
SQL Server migrations live beside the context. PostgreSQL migrations live in
`Persistence/Migrations/PostgreSql` and are discovered through
`BillFoundryPostgreSqlDbContext`. Do not generate one provider's migrations
into the other set.

Apply the PostgreSQL set (Community default) with:

```bash
dotnet ef database update --context BillFoundryPostgreSqlDbContext --project src/BillFoundry.Infrastructure --startup-project src/BillFoundry.Web
```

Apply the SQL Server set with:

```bash
dotnet ef database update --project src/BillFoundry.Infrastructure --startup-project src/BillFoundry.Web
```

or set `Database:ApplyMigrationsOnStartup` to `true`. That option applies
pending migrations for the configured provider only. It never drops or
recreates the database.

## Engine

- PostgreSQL is the Community default (Compose `postgres:16`, development
  connection on `localhost:5432`)
- SQL Server remains fully supported (LocalDB for Windows CI and optional
  local work, SQL Server 2022 via `compose.sqlserver.yaml`)
- Money as `decimal` (typically `decimal(19,4)` for unit prices and
  `decimal(19,2)` for document amounts; `numeric` with the same precision on
  PostgreSQL)
- Optimistic concurrency: SQL Server `rowversion`; PostgreSQL `bytea` tokens
  stamped on save
- Filtered unique indexes where the store needs “at most one” among live rows
  (primary contact, optional SKU)

Financial totals use the same decimal types and rounding rules on both
providers. Document numbers are allocated from a locked `DocumentSequences`
row (`UPDLOCK, HOLDLOCK` on SQL Server, `FOR UPDATE` on PostgreSQL).

## Provider selection

`Database:Provider` defaults to `PostgreSql`. Set `SqlServer` when you want
the Microsoft database stack. The Render demo sets `Database__Provider=PostgreSql`
explicitly. Selection happens only in Infrastructure startup. Do not infer
the provider from the connection string.

The connection string key is `ConnectionStrings:BillFoundry` for both stores.

PostgreSQL:

```text
Database__Provider=PostgreSql
ConnectionStrings__BillFoundry=Host=127.0.0.1;Port=5432;Database=billfoundry;Username=billfoundry;Password=DevOnly_P@ssw0rd
```

SQL Server:

```text
Database__Provider=SqlServer
ConnectionStrings__BillFoundry=Server=(localdb)\mssqllocaldb;Database=BillFoundry;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

Those Development/Compose passwords are placeholders. Do not commit production
credentials.

## Creating migrations

PostgreSQL (requires `--context` and `--output-dir`):

```bash
dotnet ef migrations add MigrationName --context BillFoundryPostgreSqlDbContext --output-dir Persistence/Migrations/PostgreSql --project src/BillFoundry.Infrastructure --startup-project src/BillFoundry.Web
```

SQL Server (default context, default output directory):

```bash
dotnet ef migrations add MigrationName --project src/BillFoundry.Infrastructure --startup-project src/BillFoundry.Web
```

Design-time factories keep each command on the matching provider so a
PostgreSQL migration is not written into the SQL Server history, and vice versa.

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

- Development: PostgreSQL database `billfoundry` on `localhost:5432` unless you
  override the connection string and provider
- Integration tests: PostgreSQL `billfoundry_it_{guid}` when a server is
  reachable; SQL Server LocalDB `BillFoundry_IT_{guid}` on Windows CI
- Compose: PostgreSQL `billfoundry` on the `db` service (`docker compose up`)
- Compose SQL Server overlay: database `BillFoundry` on the `db` service
- Render public demo: PostgreSQL only (`Database__Provider=PostgreSql`)
- Production Community installs: supply `ConnectionStrings:BillFoundry` and
  `Database:Provider` for the engine you chose

Do not commit production connection strings. Relative logo paths resolve from
the web content root; the default directory is gitignored.
