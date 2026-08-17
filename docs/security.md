# Security

BillFoundry uses ASP.NET Core Identity with Entity Framework Core and SQL Server.
This document describes the authentication and authorization model for the
Community Edition.

## Authentication

Users authenticate with email and password. Identifiers are `Guid` values on
`ApplicationUser`. Sign-in uses the Identity application cookie
(`.BillFoundry.Auth`): HTTP-only, `SameSite=Lax`, and `Secure` when the request
is HTTPS.

Supported account workflows:

- Log in and log out
- Account profile
- Password change for signed-in users
- Password reset request and completion
- Account lockout after repeated failed attempts
- Disabled accounts, which cannot sign in even with a valid password

Email confirmation is not required for sign-in while production email delivery
is unimplemented.

Logout is a POST from `/Account/Logout` so the request is covered by antiforgery.
Login `returnUrl` values are accepted only when they are local paths without
protocol-relative prefixes or control characters (`LocalUrl`).

The host emits `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`,
`Permissions-Policy`, and `Content-Security-Policy` on HTML and API responses.
See [security-review.md](security-review.md).

## Account notifications

Identity password-reset and confirmation messages go through
`IAccountNotificationService` in the Application project. Infrastructure adapts
that contract to Identity's `IEmailSender<ApplicationUser>`.

The current implementation logs that a notification was queued. It does not send
email, and it does not log reset links, tokens, or passwords. A later email
provider can replace the logging implementation without changing Identity
workflows.

## Authorization

Application pages require an authenticated user unless they are marked
`[AllowAnonymous]`. Public routes include the landing page (`/`), login,
password reset (when Demo Mode is off), access denied, error, not-found, and
health endpoints.

Authorization is enforced by ASP.NET Core endpoint metadata and cookie
challenges, not only by hiding links in the UI. Blazor `AuthorizeRouteView`
redirects unauthenticated interactive requests to login.

Roles:

- `Administrator`
- `User`

Policies in `AuthorizationPolicies` should be used when a capability is more
than a raw role check:

- `Administrator` - authenticated user in the Administrator role
- `ManageOrganizationSettings` - administrators may read and change the
  installation organization profile, including logo upload and removal
- `ManageClients` - authenticated Administrator or User role may list, create,
  edit, activate, and deactivate clients and their contacts
- `ManageCatalog` - authenticated Administrator or User role may list, create,
  edit, activate, and deactivate service catalog items
- `ManageEstimates` - authenticated Administrator or User role may list, create,
  edit drafts, manage line items, duplicate, and apply allowed estimate status
  transitions
- `ManageInvoices` - authenticated Administrator or User role may list, create,
  edit drafts, manage line items, duplicate, mark sent, void, and convert
  accepted estimates
- `NotDemoMode` - succeeds only when Demo Mode is disabled. Password change,
  password reset, and organization profile mutations require this policy.

Do not authorize privileged work only in Razor. Application services and
endpoints must demand the same policies or equivalent server-side checks.

`IOrganizationSettingsService` authorizes `ManageOrganizationSettings` before
loading or mutating the organization. The Organization settings page and
`/media/organization-logo` endpoint require the same policy.

`IClientService` authorizes `ManageClients` before listing or mutating clients.
Client pages require the same policy. Clients are not permanently deleted.

`ICatalogService` authorizes `ManageCatalog` before listing or mutating catalog
items. Catalog pages require the same policy. Catalog items are not permanently
deleted.

`IEstimateService` authorizes `ManageEstimates` before listing or mutating
estimates. Estimate pages require the same policy. Accepted and converted
estimates cannot be edited. Estimates are not permanently deleted. Conversion
to an invoice is performed by `IInvoiceService.ConvertFromEstimateAsync`, not
by a direct estimate status change. See [estimates.md](estimates.md) and
[invoice-lifecycle.md](invoice-lifecycle.md).

`IInvoiceService` authorizes `ManageInvoices` before listing or mutating
invoices, including recording and reversing payments. Invoice pages require the
same policy. `IReportingService` authorizes `ManageInvoices` before returning
dashboard metrics, reports, or CSV. Report pages and `/Reports/*.csv`
endpoints require the same policy. CSV downloads are reads, so Demo Mode does
not block them. Responses include only a Content-Disposition file name. See
[reporting.md](reporting.md).

