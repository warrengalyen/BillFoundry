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

For layout and CSS work, use `dotnet watch` so Sass is recompiled and
`wwwroot/app.css` reloads without rebuilding the container image:

```bash
dotnet watch run --project src/BillFoundry.Web
```

Open `https://localhost:7270` (or `http://localhost:5095`). Edit
`src/BillFoundry.Web/Styles/*.scss` files and save. `dotnet watch` triggers
Sass recompilation via EmbeddedSass.Net.MsBuild. Development serves
`wwwroot/app.css` without fingerprinting and with `Cache-Control: no-cache`,
so a refresh shows the new rules.

**Do not edit `wwwroot/app.css` directly.** It is generated from Sass during
`dotnet build`. The Sass source files under `Styles/` are the authoritative
source of truth.

Do not iterate on CSS against `docker compose` web. Compose publishes a
Release image; `app.css` is copied at build time and will not pick up editor
saves. Leave SQL Server in Compose if you want (`docker compose up db -d`) and
point the connection string at `localhost,1433`, or use LocalDB from
`appsettings.Development.json`.

Scoped CSS (`*.razor.css`) still goes through the Blazor bundle. Those files
need `dotnet watch` or a rebuild.

## Sass architecture

Styles are written in SCSS and compiled to `wwwroot/app.css` by
[EmbeddedSass.Net.MsBuild](https://github.com/gumbarros/EmbeddedSass.Net),
which uses the official Dart Sass compiler via the Embedded Sass Protocol.
Compilation happens automatically during `dotnet build` - no Node.js, npm, or
additional CLI tools are needed.

- **Debug builds** produce expanded CSS with source maps
- **Release builds** produce compressed CSS without source maps
- The generated `wwwroot/app.css` is committed to source control so `dotnet run` works without a prior build step

### File organization

```
src/BillFoundry.Web/Styles/
  app.scss                   Entry point, @use all partials

  abstracts/
    _tokens.scss             Design tokens: Sass vars + CSS custom properties
    _mixins.scss             Breakpoints, typography helpers, responsive utilities

  base/
    _reset.scss              Box-sizing, margin reset, color-scheme
    _typography.scss         Headings, body text, lede
    _base.scss               Links, code, focus-visible, skip-link, visually-hidden, Blazor error UI

  layout/
    _shell.scss              App shell, sidebar, header, nav toggle, nav links, nav icons, panels
    _responsive.scss         Global layout transitions at breakpoints

  components/
    _buttons.scss            Primary, secondary, ghost, destructive variants
    _forms.scss              Form fields, grids, fieldsets, checkboxes, validation
    _tables.scss             Data tables, sort buttons, table actions, responsive line-item stacking
    _status.scss             Status badges, definition lists, activity timeline
    _filters.scss            Filter bars, progressive-disclosure pattern, filter variants
    _feedback.scss           Alerts, success messages, pagination

  pages/
    _dashboard.scss          KPI summary bar, dashboard panels, metric cards, bar charts
    _account.scss            Login, manage, change-password layouts
    _reports.scss            Report navigation, report index, responsive bar charts
    _landing.scss            Landing page, hero, grid, footer, demo banner/credentials
```

### Design tokens

Centralized in `_tokens.scss`. Sass variables (e.g. `$bf-accent`) are used at
compile time in mixins and calculations. CSS custom properties (e.g.
`--bf-accent`) are emitted on `:root` for runtime use. Token categories:

| Category | Examples |
| --- | --- |
| Surfaces | `--bf-surface-page`, `--bf-surface-primary`, `--bf-surface-elevated` |
| Text | `--bf-text-primary`, `--bf-text-secondary`, `--bf-text-muted` |
| Accent | `--bf-accent`, `--bf-accent-hover`, `--bf-accent-subtle` |
| Semantic | `--bf-success`, `--bf-warning`, `--bf-error`, `--bf-info` (+ `-bg` variants) |
| Borders | `--bf-border-default`, `--bf-border-subtle` |
| Spacing | `--bf-space-1` through `--bf-space-8` (4px base scale) |
| Typography | `--bf-font-family`, `--bf-font-mono` |
| Radii | `--bf-radius-sm`, `--bf-radius-md` |
| Shadows | `--bf-shadow-sm`, `--bf-shadow-md` |
| Layout | `--bf-sidebar-width`, `--bf-sidebar-bg`, `--bf-header-height` |

To add a new token, add the Sass variable and the CSS custom property in
`_tokens.scss`.

### Breakpoint system

Defined in `_mixins.scss`. Use `@include respond-above($bp-md)` or
`@include respond-below($bp-md)`.

| Token | Value | Purpose |
| --- | --- | --- |
| `$bp-sm` | 30rem (~480px) | Mobile |
| `$bp-md` | 48rem (~768px) | Tablet portrait / sidebar transition |
| `$bp-lg` | 64rem (~1024px) | Tablet landscape / laptop |
| `$bp-xl` | 80rem (~1280px) | Desktop |

### Intrinsic-first responsive principle

Prefer intrinsic responsive layouts (CSS Grid with `auto-fit`/`minmax()`,
Flexbox with `flex-wrap`, `clamp()` for fluid sizing) before adding
breakpoint-based overrides. Reserve breakpoints for genuine layout transitions
(sidebar collapse, form column changes, filter progressive disclosure).

### Responsive table strategy

Tables use a deprioritize hierarchy rather than hiding columns:

1. **Preserve** essential information (number, client, status, amount)
2. **Combine** related information into composite columns
3. **Relocate** secondary information within the row
4. **Selectively hide** truly nonessential information only as a last resort
5. **Localized horizontal scroll** when tabular relationships require preservation

Line-item tables (invoice/estimate editors) use `data-table--line-items` for
a stacked block layout on narrow widths.

### Progressive-disclosure filters

Pages with many filter controls (InvoiceList, Audit) use progressive
disclosure: common filters (search, status) are always visible, with less
common filters behind a "Filters" button. Active advanced filter count is
shown as a badge. Filter state is preserved when the section collapses.

### Navigation icons

Lucide SVG icons (ISC license) are embedded inline in `NavMenu.razor`.
Icons use `aria-hidden="true"` since text labels provide the accessible name.
Licensed in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).

