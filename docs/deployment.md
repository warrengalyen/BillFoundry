# Deployment

BillFoundry Community Edition is a single ASP.NET Core app. Self-hosted
Community installs use SQL Server. This document covers the production
container image, the SQL Server Compose environment, and the Render public
demo that uses managed PostgreSQL. It is not Kubernetes, Helm, or a
service-mesh guide.

## Production image

Build from the repository root:

```bash
docker build -f src/BillFoundry.Web/Dockerfile -t billfoundry-web .
```

The Dockerfile is multi-stage:

1. Restore the Web project and its project references
2. Publish a framework-dependent Release build
3. Copy into `mcr.microsoft.com/dotnet/aspnet:10.0`

The runtime image:

- Listens on HTTP port **8080** (`ASPNETCORE_HTTP_PORTS`)
- Runs as the image non-root `app` user (`$APP_UID`)
- Defaults `ASPNETCORE_ENVIRONMENT` to `Production`
- Defaults `Database:ApplyMigrationsOnStartup` to `false`
- Persists data-protection keys under `/app/data-protection-keys` when that
  path is writable
- Exposes HTTP `8080`. The image HEALTHCHECK opens that TCP port. HTTP
  liveness is `GET /health`; readiness is `GET /health/ready`.

Put TLS on a reverse proxy. The container speaks HTTP.

### Required configuration

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings__BillFoundry` | SQL Server or PostgreSQL connection string. Production startup fails if this is missing. |
| `Database__Provider` | `SqlServer` (default) or `PostgreSql` (public Render demo only). |
| `DataProtection__KeyPath` | Directory for the data-protection key ring. Mount a volume. |
| `OrganizationLogoStorage__RootPath` | Logo file root. Mount a volume if logos must survive recreates. |

Never bake production passwords into the image or Compose file.

### Optional configuration

| Setting | Default | Purpose |
| --- | --- | --- |
| `Database__ApplyMigrationsOnStartup` | `false` | Apply **pending** EF Core migrations at process start. Never drops the database. |
| `ForwardedHeaders__Enabled` | `false` | Honor `X-Forwarded-*` from a reverse proxy that overwrites those headers. |
| `ASPNETCORE_ENVIRONMENT` | `Production` in the image | `Development` enables Identity seed. Do not use Development in production. |
| `IdentitySeed__Enabled` | `false` | Ignored outside Development. |

Apply schema with `dotnet ef database update` from a controlled job, or set
`Database__ApplyMigrationsOnStartup=true` on a single starting instance. Do
not enable drop/create or `EnsureDeleted`.

### Reverse proxy

When TLS terminates in front of the container, set:

```text
ForwardedHeaders__Enabled=true
```

The proxy must overwrite `X-Forwarded-For` and `X-Forwarded-Proto`. Cookie
`Secure` then follows the forwarded scheme (`SameAsRequest`).

## Local Compose

`compose.yaml` is a **development** stack: the web app, SQL Server 2022, and
named volumes.

```bash
docker compose up --build
```

Then open `http://localhost:8080`. Sign in with the Development seed accounts
documented in [development.md](development.md) (`admin@localhost` /
`Dev-Admin-Passw0rd!` unless you override them).

Compose sets:

- `ASPNETCORE_ENVIRONMENT=Development` so Identity seed may run
- `Database__ApplyMigrationsOnStartup=true` so pending migrations apply
- SQL Server SA password `${MSSQL_SA_PASSWORD:-DevOnly_P@ssw0rd}`

That password is a **development placeholder**. Copy `.env.example` to `.env`
(gitignored) to override it. Do not use it in production.

Volumes:

- `sql-data` - SQL Server data files
- `data-protection-keys` - ASP.NET Core key ring
- `organization-logos` - uploaded logos

`web` waits until `db` is healthy. The Compose `web` healthcheck opens TCP
port 8080 (the process is accepting connections). HTTP liveness is still
`GET /health`; readiness is `GET /health/ready`.

Stop and start again with the same volumes; the database and keys remain.

```bash
docker compose down
docker compose up
```

`docker compose down -v` **deletes** the SQL volume. That is destructive and
is not part of normal restart.

### SQL Server client port

Compose publishes `1433` for local tools. The SA password is the Compose
placeholder unless `.env` overrides it.

## Public demo Compose overlay

`compose.demo.yaml` layers onto `compose.yaml`:

```bash
docker compose -f compose.yaml -f compose.demo.yaml up --build
```

The overlay sets Production, enables Demo Mode, enables demo seed with
`DemoSeed__ResetOnStartup=true`, and keeps Identity development seed off.
All seeded business data is fictional. That overlay still uses SQL Server.

## Local PostgreSQL demo validation

`compose.demo.postgres.yaml` is a separate stack for checking the Render
database path. It does not replace `compose.yaml`.

```bash
docker compose -f compose.demo.postgres.yaml up --build
```

Then open `http://localhost:8081`. The web process sets
`Database__Provider=PostgreSql`, applies the PostgreSQL migration set at
startup, and loads fictional demo seed. Postgres is published on host port
**5433**. Integration tests use that port when `BILLFOUNDRY_TEST_POSTGRES` is
unset.

## Public demo on Render

`render.yaml` is a Render Blueprint for the public demo only. Dashboard:
**New → Blueprint** and connect this repository.

It deploys:

- `billfoundry-web` — the production Docker image (`starter`, Oregon)
- `billfoundry-db` — Render Postgres (`basic-256mb`, Oregon)

Required non-secret environment values are in the Blueprint:

- `Database__Provider=PostgreSql`
- `Database__ApplyMigrationsOnStartup=true`
- `ConnectionStrings__BillFoundry` from the Postgres `connectionString`
- Demo Mode and demo seed with reset on startup
- `ForwardedHeaders__Enabled=true`
- `ASPNETCORE_ENVIRONMENT=Production`

The first web start applies the PostgreSQL migrations, then seeds fictional
North Beacon Studio data. `EnsureCreated` / `EnsureDeleted` are not used.

After the Blueprint syncs, you still need to:

- Confirm the Postgres plan matches the workspace (Basic)
- Attach a custom domain if you want one other than `*.onrender.com`
- Wait for the first deploy; SQL-style crash loops should not occur because
  `/health` does not require the database, but the app is not usable until
  migrations and seed finish

Do not use this Blueprint for a real freelance install.

## Health checks

- `GET /health` - process is up. No database.
- `GET /health/ready` - the configured database is reachable.

Orchestrators should use liveness → `/health` and readiness → `/health/ready`.
Render's web health check uses `/health`.

## Intentionally not included

Kubernetes, Helm, service mesh, Redis, and generic cloud ingress controllers
are out of scope for this Community Edition host. The Render Blueprint is only
the public demo stack. PostgreSQL is not a second Community default database.
