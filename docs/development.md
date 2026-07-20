# Development

## Prerequisites

- .NET 10 SDK matching `global.json` (`10.0.302` or a later 10.0 feature band)
- SQL Server LocalDB, or another SQL Server instance, when you need the database

The solution file is `BillFoundry.slnx`.

## Build and test

```bash
dotnet build
dotnet test
```

Run the web application:

```bash
dotnet run --project src/BillFoundry.Web
```

HTTPS development URLs are defined in
`src/BillFoundry.Web/Properties/launchSettings.json`.

## Coding standards

Shared MSBuild settings live in `Directory.Build.props`:

- Target framework: `net10.0`
- Language version: C# 14
- Nullable reference types enabled
- Implicit usings enabled
- Warnings treated as errors

Formatting conventions live in `.editorconfig` (file-scoped namespaces, usings
outside the namespace, 4-space indentation for C#).

Prefer:

- async APIs for I/O
- dependency injection
- `TimeProvider` instead of static clock calls
- strongly typed options
- `decimal` for money (when financial types are introduced)
- authorization on the server for privileged actions

Avoid MediatR, CQRS, generic repositories, and extra abstractions that do not
solve a current problem.

## Configuration

Configuration sources:

- `appsettings.json` — non-secret defaults, including logging and database option defaults
- `appsettings.Development.json` — local development values, including a LocalDB connection string
- User Secrets — secrets and non-LocalDB credentials on a developer machine
- Environment variables — secrets and overrides in deployed environments

Set a secret connection string:

```bash
dotnet user-secrets set "ConnectionStrings:BillFoundry" "YOUR_CONNECTION_STRING" --project src/BillFoundry.Web
```

Environment variable form:

```text
ConnectionStrings__BillFoundry
```

Never commit passwords, tokens, or full production connection strings.

Production (non-Development) startup fails if
`ConnectionStrings:BillFoundry` is missing.

## Database

EF Core is registered against SQL Server through `BillFoundryDbContext`. There
are no business entities or migrations yet.

When migrations are introduced:

```bash
dotnet ef migrations add MigrationName --project src/BillFoundry.Infrastructure --startup-project src/BillFoundry.Web
dotnet ef database update --project src/BillFoundry.Infrastructure --startup-project src/BillFoundry.Web
```

The `dotnet-ef` tool is not required for Phase 0.

## Health checks

- `GET /health` — liveness. Does not contact SQL Server.
- `GET /health/ready` — readiness, including an EF Core database check.

Smoke tests use `/health` so they can run without a database.

## Logging and errors

ASP.NET Core logging is configured in appsettings. Production uses the JSON
console formatter; Development uses the simple formatter. Do not log passwords,
secrets, tokens, or unnecessary personal information.

Unhandled exceptions are logged by `GlobalExceptionHandler`. JSON clients receive
ProblemDetails. HTML requests use the Blazor error page.

## Accessibility

WCAG 2.2 AA is a project requirement. New UI must keep semantic landmarks,
keyboard access, and visible focus. The shell includes a skip-to-content link.

## Tests

Test projects live under `tests/`. Integration tests reference the Web project
and use `Microsoft.AspNetCore.Mvc.Testing`. They should not require SQL Server
unless they specifically exercise database behavior.
