# Story 4.1: SoD Validation Service

## Story Information
Epic: Governance (Epic 4)  
Story ID: 4.1  
Priority: High  
Effort: 3 SP (4–6 hours)  
Dependencies: 1.1 (Schema)  
Blocks: 4.2

---

## Story Statement
As a Security Officer, I want SoD enforcement to prevent conflicting permissions so we maintain compliance and reduce fraud risk.

---

## Acceptance Criteria
- [ ] AC1: ValidateRoleAssignmentAsync blocks (Enforcement=strict) or warns (Enforcement=warning) on conflicts as defined in `SoDRules`.
- [ ] AC2: DetectViolationsAsync returns a report of current users violating rules.
- [ ] AC3: Violations are audited with actionable details.
- [ ] AC4: Validation query executes < 100ms typical.

---

## Technical Specification

- Interface:
```csharp path=null start=null
public interface ISoDValidationService
{
    Task<SoDValidationResult> ValidateRoleAssignmentAsync(string userId, string roleId, CancellationToken ct = default);
    Task<SoDValidationResult> ValidatePermissionConflictsAsync(string userId, string[] newPermissions, CancellationToken ct = default);
    Task<SoDViolationReport> DetectViolationsAsync(CancellationToken ct = default);
}
```
- Rule storage: `SoDRules` rows with `ConflictingPermissions` JSON array; each rule evaluated against the union of current and prospective permissions.
- Permission source: union of role claims (`AspNetRoleClaims where ClaimType='permission'`) for user (existing + candidate role).
- Integration point: `RoleService.AddToRoleAsync` and `RemoveFromRoleAsync` must call validation before persist.
- Audit: `Action='SoDViolation'`, include rule name, conflicting permissions, userId, attempted role.

---

## Implementation Steps
1) Implement permission aggregation for user + candidate role.
2) Implement rule evaluation: conflict if intersection of permissions contains both sides of a rule pair/group.
3) Enforce behavior based on `Enforcement` flag; block with domain exception for `strict`.
4) Wire into role assignment flows; add unit tests.

---

## Testing Requirements
- Assign conflicting roles -> blocked; audit created.  
- Assign warning rule -> allowed; warning audit created.  
- DetectViolationsAsync returns offenders for seeded data.

---

## Definition of Done
- Service integrated with role assignment; tests and audits in place; perf validated.
