# IntelliFin Admin Service

The Admin Service provides the control plane entry point for IntelliFin system administration features. It exposes health and readiness probes, Prometheus metrics, a service version endpoint, and Keycloak-backed user and role management APIs while persisting audit events and user ID mappings.

## Endpoints
- `GET /health` – Liveness probe
- `GET /health/ready` – Dependency-aware readiness probe (SQL Server + Keycloak)
- `GET /metrics` – Prometheus metrics
- `GET /api/admin/version` – Build/version metadata
- `GET /api/admin/users` – Paginated user list (supports `pageNumber`/`pageSize` query parameters)
- `GET /api/admin/users/{id}` – Retrieve a Keycloak user
- `POST /api/admin/users` – Create a user (forces initial password reset via Keycloak required actions)
- `PUT /api/admin/users/{id}` – Update user profile and status flags
- `DELETE /api/admin/users/{id}` – Delete a user from the Keycloak realm
- `POST /api/admin/users/{id}/reset-password` – Set a temporary or permanent password
- `GET /api/admin/users/{id}/roles` – List assigned realm roles
- `POST /api/admin/users/{id}/roles` – Assign one or more realm roles
- `DELETE /api/admin/users/{id}/roles/{roleName}` – Remove a realm role from the user
- `GET /api/admin/roles` – List Keycloak realm roles
- `GET /api/admin/roles/{name}` – Retrieve a realm role
- `POST /api/admin/roles` – Create a new realm role
- `PUT /api/admin/roles/{name}` – Update role name/description
- `DELETE /api/admin/roles/{name}` – Delete a realm role

## Configuration
Configuration is supplied via environment variables or `appsettings.json` values:

| Setting | Description |
| --- | --- |
| `ConnectionStrings__Default` | SQL Server connection string for `IntelliFin_AdminService` |
| `Keycloak__BaseUrl` | Base URL for the Keycloak deployment |
| `Keycloak__Realm` | Realm name (defaults to `IntelliFin`) |
| `Keycloak__ClientId` / `Keycloak__ClientSecret` | Client credentials for Keycloak Admin API |
| `Database:ApplyMigrations` | Toggle automatic EF Core migrations (default `true`) |

## Development
```bash
# Restore and run migrations
DOTNET_ENVIRONMENT=Development dotnet run --project apps/IntelliFin.AdminService
```

Use `scripts/build-admin-service-image.sh` to produce a signed container image for deployment.
