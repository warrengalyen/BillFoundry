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
`IdentityRole<Guid>`). Configurations are applied from the Infrastructure
assembly. The initial Identity schema is the `AddIdentity` EF Core migration.

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

Interactive Server is enabled per component, not globally. Identity account pages
use static SSR. The shell navigation is interactive so the mobile menu can work.

Application pages require authentication unless marked `[AllowAnonymous]`.

## Testing

- Domain and Application tests cover rules, identity abstractions, and authorization policies.
- Integration tests use `WebApplicationFactory` against the Web host.
- Authentication tests disable development identity seeding and do not require SQL Server
  except when a test explicitly exercises the database.