### Adding new page/component styles

1. Create a partial `_filename.scss` in the appropriate directory
2. Add `@use "directory/filename"` to `app.scss`
3. Use tokens from `_tokens.scss` via `@use "../abstracts/tokens" as *`
4. Use mixins via `@use "../abstracts/mixins" as *`
5. Prefer tokens over hardcoded values

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

- `appsettings.json` - non-secret defaults, including logging and database option defaults
- `appsettings.Development.json` - local development values, including a LocalDB connection string
- User Secrets - secrets and non-LocalDB credentials on a developer machine
- Environment variables - secrets and overrides in deployed environments

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

EF Core is registered against SQL Server through `BillFoundryDbContext`, which
includes ASP.NET Core Identity, the installation organization profile,
clients, the service catalog, estimates, invoices, invoice payments, document number sequences, and the business audit trail. Apply
migrations before first sign-in:

```bash
dotnet ef migrations add MigrationName --project src/BillFoundry.Infrastructure --startup-project src/BillFoundry.Web
dotnet ef database update --project src/BillFoundry.Infrastructure --startup-project src/BillFoundry.Web
```

The Web project references `Microsoft.EntityFrameworkCore.Design` so the EF tools
can use it as the startup project.

`Database:ApplyMigrationsOnStartup` defaults to `false`. When `true`, the host
applies pending EF Core migrations at process start. It never drops or recreates
the database. The local Compose stack enables this so a clean container can
reach a usable schema. Prefer `dotnet ef database update` for developer
workstations unless you are using Compose.

Relative `OrganizationLogoStorage:RootPath` values are resolved from the Web
content root. The default is `App_Data/organization-logos`. That directory is
gitignored. Do not place uploaded logos in `wwwroot`.

## Docker Compose

`compose.yaml` runs BillFoundry and SQL Server 2022 for local container work.
See [deployment.md](deployment.md) for image details, volumes, and health
checks.

```bash
docker compose up --build
```

The stack is **development only**. It sets `ASPNETCORE_ENVIRONMENT=Development`,
applies pending migrations at startup, and uses the placeholder SA password
`DevOnly_P@ssw0rd` unless you override `MSSQL_SA_PASSWORD` in a gitignored
`.env` file (see `.env.example`). Open `http://localhost:8080` and sign in
with the Development Identity seed accounts.

