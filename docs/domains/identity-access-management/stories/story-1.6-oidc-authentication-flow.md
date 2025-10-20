# Story 1.6: OIDC Authentication Flow

## Story Information
Epic: Foundation Setup (Epic 1)  
Story ID: 1.6  
Priority: Critical  
Effort: 5 SP (8–12 hours)  
Dependencies: 1.2 (Realm), 1.3 (Libraries), 1.4 (Dual Validation)

---

## Story Statement
As a Backend/Platform Developer, I want to implement the OIDC Authorization Code flow with PKCE so users authenticate via Keycloak while preserving existing response shapes for clients during migration.

---

## Acceptance Criteria

### Functional
- [ ] AC1: GET `/api/auth/oidc/login` generates PKCE and state, stores in Redis (10 min TTL), and redirects to Keycloak authorize endpoint.
- [ ] AC2: GET `/api/auth/oidc/callback` validates state and PKCE, exchanges code for tokens, validates nonce, creates local session, and returns user payload compatible with legacy format plus tenant fields.
- [ ] AC3: POST `/api/auth/oidc/logout` clears local session and returns Keycloak end-session URL (or executes remote logout when configured).
- [ ] AC4: Events logged: LoginStarted, LoginSucceeded, LoginFailed with correlationId.

### Security/Non-Functional
- [ ] AC5: PKCE S256; `state` bound to user agent via cookie; SameSite=Strict, HttpOnly, Secure cookies.
- [ ] AC6: p95 end-to-end login < 2s under nominal load; no PII leaked in logs.

---

## Technical Specification

- Controller: `Controllers/OidcController.cs`
```csharp path=null start=null
[ApiController]
[Route("api/auth/oidc")]
public class OidcController : ControllerBase
{
    [HttpGet("login")] public Task<IActionResult> Login([FromQuery] string? returnUrl);
    [HttpGet("callback")] public Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state);
    [HttpPost("logout")] public Task<IActionResult> Logout([FromBody] LogoutRequest req);
}
```
- Services: `IKeycloakService` (auth endpoints), `ISessionService` (cookie/session), `IRedisCache` for `state`/`pkce`.
- Redis keys: `oidc:state:{guid}`, `oidc:pkce:{guid}`; values include `uaHash` to bind to user agent.
- PKCE: `code_verifier` 43–128 chars; challenge = `BASE64URL(SHA256(verifier))`.
- Nonce: store in cookie `oidc.nonce` (HttpOnly) or Redis; validate against ID token claim.
- Response DTO: Reuse existing `TokenResponse` shape, adding nullable tenant fields; ensure backward compatibility.

### Login Flow
1. Generate `state` (guid) and `code_verifier`; compute `code_challenge` S256.  
2. Persist `state` and `code_verifier` in Redis with UA hash and optional returnUrl.  
3. Redirect 302 to: `{Authority}/protocol/openid-connect/auth?...&state={state}&code_challenge={challenge}&code_challenge_method=S256`.

### Callback Flow
1. Validate `state` from Redis (exists, not expired, UA matches).  
2. Exchange `code` + `code_verifier` for tokens via token endpoint.  
3. Validate ID token signature, nonce, exp, iss/aud.  
4. Fetch userinfo; map to DTO (username, names, email, roles, permissions, branch*, tenant*).  
5. Create session (cookies) and return JSON payload for SPA/mobile.

### Logout Flow
- Clear local session and return `end_session_endpoint?id_token_hint={idToken}` and post-logout redirect url; when configured, execute backchannel logout.

---

## Implementation Steps

1) State/PKCE Manager
```csharp path=null start=null
public sealed class OidcStateStore
{
    public Task StoreAsync(string state, string verifier, string? returnUrl, string uaHash);
    public Task<(string Verifier, string? ReturnUrl, string UaHash)?> GetAsync(string state);
    public Task RemoveAsync(string state);
}
```

2) OidcController
- Implement Login: generate state/pkce, cache, set minimal cookie, redirect.
- Implement Callback: validate, exchange via `IdentityModel`, validate tokens, create session.
- Implement Logout: clear session, return logout url.

3) Mapping
- Map Keycloak claims to existing DTO; compute permissions array if not present in token by calling permission API or from userinfo/roles.

4) DI and Feature Flag
- Guard endpoints with `FeatureFlags.EnableKeycloakIntegration`; keep legacy `/api/auth/login` operational.

---

## Example Requests/Responses

### Initiate Login
```http path=null start=null
GET /api/auth/oidc/login?returnUrl=/dashboard HTTP/1.1
```
302 Location: `{authority}/auth?...&state=...&code_challenge=...&code_challenge_method=S256`

### Callback Response
```json path=null start=null
{
  "accessToken": "...",
  "refreshToken": "...",
  "idToken": "...",
  "expiresIn": 3600,
  "tokenType": "Bearer",
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "username": "john.doe",
    "email": "john.doe@intellifin.com",
    "firstName": "John",
    "lastName": "Doe",
    "roles": ["LoanOfficer"],
    "permissions": ["loans:create","loans:view"],
    "branchId": "123e4567-e89b-12d3-a456-426614174000",
    "branchName": "Lusaka Branch",
    "branchRegion": "Central",
    "tenantId": "789e0123-e45b-67c8-d901-234567890abc",
    "tenantName": "ABC Microfinance"
  }
}
```

---

## Testing Requirements

### Unit Tests (`OidcControllerTests.cs`)
- Login_GeneratesStateAndRedirects().
- Callback_InvalidState_Returns400().
- Callback_InvalidNonce_Returns401().
- Callback_ValidCode_ReturnsSessionAndPayload().
- Logout_ClearsSession_ReturnsLogoutUrl().

### Integration Tests
- Full browser-like flow with test realm; verify cookies and response payload shape.
- Negative: tampered state, wrong UA, expired state, wrong PKCE -> rejected.

### Security Tests
- CSRF on callback/logout blocked; SameSite=Strict verified.

---

## Integration Verification
- [ ] OIDC discovery reachable; token and userinfo endpoints responsive.  
- [ ] Redis keys for state/pkce created and expire correctly.  
- [ ] Session cookies present with HttpOnly/Secure/SameSite.  
- [ ] Dual validation in gateway accepts Keycloak JWT tokens.  

---

## Rollback Plan
- Toggle `EnableKeycloakIntegration=false`; keep legacy login as primary.

---

## Definition of Done
- Endpoints implemented with feature flag guard.  
- Unit/integration/security tests pass.  
- Observability in place for login metrics/errors.  
- Swagger updated; docs added.
