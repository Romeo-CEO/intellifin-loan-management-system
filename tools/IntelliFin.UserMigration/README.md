# IntelliFin Identity to Keycloak Migration Tool

This console application executes the brownfield migration path defined in the System Administration control plane. It extracts users, roles, and assignments from the legacy ASP.NET Core Identity database and recreates them inside the IntelliFin Keycloak realm while keeping bi-directional mapping tables in the Admin Service database.

## Features

- Migrates ASP.NET Core Identity roles to Keycloak realm roles and stores a `RoleIdMapping` entry for each role.
- Migrates users in configurable batches, forcing an `UPDATE_PASSWORD` action on first Keycloak sign-in.
- Recreates user-role assignments using the Keycloak Admin REST API and preserves verification flags.
- Generates JSON migration reports that can be archived or pushed to MinIO for compliance.
- Provides validation and rollback commands that work against the IntelliFin realm without touching the legacy database schema.

## Configuration

The tool is configured through `appsettings.json` (checked into source control with sensible defaults) and environment variables prefixed with `INTELLIFIN_MIGRATION_`. Sensitive values such as the client secret or admin password should be injected via environment variables or secret management, not committed to source control.

```json
{
  "Keycloak": {
    "BaseUrl": "https://keycloak.local/",
    "Realm": "IntelliFin",
    "TokenRealm": "master",
    "ClientId": "admin-cli",
    "ClientSecret": null,
    "Username": "",
    "Password": "",
    "ApiDelayMs": 100
  },
  "Databases": {
    "IdentityConnectionString": "Server=localhost;Database=IntelliFin.Identity;Trusted_Connection=True;TrustServerCertificate=True;",
    "AdminConnectionString": "Server=localhost;Database=IntelliFin.Admin;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Migration": {
    "UserBatchSize": 200,
    "ValidationSamplePercentage": 10,
    "MaxRetryAttempts": 3,
    "RetryDelayMs": 1000,
    "ReportsDirectory": "reports"
  }
}
```

### Environment Variable Overrides

| Setting | Environment Variable | Notes |
| ------- | -------------------- | ----- |
| `Keycloak:BaseUrl` | `INTELLIFIN_MIGRATION_KEYCLOAK__BASEURL` | Must include the trailing slash or it is added automatically. |
| `Keycloak:ClientSecret` | `INTELLIFIN_MIGRATION_KEYCLOAK__CLIENTSECRET` | Preferred over storing in configuration files. |
| `Keycloak:Username` | `INTELLIFIN_MIGRATION_KEYCLOAK__USERNAME` | Required when using resource owner password flow. |
| `Keycloak:Password` | `INTELLIFIN_MIGRATION_KEYCLOAK__PASSWORD` | Required when using resource owner password flow. |
| `Databases:IdentityConnectionString` | `INTELLIFIN_MIGRATION_DATABASES__IDENTITYCONNECTIONSTRING` | Connection string for the legacy Identity database. |
| `Databases:AdminConnectionString` | `INTELLIFIN_MIGRATION_DATABASES__ADMINCONNECTIONSTRING` | Connection string for the Admin Service mapping database. |
| `Migration:ReportsDirectory` | `INTELLIFIN_MIGRATION_MIGRATION__REPORTSDIRECTORY` | Location where migration reports are written. |

## Commands

```bash
# Migrate roles and users (validation executes by default)
dotnet run --project tools/IntelliFin.UserMigration migrate

# Migrate roles and users but skip validation (useful for smoke tests)
dotnet run --project tools/IntelliFin.UserMigration migrate --skip-validation

# Perform a standalone validation pass

dotnet run --project tools/IntelliFin.UserMigration validate

# Rollback Keycloak changes and clear mapping tables
dotnet run --project tools/IntelliFin.UserMigration rollback
```

The tool returns a non-zero exit code when `System.CommandLine` encounters validation errors; runtime exceptions are logged to the console.

## Migration Workflow (High-Level)

1. **Role Migration**: Query `AspNetRoles`, create corresponding Keycloak realm roles, and record `AspNetRoleId → KeycloakRoleId` entries.
2. **User Migration**: Page through `AspNetUsers`, create Keycloak user accounts, and populate `UserIdMapping` entries. Every migrated user receives the `UPDATE_PASSWORD` required action.
3. **User-Role Assignment**: For each Identity user-role pairing, assign the Keycloak realm role resolved through `RoleIdMapping`.
4. **Validation**: Count comparisons, role assignment totals, and a 10% random sample cross-check.
5. **Reporting**: Write a JSON report describing the migration outcome and validation status to the configured directory.
6. **Rollback (Optional)**: Delete Keycloak entities and clear mapping tables without modifying the legacy Identity database.

## Safety Considerations

- The Identity database is accessed in read-only mode; `DbContext` instances never call `SaveChanges` against it.
- Keycloak API calls include a configurable delay to respect rate limiting.
- The rollback command logs every deletion attempt and continues processing even if individual calls fail.
- Reports can be copied to MinIO or another archive location by CI after the tool finishes.

## Next Steps

- Wire the executable into the CI/CD pipeline to support dry-run migrations.
- Extend the report writer to push artifacts directly into MinIO (Story 1.5 dependency).
- Add integration tests once a disposable Keycloak container is available in the test infrastructure.