`docker compose down` stops containers and keeps the SQL volume.
`docker compose down -v` deletes database data.

## Identity seed (Development)

`appsettings.Development.json` can enable `IdentitySeed` to create the
Administrator and User roles plus local demo accounts. Those passwords are
development placeholders. Override them with User Secrets if you prefer not to
use the committed Development values, and never use them in production.

Seeding runs only when `IdentitySeed:Enabled` is true **and** the environment is
Development. If SQL Server is unavailable, seed failure is logged and startup
continues.

```bash
dotnet user-secrets set "IdentitySeed:AdministratorPassword" "YOUR_DEV_PASSWORD" --project src/BillFoundry.Web
dotnet user-secrets set "IdentitySeed:UserPassword" "YOUR_DEV_PASSWORD" --project src/BillFoundry.Web
```

## Demo Mode and demo seed

`DemoMode:Enabled` defaults to false. When it is true, the `NotDemoMode`
authorization policy fails. Application services and account pages use that
policy so a public demo cannot change published passwords or the organization
profile. Account lockout is disabled while Demo Mode is on.

`DemoSeed:Enabled` is also false by default. When true, a hosted service loads
the fictional North Beacon Studio dataset (organization, users, clients,
catalog items, estimates, invoices, payments, and audit events). It runs in
any environment, including Production, so a live portfolio demo can opt in
explicitly. Production `appsettings.json` leaves it off.

`DemoSeed:ResetOnStartup` replaces business data and restores the published
demo passwords. Compose overlay `compose.demo.yaml` turns that on so a
container restart returns the site to a known state.

Published demo accounts (fictional):

| Role | Email | Password |
| --- | --- | --- |
| Administrator | `admin@northbeacon.example` | `Demo-Admin-Passw0rd!` |
| User | `user@northbeacon.example` | `Demo-User-Passw0rd!` |

Identity development seed (`IdentitySeed`) still runs only in Development and
is independent of Demo Seed. Do not enable Identity seed in Production.

See [security.md](security.md) for authentication, authorization, and account
notification details.

## Health checks

- `GET /health` - liveness. Does not contact SQL Server.
- `GET /health/ready` - readiness, including an EF Core database check.

Smoke tests use `/health` so they can run without a database. Unauthenticated
requests to application pages should redirect to `/Account/Login`.

## Logging and errors

ASP.NET Core logging is configured in appsettings. Production uses the JSON
console formatter; Development uses the simple formatter. Do not log passwords,
secrets, tokens, or unnecessary personal information.

Unhandled exceptions are logged by `GlobalExceptionHandler`. JSON clients receive
ProblemDetails. HTML requests use the Blazor error page.

## Accessibility

WCAG 2.2 AA is a project requirement. New UI must keep semantic landmarks,
keyboard access, and visible focus. The shell includes a skip-to-content link.
See [accessibility.md](accessibility.md) for the Community Edition review.

## Tests

Test projects live under `tests/`. Integration tests reference the Web project
and use `Microsoft.AspNetCore.Mvc.Testing`. They should not require SQL Server
unless they specifically exercise database behavior. Organization, client,
catalog, estimate, invoice, and payment persistence, concurrency, and constraint tests create a
LocalDB database named `BillFoundry_IT_{guid}` and drop it when the fixture
completes. Client, catalog, estimate, and invoice lists are queried with server-side
search, status filtering, sorting, and paging.

Estimate rounding, status transitions, and number allocation are documented in
[estimates.md](estimates.md). Invoice lifecycle, overdue derivation, conversion,
and numbering are documented in [invoice-lifecycle.md](invoice-lifecycle.md).
Payment recording, settlement, and reversals are documented in
[payments.md](payments.md). Invoice and estimate PDF generation, the PDFsharp
library choice, and download endpoints are documented in [pdf.md](pdf.md).
Generator tests assert `%PDF` headers and extracted field text with PdfPig.
Dashboard metrics, aging, payment reports, outstanding invoices, and CSV
exports are documented in [reporting.md](reporting.md). The append-only
business audit trail is documented in [audit.md](audit.md).
