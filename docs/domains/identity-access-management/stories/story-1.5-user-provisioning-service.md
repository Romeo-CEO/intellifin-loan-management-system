# Story 1.5: User Provisioning Service

## Story Information

Epic: Foundation Setup (Epic 1)
Story ID: 1.5  
Name: User Provisioning Service  
Priority: Critical  
Effort: 5 SP (8–12 hours)  
Dependencies: 1.2 (Realm), 1.3 (SDK/DI)  
Blocks: 1.6 (OIDC flow), 2.1 (Tenant), 3.1 (Service Accounts)

---

## Story Statement

As a Backend Developer, I want to provision and synchronize users from SQL Server to Keycloak so that Keycloak becomes the IdP while preserving existing roles, permissions, branch and tenant attributes.

---

## Acceptance Criteria

### Functional
- [ ] AC1: Provision single user by Id (create if missing, update if exists) including attributes and realm roles.
- [ ] AC2: Bulk provisioning job processes all active users; supports dry-run with diff summary (create/update/skip/fail counts).
- [ ] AC3: Change-driven sync: enqueue provisioning when user updated, role assignments change, or tenant membership changes.
- [ ] AC4: Idempotency: repeated provisioning produces no duplicate roles or attributes.
- [ ] AC5: Audit events recorded: ProvisionStarted, ProvisionSucceeded, ProvisionFailed with correlationId and details.

### Non-Functional
- [ ] AC6: Throughput ≥ 50 users/sec in bulk; p95 < 500ms/user.  
- [ ] AC7: Resiliency: transient failures retried (3x exponential backoff); dead-letter after 5 failures with alert.  
- [ ] AC8: Rate limiting respected; backoff on 429s; circuit opens after 5 consecutive failures for 1 minute.  

---

## Technical Specification

- Location: `IntelliFin.IdentityService/Services/KeycloakProvisioningService.cs`
- Interface:
```csharp path=null start=null
public interface IKeycloakUserProvisioningService
{
    Task<ProvisioningResult> ProvisionUserAsync(string userId, CancellationToken ct = default);
    Task<ProvisioningResult> SyncUserAsync(string userId, CancellationToken ct = default);
    Task<BulkProvisioningResult> ProvisionAllUsersAsync(bool dryRun = false, CancellationToken ct = default);
}
```
- Data sources: `AspNetUsers`, `AspNetUserRoles`, `AspNetRoleClaims`, `TenantUsers`.
- Keycloak representation:
  - Search: by attribute `extUserId` = `AspNetUsers.Id`; fallback by `email` or `username`.
  - Attributes: `branchId`, `branchName`, `branchRegion`, `tenantId[]`, `tenantName[]`, `permissions[]`.
  - Roles: map ASP.NET roles to Keycloak realm roles (auto-create when enabled via config `AllowRoleAutoCreate`).
- Queue: `IBackgroundQueue<ProvisionCommand>` using in-memory for dev; interface designed to allow Redis Streams later.
- Retry/circuit: Polly policies shared via DI (3 retries 2^n; circuit 5 failures/1m).
- Audit: `AuditEvents` table + forward to Admin Service; include `ActorId`, `Entity=User`, `EntityId=userId`, `Details` diff.
- Config flags: `FeatureFlags.EnableUserProvisioning`, `Provisioning.AllowRoleAutoCreate`.

### Mapping Rules
- Username/email -> Keycloak username/email.
- Names -> firstName/lastName.
- Branch -> attributes `branchId`, `branchName`, `branchRegion`.
- Tenant membership -> arrays `tenantId[]`, `tenantName[]`.
- Permissions -> flattened unique list derived from role claims (type = permission).  

---

## Implementation Steps

1) Contracts and Models
- Create `ProvisioningResult`, `BulkProvisioningResult` with counts and messages.
- Create `ProvisionCommand { string UserId; string Reason; Guid CorrelationId; }`.

2) Service Implementation
```csharp path=null start=null
public class KeycloakProvisioningService : IKeycloakUserProvisioningService
{
    // ctor: inject DbContext, Keycloak admin client, logger, queue, policies, options
    public async Task<ProvisioningResult> ProvisionUserAsync(string userId, CancellationToken ct)
    {
        // 1) Load user + roles + claims + tenants from SQL
        // 2) Resolve Keycloak user by extUserId/email/username
        // 3) Create or update user (attributes, enabled state)
        // 4) Ensure realm roles exist (optional auto-create)
        // 5) Assign roles (idempotent)
        // 6) Compute permissions[] from role claims and set as attribute
        // 7) Audit success/error
    }
}
```

3) Background Worker
- `ProvisioningWorker : BackgroundService` consumes queue, executes `ProvisionUserAsync`, applies retry/circuit policies, and dead-letters on repeated failure.

4) Change Hooks
- In `UserService`, `RoleService`, and `TenantService`, publish `ProvisionCommand` on relevant mutations when `FeatureFlags.EnableUserProvisioning` is true.

5) DI Wiring
- Register service and worker in `ServiceCollectionExtensions.AddKeycloakIntegration` when provisioning enabled.

---

## Example Payloads

### Keycloak Admin API (Create User)
```json path=null start=null
{
  "username": "john.doe",
  "email": "john.doe@intellifin.com",
  "firstName": "John",
  "lastName": "Doe",
  "enabled": true,
  "attributes": {
    "extUserId": ["550e8400-e29b-41d4-a716-446655440000"],
    "branchId": ["123e4567-e89b-12d3-a456-426614174000"],
    "branchName": ["Lusaka Branch"],
    "branchRegion": ["Central"],
    "tenantId": ["789e0123-e45b-67c8-d901-234567890abc"],
    "tenantName": ["ABC Microfinance"],
    "permissions": ["loans:create","loans:view"]
  }
}
```

---

## Testing Requirements

### Unit Tests (`IntelliFin.IdentityService.Tests/Services/KeycloakProvisioningServiceTests.cs`)
- LoadUserData_MapsAttributesAndRoles_Correctly().
- ProvisionUserAsync_UserNotExists_CreatesWithAttributes().
- ProvisionUserAsync_UserExists_UpdatesAttributesIdempotently().
- ProvisionAllUsersAsync_DryRun_ProducesDiffSummary().
- RetryOnTransientFailure_ThenSuccess().

### Integration Tests (Keycloak test realm)
- Create user in SQL -> provision -> verify Keycloak attributes, roles.
- Change role in SQL -> provision -> verify realm roles updated.
- Remove tenant -> provision -> verify tenant attributes updated.

### Performance
- Seed 10k users and run bulk; assert throughput and p95; assert no 429 after backoff.

---

## Integration Verification
- [ ] Keycloak user has `extUserId` attribute = SQL user Id.  
- [ ] Realm roles match SQL roles (or auto-created when enabled).  
- [ ] Attributes include branch*, tenant*, permissions[].  
- [ ] AuditEvents captured with ProvisionSucceeded.  
- [ ] Dead-letter queue empty after run.

---

## Rollback Plan
- Disable `FeatureFlags.EnableUserProvisioning` (immediate stop of worker).
- Re-run bulk provisioning after fix; dead-letter items retried via admin command.

---

## Definition of Done
- Implementation complete with background worker and hooks.  
- Unit/integration/performance tests pass.  
- Observability in place (Serilog/Otel metrics for counts, durations, failures).  
- Documentation updated under `domains/identity-access-management/`.
