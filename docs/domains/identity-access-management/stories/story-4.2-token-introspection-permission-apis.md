# Story 4.2: Token Introspection & Permission APIs

## Story Information
Epic: Governance (Epic 4)  
Story ID: 4.2  
Priority: High  
Effort: 4 SP (6–8 hours)  
Dependencies: 1.1 (Schema), 1.4 (Dual Validation), 1.6 (OIDC)

---

## Story Statement
As a Backend Service, I want introspection and permission APIs so I can validate tokens and check authorization centrally.

---

## Acceptance Criteria
- [ ] AC1: POST `/api/auth/introspect` returns `{ active, sub, username, email, roles, permissions, branchId, tenantId, exp, iat, iss }` when valid, `active=false` otherwise.
- [ ] AC2: POST `/api/auth/check-permission` returns `{ allowed, reason }` considering branch/tenant context.
- [ ] AC3: Revoked tokens (Redis denylist or SQL TokenRevocations) return `active=false`.
- [ ] AC4: p95 < 100ms for introspection under normal load.

---

## Technical Specification

- Controller: `Controllers/AuthorizationController.cs`
```csharp path=null start=null
[ApiController]
[Route("api/auth")]
public class AuthorizationController : ControllerBase
{
    [HttpPost("introspect")] public Task<IActionResult> Introspect([FromBody] IntrospectionRequest req);
    [HttpPost("check-permission")] public Task<IActionResult> CheckPermission([FromBody] PermissionCheckRequest req);
}
```
- Introspection Steps:
  1) Determine token type by `iss`; validate signature via Keycloak JWKS or custom key.
  2) Validate audience/expiry; extract `jti` when present.
  3) Check Redis denylist `deny:{jti}` and SQL `TokenRevocations` table.
  4) Return claims mapping per AC.
- Permission Check:
  - Load user roles and `AspNetRoleClaims` (permission type); apply tenant/branch filters; compute `allowed` and `reason`.
- Security: restrict to internal services via policy (e.g., `system:token_introspect`).

### DTOs
```csharp path=null start=null
public record IntrospectionRequest(string Token, string? TokenTypeHint);
public record PermissionCheckRequest(string UserId, string Permission, PermissionContext? Context);
public record PermissionContext(Guid? BranchId, Guid? TenantId);
```

---

## Example
- Introspection Request
```json path=null start=null
{ "token": "eyJ...", "tokenTypeHint": "access_token" }
```
Response (valid):
```json path=null start=null
{ "active": true, "sub":"550e...", "username":"john.doe", "roles":["LoanOfficer"], "permissions":["loans:view"], "branchId":"123e...", "tenantId":"789e...", "exp":1697382000, "iat":1697378400, "iss":"https://keycloak.../IntelliFin" }
```

---

## Testing Requirements
- Valid token -> active=true with mapped claims.  
- Revoked jti -> active=false.  
- Unknown issuer -> 400.  
- Permission check allowed/denied with correct reason.

---

## Definition of Done
- Endpoints implemented, secured, documented, and tested with performance validated.
