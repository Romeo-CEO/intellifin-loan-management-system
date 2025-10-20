# Story 2.1: Tenant Management Service

## Story Information
Epic: Tenancy (Epic 2)  
Story ID: 2.1  
Priority: High  
Effort: 3 SP (4–6 hours)  
Dependencies: 1.1 (Schema), 1.5 (optional sync)  
Blocks: 2.2 (API)

---

## Story Statement
As a Platform Admin, I want to manage tenants and memberships so users can be scoped to organizations with proper roles and auditability.

---

## Acceptance Criteria

### Functional
- [ ] AC1: CreateTenantAsync enforces unique `Code`, persists `Tenants` row, returns DTO with Id/Name/Code/IsActive/CreatedAt.
- [ ] AC2: AssignUserToTenantAsync upserts `TenantUsers` (composite PK) with optional role and audit event.
- [ ] AC3: RemoveUserFromTenantAsync deletes membership; cascades validated; audited.
- [ ] AC4: ListTenantsAsync supports paging (page/pageSize) and filtering by `isActive`.
- [ ] AC5: On membership change, trigger provisioning (if `EnableUserProvisioning`).

### Non-Functional
- [ ] AC6: Transactions wrap multi-row ops; idempotent assignment.  
- [ ] AC7: Concurrency-safe unique code with DB index + conflict handling.

---

## Technical Specification

- Service interface:
```csharp path=null start=null
public interface ITenantService
{
    Task<TenantDto> CreateTenantAsync(TenantCreateRequest request, CancellationToken ct = default);
    Task AssignUserToTenantAsync(Guid tenantId, string userId, string? role, CancellationToken ct = default);
    Task RemoveUserFromTenantAsync(Guid tenantId, string userId, CancellationToken ct = default);
    Task<PagedResult<TenantDto>> ListTenantsAsync(int page, int pageSize, bool? isActive, CancellationToken ct = default);
}
```
- EF: Use `DbSet<Tenant>` and `DbSet<TenantUser>`; composite key (TenantId, UserId) already configured.
- Validation: FluentValidation on `TenantCreateRequest { Name, Code, Settings? }` (Code: [a-z0-9-], 3–50 chars).
- Audit: `AuditEvents` with `Action` = TenantCreated/UserAssigned/UserRemoved.
- Provisioning: publish `ProvisionCommand(userId, reason="MembershipChanged")` when enabled.

---

## Implementation Steps
1) DTOs and Validators
- Create `TenantCreateRequest`, `TenantDto`, `UserAssignmentRequest` and validators.

2) TenantService
- CreateTenantAsync: check code exists, insert, audit, return DTO.
- AssignUserToTenantAsync: upsert via `FirstOrDefault` + add/update; audit; publish provisioning.
- RemoveUserFromTenantAsync: delete link; audit; publish provisioning.
- ListTenantsAsync: apply paging and filter.

3) Error Handling
- Map `DbUpdateException` unique index to HTTP 409 in API layer (Story 2.2).

---

## Testing Requirements

### Unit Tests (`TenantServiceTests.cs`)
- CreateTenantAsync_UniqueCode_Succeeds().
- CreateTenantAsync_DuplicateCode_ThrowsConflict().
- AssignUserToTenantAsync_DoubleAssign_Idempotent().
- RemoveUserFromTenantAsync_RemovesLink_AndAudits().
- ListTenantsAsync_PagedAndFiltered().

### Integration Tests
- With SQL provider (SQLite in-memory) enforce composite keys and unique index; verify cascade behavior on tenant delete.

---

## Integration Verification
- [ ] Unique index `IX_Tenants_Code` enforced; duplicate rejected.  
- [ ] `TenantUsers` contains exactly one row per (TenantId, UserId).  
- [ ] AuditEvents created for each operation.

---

## Rollback Plan
- No schema changes; disable membership-triggered provisioning via feature flag if needed.

---

## Definition of Done
- Service implemented and tested with audits and optional provisioning hooks.  
- Docs updated; used by Story 2.2 API.
