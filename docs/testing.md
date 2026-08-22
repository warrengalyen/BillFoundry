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

Domain and Application tests do not need SQL Server. Integration tests that
touch persistence use SQL Server LocalDB and create an isolated database per
fixture.

PostgreSQL demo workflows live in `PostgreSqlDemoPersistenceTests`. They
require a reachable Postgres server (`compose.demo.postgres.yaml` or
`BILLFOUNDRY_TEST_POSTGRES`). They skip when that server is not available.
Do not use EF Core InMemory for those tests.

`WebApplicationFactory` tests disable `IdentitySeed` and `DemoSeed` unless a
test turns them on. Authentication tests do not require SQL Server except when
they exercise the database.

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

## CI

GitHub Actions (`.github/workflows/ci.yml`) runs a Windows job so LocalDB is
available for SQL Server integration tests (PostgreSQL demo tests are filtered
out there). A second Ubuntu job runs Domain/Application tests plus the
PostgreSQL demo workflows against a Postgres service container. Treat warnings
as errors is already set in `Directory.Build.props`.