`IInvoiceDocumentService` and `IEstimateDocumentService` authorize
`ManageInvoices` / `ManageEstimates` before generating PDFs. Download endpoints
`GET /Invoices/{id}/pdf` and `GET /Estimates/{id}/pdf` require the same
policies. Demo Mode does not block downloads because they are not mutations.
Responses include only a Content-Disposition file name, never a filesystem
path. See [pdf.md](pdf.md).

## Organization logo uploads

Logo uploads are accepted only after administrator authorization. The
application:

- Allows PNG, JPEG, and WebP, verified by file signatures rather than the
  submitted name or content type
- Rejects payloads larger than 1 MB
- Stores a generated file name and never trusts the uploaded file name
- Rejects stored names that contain path segments or `..`
- Serves the current logo from `/media/organization-logo` only to administrators

Logo files live outside `wwwroot` under the configured storage root.

## Current user

`ICurrentUser` is the Application-facing identity abstraction. Application
services should depend on it instead of `HttpContext`, Blazor
`AuthenticationStateProvider`, or Identity UI types.

`ClaimsPrincipalCurrentUser` maps claims. The Web host registers
`HttpContextCurrentUser`, which reads the authenticated principal from
`IHttpContextAccessor`. When no HTTP user is present, the Application default is
`UnauthenticatedCurrentUser`.

## Demo Mode

`IDemoMode` binds `DemoMode:Enabled` (default false). The `NotDemoMode` policy
succeeds only when Demo Mode is off. It does not require an authenticated user,
so anonymous password-reset pages stay available on a normal install and stay
blocked on a public demo.

When Demo Mode is on:

- A site-wide banner states that the installation is a demonstration and that
  business data is fictional
- The landing page and login page publish the configured demo accounts
- Account lockout is disabled so visitors cannot lock the shared accounts
- Changing a password, requesting a reset, and completing a reset are denied
- Organization profile, logo upload, and logo removal are denied in
  `IOrganizationSettingsService` and hidden in the UI via the same policy

Demo restrictions live behind `NotDemoMode` and a few dedicated components
(`DemoBanner`, `DemoOnly`, `WhenNotDemo`, `DemoSignInHint`). Pages do not
scatter `if (DemoMode)` checks for those rules.

`DemoSeed:Enabled` is a separate opt-in. It loads fictional North Beacon Studio
data. Production does not enable it unless an operator sets the flag (for
example through `compose.demo.yaml`). Identity development seed still cannot run outside
Development.

CSV downloads and PDF downloads remain allowed in Demo Mode because they are
reads.

## Audit metadata

Entities that implement `IAuditable` receive:

- `CreatedAtUtc` / `UpdatedAtUtc` from `TimeProvider`
- `CreatedByUserId` / `UpdatedByUserId` from `ICurrentUser`

`AuditableInterceptor` applies those values in `SaveChanges`. `ApplicationUser`,
`Organization`, `Client`, `ClientContact`, `CatalogItem`, `Estimate`,
`EstimateLine`, `Invoice`, and `InvoiceLine` implement the pattern.

Business activity history is a separate `AuditEvent` table. See [audit.md](audit.md).
Those rows are append-only and are not written by `AuditableInterceptor`.

## Development seeding

When `IdentitySeed:Enabled` is true in Development, the host seeds the two roles
and optional administrator/user accounts. Seed passwords belong in Development
configuration or User Secrets. Seeding does not run outside Development. Do not
commit production credentials.

## Security considerations

- Passwords, reset tokens, and authentication cookies must not be logged
- Failed sign-in messages do not confirm whether an email exists
- Forgot-password always shows the same queued-message result
- Health liveness remains anonymous; readiness still checks the database
- Privileged actions must be authorized on the server
- Persist data-protection keys when running in containers (`DataProtection:KeyPath`)
- Enable `ForwardedHeaders:Enabled` only behind a proxy that overwrites forwarded headers
