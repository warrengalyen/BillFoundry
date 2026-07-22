# BillFoundry

BillFoundry is a freelance invoicing application. The Community Edition is meant
to be genuinely usable for day-to-day invoicing work, not a demo shell, while
also showing modern professional .NET development practices.

Copyright (C) 2026 Warren Galyen

## Technology stack

- .NET 10 LTS
- C# 14
- ASP.NET Core
- Blazor Web App with Interactive Server rendering
- Entity Framework Core 10
- SQL Server
- ASP.NET Core Identity
- xUnit

## Getting started

### Prerequisites

- .NET 10 SDK (see `global.json`)
- SQL Server LocalDB, or another SQL Server instance, for database connectivity

The `/health` liveness check does not require SQL Server. Signing in and the
rest of the application require a configured SQL Server database. The
`/health/ready` endpoint also requires a reachable database.

### Build

```bash
dotnet build
```

### Test

```bash
dotnet test
```

### Run

```bash
dotnet run --project src/BillFoundry.Web
```

By default the HTTPS development profile listens on `https://localhost:7270`.

### Configuration and secrets

Non-secret defaults live in `src/BillFoundry.Web/appsettings.json`. Development
overrides, including a LocalDB connection string, live in
`src/BillFoundry.Web/appsettings.Development.json`.

Do not commit secrets. For credentials or non-LocalDB connection strings, use
.NET User Secrets or environment variables:

```bash
dotnet user-secrets set "ConnectionStrings:BillFoundry" "YOUR_CONNECTION_STRING" --project src/BillFoundry.Web
```

The equivalent environment variable is `ConnectionStrings__BillFoundry`.

See [docs/development.md](docs/development.md) for more detail.

## Documentation

- [Architecture](docs/architecture.md)
- [Development](docs/development.md)
- [Security](docs/security.md)

## License

BillFoundry Community Edition is licensed under the GNU Affero General Public
License v3.0. The full license text is in [LICENSE](LICENSE).

A separate commercial license may be offered in the future for a Pro edition.
That possibility does not change the AGPLv3 terms that apply to this Community
Edition.
