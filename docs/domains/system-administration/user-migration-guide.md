# ASP.NET Core Identity to Keycloak Migration Guide

This guide documents the process, configuration, and validation steps for Story 1.2 of the System Administration Control Plane: migrating IntelliFin users, roles, and permissions from ASP.NET Core Identity to Keycloak while preserving audit history.

## Prerequisites

1. **Keycloak IntelliFin Realm**: Story 1.1 must be completed and the IntelliFin realm available with an admin service account (`admin-cli` or client credentials).
2. **Database Access**: Read-only credentials to the legacy ASP.NET Core Identity database and write access to the Admin Service database (for mapping tables and audit logs).
3. **.NET SDK 9.0**: Required to build and run the migration tool located at `tools/IntelliFin.UserMigration`.
4. **Backups**: Confirm full backups of the Identity database and Admin Service database are stored in MinIO prior to execution.

## Tool Overview

The migration is executed via the console application defined in `tools/IntelliFin.UserMigration`. The tool performs the following:

1. Creates/updates the `UserIdMapping` and `RoleIdMapping` tables in the Admin Service database.
2. Migrates Keycloak realm roles based on `AspNetRoles` and preserves descriptions.
3. Migrates users in batches from `AspNetUsers`, copying profile data and status flags.
4. Recreates `AspNetUserRoles` assignments in Keycloak, including composite roles if present.
5. Validates counts and performs a 10% random sample comparison.
6. Generates a JSON migration report and logs the outcome.
7. Provides a rollback routine that removes Keycloak users and roles created during migration.

Refer to the [tool README](../../../tools/IntelliFin.UserMigration/README.md) for detailed configuration, environment variables, and command usage.

## Database Schema Changes

Deploy the following SQL schema to the Admin Service database before running the migration:

```sql
CREATE TABLE IF NOT EXISTS UserIdMapping (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AspNetUserId NVARCHAR(450) NOT NULL UNIQUE,
    KeycloakUserId NVARCHAR(100) NOT NULL UNIQUE,
    MigrationDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    MigrationStatus NVARCHAR(50) NOT NULL,
    Notes NVARCHAR(1024) NULL,
    INDEX IX_UserIdMapping_AspNetUserId (AspNetUserId),
    INDEX IX_UserIdMapping_KeycloakUserId (KeycloakUserId)
);

CREATE TABLE IF NOT EXISTS RoleIdMapping (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AspNetRoleId NVARCHAR(450) NOT NULL UNIQUE,
    KeycloakRoleId NVARCHAR(100) NOT NULL UNIQUE,
    RoleName NVARCHAR(256) NOT NULL,
    MigrationDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    INDEX IX_RoleIdMapping_AspNetRoleId (AspNetRoleId),
    INDEX IX_RoleIdMapping_KeycloakRoleId (KeycloakRoleId)
);

CREATE TABLE IF NOT EXISTS MigrationAuditLog (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CreatedOnUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Action NVARCHAR(128) NOT NULL,
    Actor NVARCHAR(256) NOT NULL,
    Details NVARCHAR(2048) NULL
);
```

> **Note:** `IF NOT EXISTS` syntax is provided for documentation. Adjust for SQL Server by checking object existence before creation.

## Execution Steps

1. **Configure Environment**
   - Update `appsettings.json` or inject environment variables with the correct Keycloak base URL, realm name, and database connection strings.
   - Ensure the Keycloak admin account has rights to create/delete realm users and roles.
2. **Dry Run (Optional but recommended)**
   - Execute `dotnet run --project tools/IntelliFin.UserMigration migrate --skip-validation` against a staging Keycloak realm.
   - Review the generated report under the configured `ReportsDirectory`.
3. **Production Migration**
   - Execute `dotnet run --project tools/IntelliFin.UserMigration migrate`.
   - Monitor console output and the generated report for failures.
   - If failures occur, inspect the JSON report and Keycloak logs; re-run after addressing data issues.
4. **Validation Only**
   - After the migration completes, run `dotnet run --project tools/IntelliFin.UserMigration validate` to verify counts on demand.
5. **Rollback (if required)**
   - Execute `dotnet run --project tools/IntelliFin.UserMigration rollback` to remove migrated entities from Keycloak and clear mapping tables.
   - Confirm that the legacy Identity database still authenticates users during rollback.

## Validation Checklist

| Validation | Command | Expected Result |
| ---------- | ------- | --------------- |
| User Count | `dotnet run --project tools/IntelliFin.UserMigration validate` | `Users=True` in console output and report. |
| Role Count | Same as above | `Roles=True`. |
| Assignment Count | Same as above | `Assignments=True`. |
| Sample Review | Inspect JSON report | `SampleErrors` array is empty. |
| Mapping Verification | Query `UserIdMapping`, `RoleIdMapping` | Every migrated entity has mapping rows. |

## Rollback Procedure

The rollback command performs the following actions:

1. Deletes all Keycloak users referenced in `UserIdMapping`.
2. Deletes Keycloak realm roles referenced in `RoleIdMapping`.
3. Clears mapping tables and writes an entry to `MigrationAuditLog`.

Rollback does **not** modify the ASP.NET Core Identity database; the Identity Service continues using the legacy provider while remediation occurs.

## Reporting and Compliance

- JSON reports generated by the tool should be uploaded to MinIO (`compliance/migrations/identity/`) and retained for seven years.
- Include the report link and console summary in the migration change record for audit tracking.
- Update the System Administration PROGRESS_REPORT with migration status after completion.

## Troubleshooting

| Symptom | Possible Cause | Resolution |
| ------- | -------------- | ---------- |
| Keycloak API returns `401 Unauthorized` | Invalid client credentials or realm mismatch | Confirm `Keycloak:TokenRealm`, client ID, and secret. Refresh Vault secret if expired. |
| Duplicate user errors | User already migrated or present manually in Keycloak | Check `UserIdMapping`. Remove stale mapping or rollback individual user via Keycloak admin console. |
| Validation assignment mismatch | Missing composite role mapping | Ensure custom composite roles are represented in `AspNetUserRoles` and re-run role migration. |
| Tool exits early without output | Configuration validation failed | Run with `--help` to confirm arguments; check console logs for configuration validation errors. |

## References

- [Story 1.2 Requirements](stories/story-1.2-user-migration.md)
- [System Administration Control Plane Architecture](system-administration-control-plane-architecture.md)
- [Keycloak Deployment Guide](keycloak-setup.md)
