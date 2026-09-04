# Testing

The solution has three test projects:

| Project | What it covers |
| --- | --- |
| `BillFoundry.Domain.Tests` | Entities, value objects, rounding, status rules |
| `BillFoundry.Application.Tests` | Validators, policies, options defaults |
| `BillFoundry.IntegrationTests` | EF Core, Identity, HTTP, PDFs, CSV, demo seed |

```bash
dotnet test
```

Domain and Application tests do not need a database. SQL Server integration
tests that touch persistence use LocalDB and create an isolated database per
fixture.

PostgreSQL persistence workflows live in `PostgreSqlPersistenceTests`. They
require a reachable Postgres server (`docker compose up db` on host port 5433,
or `BILLFOUNDRY_TEST_POSTGRES`). They skip when that server is not available
unless the environment variable is set (then they fail). Do not use EF Core
InMemory for those tests.

`WebApplicationFactory` tests disable `IdentitySeed` and `DemoSeed` unless a
test turns them on. Authentication tests do not require a database except when
they exercise persistence.

## What is asserted

- Estimate and invoice rounding and totals
- Estimate transitions and invoice lifecycle, including overdue derivation
- Payment recording, settlement, and reversals without overpayment
- Authorization on application services, not only UI
- PDF responses start with `%PDF` and contain expected field text (PdfPig)
- Report CSV exports
- Demo seed creates a bounded fictional dataset and does not duplicate rows
  when reset is off
- Demo Mode forbids organization mutations, password change, and password reset
- Critical persistence workflows on both PostgreSQL and SQL Server: migrate,
  Identity, CRUD, estimate-to-invoice conversion, payments, reporting, audit,
  number uniqueness, and concurrency

## CI

GitHub Actions (`.github/workflows/ci.yml`) builds once on Windows, runs
provider-neutral tests plus SQL Server LocalDB integration tests, and runs
critical PostgreSQL persistence tests on Ubuntu against a Postgres service
container. Treat warnings as errors is already set in `Directory.Build.props`.
