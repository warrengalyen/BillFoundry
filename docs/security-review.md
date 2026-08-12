# Security review

This document records a Community Edition security review of the complete
application. It is not a penetration-test report and it does not replace
[security.md](security.md).

## Areas reviewed

- Authentication and Identity cookies
- Authorization policies and server-side resource checks
- Antiforgery / CSRF on cookie-authenticated POSTs
- Login `returnUrl` handling
- File uploads (organization logo)
- Input validation and output encoding
- SQL access (EF Core parameterized queries)
- Secrets and configuration
- Cookie flags
- Redirect safety
- Error disclosure
- Sensitive-data logging
- Production headers and reverse-proxy forwarding
- Health-endpoint exposure

## Findings and changes

### Login return URLs

`LocalUrl` previously accepted any string that started with `/` except `//` and
`/\`. That missed control-character open-redirect tricks such as a tab after
the first slash.

`LocalUrl.IsSafe` now follows the ASP.NET Core local-URL rules: only `/` or
`~/` paths, no `//` or `/\`, and no control characters. Regression tests cover
accepted local paths and rejected external or control-character values.

### Security headers

The host did not emit browser security headers. `SecurityHeadersMiddleware`
now adds:

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: no-referrer`
- `Permissions-Policy` disabling camera, microphone, geolocation, and payment
- `Content-Security-Policy` with `frame-ancestors 'none'`

Blazor Web App still requires `'unsafe-inline'` for scripts (import map and
boot script) and styles (report bar widths). Interactive Server also needs
`ws:` / `wss:` in `connect-src`. Those CSP exceptions are intentional.

### Data-protection keys

ASP.NET Core data protection now uses application name `BillFoundry`. When
`DataProtection:KeyPath` is set, keys are persisted to that directory. Compose
maps a volume to `/app/data-protection-keys` so cookie protection survives
container recreation. An empty path keeps the default ephemeral key ring for
local `dotnet run`.

### Forwarded headers

`ForwardedHeaders:Enabled` defaults to false. When true, the host trusts
`X-Forwarded-For` and `X-Forwarded-Proto` from the immediate proxy. Enable this
only behind a reverse proxy that **overwrites** incoming forwarded headers.
See [deployment.md](deployment.md).

### Cookie security

The Identity cookie remains HTTP-only, `SameSite=Lax`, and
`CookieSecurePolicy.SameAsRequest`. Combined with forwarded headers, HTTPS
requests from a TLS-terminating proxy mark the cookie Secure. Forcing
`Always` would break the HTTP Compose environment.

### Already in good shape

- Pages under `Components/Pages` require `[Authorize]`; account login/reset
  pages are `[AllowAnonymous]`
- Application services re-check policies; endpoints require the same policies
- Logout is POST + antiforgery
- Logo uploads: signature inspection, 1 MB cap, generated names, not in `wwwroot`
- EF Core LINQ / `FromSql` with parameters; no string-concatenated SQL
- Production refuses to start without `ConnectionStrings:BillFoundry`
- Identity seed cannot run outside Development
- Failed sign-in messages do not reveal whether an email exists
- Account notifications do not log tokens or passwords
- HTML error page shows a request id, not exception details
- JSON errors use generic ProblemDetails titles
- `/health` is anonymous and does not touch the database; `/health/ready`
  is anonymous and checks SQL Server (needed for orchestrator probes)

## Intentionally deferred

- A stricter CSP without `'unsafe-inline'` (would require a nonce pipeline
  for Blazor and a rewrite of inline chart styles)
- `CookieSecurePolicy.Always` as a global default
- Automatic SQL retry (`EnableRetryOnFailure`) around existing user
  transactions
- A separate secrets manager or vault
- Redis, Kubernetes, or a service mesh
- Demo Mode write blocking (policy exists; product behavior is still open)
- Email confirmation (no production mail provider yet)

## Tests

- `LocalUrlTests` — local vs unsafe return URLs
- `AuthenticationTests.Health_endpoint_includes_security_headers`
- Existing authorization tests for organization, clients, catalog, estimates,
  invoices, reports, documents, and audit remain the server-side policy suite
