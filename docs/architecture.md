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

`BillFoundryDbContext` currently has no business entity mappings. Configurations
are applied from the Infrastructure assembly so future entity configurations can
be added as explicit, readable classes.

Migrations are not created in Phase 0. When they exist, they will live with the
Infrastructure project, using Web as the startup project.

## Configuration and time

Strongly typed `DatabaseOptions` bind database command timeout settings.
The SQL Server connection string uses the standard
`ConnectionStrings:BillFoundry` key.

Application code should use `TimeProvider` rather than `DateTime.Now` or
`DateTime.UtcNow`. `TimeProvider.System` is registered in Application DI.

## Hosting concerns

`BillFoundry.Web` is the composition root. `Program.cs` registers Application
and Infrastructure services, Razor components with Interactive Server support,
ProblemDetails, a centralized `IExceptionHandler`, logging, and health checks.

- `/health` is a liveness probe and does not require SQL Server.
- `/health/ready` includes an EF Core database check.

Interactive Server is enabled per component, not globally, so later Identity
pages can remain static SSR. The shell navigation is the interactive island
needed for mobile menu behavior.

## Testing

- Domain and Application tests cover rules and application services.
- Integration tests use `WebApplicationFactory` against the Web host.

Phase 0 includes smoke tests only. Business-rule coverage will land with the
features that introduce those rules.
