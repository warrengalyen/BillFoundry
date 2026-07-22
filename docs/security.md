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
Login `returnUrl` values are accepted only when they are local paths.

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
`[AllowAnonymous]`. Public routes include login, password reset, access denied,
error, not-found, and health endpoints.

Authorization is enforced by ASP.NET Core endpoint metadata and cookie
challenges, not only by hiding links in the UI. Blazor `AuthorizeRouteView`
redirects unauthenticated interactive requests to login.

Roles:

- `Administrator`
- `User`

Policies in `AuthorizationPolicies` should be used when a capability is more
than a raw role check:

- `Administrator` — authenticated user in the Administrator role
- `NotDemoMode` — succeeds only when Demo Mode is disabled; reserved for later
  mutation restrictions

Do not authorize privileged work only in Razor. Application services and
endpoints must demand the same policies or equivalent server-side checks.

## Current user

`ICurrentUser` is the Application-facing identity abstraction. Application
services should depend on it instead of `HttpContext`, Blazor
`AuthenticationStateProvider`, or Identity UI types.

`ClaimsPrincipalCurrentUser` maps claims. The Web host registers
`HttpContextCurrentUser`, which reads the authenticated principal from
`IHttpContextAccessor`. When no HTTP user is present, the Application default is
`UnauthenticatedCurrentUser`.

## Demo Mode

`DemoMode` / `IDemoMode` bind the `DemoMode` configuration section. The
`NotDemoMode` policy is registered for later phases. This phase does not block
writes or expose a public demo tenant.

## Audit metadata

Entities that implement `IAuditable` receive:

- `CreatedAtUtc` / `UpdatedAtUtc` from `TimeProvider`
- `CreatedByUserId` / `UpdatedByUserId` from `ICurrentUser`

`AuditableInterceptor` applies those values in `SaveChanges`. `ApplicationUser`
implements the pattern.

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
