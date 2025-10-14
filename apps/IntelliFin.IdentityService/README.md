# IntelliFin Identity Service

The Identity Service provides authentication and authorization capabilities for the IntelliFin platform. This document
covers the base service configuration that was scaffolded in Story 2.1.

## Running locally

```bash
dotnet run --project apps/IntelliFin.IdentityService/IntelliFin.IdentityService.csproj
```

The service listens on port `8080` by default. Swagger UI is available at `https://localhost:8080/swagger` in development
builds.

## Configuration

Configuration is composed from the following sources (in order):

1. `appsettings.json`
2. Environment specific settings (`appsettings.Development.json`, `appsettings.Production.json`)
3. Environment variables (optionally prefixed with `IDENTITY_`)

Key settings:

| Setting | Description | Default |
| --- | --- | --- |
| `ConnectionStrings:IdentityDb` | SQL Server connection string for the identity database. | `Server=localhost;Database=IntellifinIdentity;Trusted_Connection=True;TrustServerCertificate=True;` |
| `ConnectionStrings:DefaultConnection` | Fallback SQL Server connection string used by shared infrastructure. | Same as `IdentityDb` |
| `Features:DisableSqlHealthCheck` | When `true`, skips the SQL Server readiness health check (useful for tests). | `false` |
| `Serilog:MinimumLevel:Default` | Minimum log level per environment. | `Debug` (Development) / `Information` (Production) |

All settings can be overridden with environment variables. Example:

```bash
export IDENTITY_ConnectionStrings__IdentityDb="Server=sql-server;Database=IntellifinIdentity;User Id=identity_svc;Password=changeme;TrustServerCertificate=True;"
```

## Health & Observability

* `/health/live` – liveness probe (no dependencies).
* `/health/ready` – readiness probe (includes SQL Server health check).
* `/metrics` – Prometheus metrics endpoint providing default ASP.NET Core metrics plus:
  * `identity_service_startup_total`
  * `identity_service_requests_total{method,endpoint,status_code}`

## Logging

Serilog is configured for structured JSON output. Logs are emitted to the console and to rolling files at
`logs/identityservice-<date>.txt`. Each log entry includes a `CorrelationId` property populated from the
`X-Correlation-ID` request header (or generated when missing).

## Deployment

Kubernetes manifests for the service are located in `k8s/`. See [`docs/deployment.md`](docs/deployment.md) for detailed
instructions.
