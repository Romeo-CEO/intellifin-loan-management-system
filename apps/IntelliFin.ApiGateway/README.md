# IntelliFin API Gateway

## Secret Resolution

The gateway resolves sensitive configuration (database connections and
Keycloak credentials) at runtime using the `EnvironmentSecretResolver`. Values are
retrieved in the following order:

1. Environment variable matching the secret key (for example
   `APIGATEWAY_DB_CONNECTION_STRING`).
2. File path referenced by the `<KEY>_FILE` environment variable. This supports
   Vault sidecars that project credentials onto the filesystem.
3. The optional `Secrets` section inside `appsettings.Development.json` (not
   committed to git).

If no value is found the service fails fast during startup, avoiding accidental
usage of placeholder credentials.

## Local Development

Create `apps/IntelliFin.ApiGateway/appsettings.Development.json` from the
provided `.template` file and populate the `Secrets` section with development
credentials:

```bash
cp apps/IntelliFin.ApiGateway/appsettings.Development.template.json \
   apps/IntelliFin.ApiGateway/appsettings.Development.json
```

Alternatively, export the environment variables before running the service:

```bash
export APIGATEWAY_DB_CONNECTION_STRING="Server=localhost,31433;Database=IntelliFin_LoanManagement;User Id=app_user;Password=local-password;TrustServerCertificate=true"
```

The development JSON file is gitignored to prevent accidental disclosure of
secrets.

## Keycloak-Only Authentication

Legacy HMAC JWT tokens are no longer accepted. The gateway now enforces the
Keycloak bearer scheme exclusively:

- Populate `Authentication:KeycloakJwt` via environment variables or
  configuration providers. At minimum, `Authority`, `Issuer`, and `Audience`
  must be supplied.
- Production deployments must keep
  `Authentication__KeycloakJwt__RequireHttps=true` so that metadata is fetched
  securely.
- Downstream consumers must obtain OAuth2 access tokens from Keycloak using the
  same realm configured here. Services that previously relied on the legacy
  signing key should switch to client credentials or delegated Keycloak flows.

See `docs/deployment/README.md` for rollout timelines, monitoring guidance, and
rollback procedures associated with the Keycloak cutover.
