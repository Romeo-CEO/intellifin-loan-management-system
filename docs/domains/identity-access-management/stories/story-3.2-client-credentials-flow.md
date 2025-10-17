# Story 3.2: Client Credentials Flow

## Story Information
Epic: Service-to-Service Auth (Epic 3)  
Story ID: 3.2  
Priority: High  
Effort: 3 SP (4–6 hours)  
Dependencies: 3.1 (Service Accounts), 1.2, 1.3

---

## Story Statement
As a Service, I want to exchange clientId/secret for an access token so I can call APIs securely using OAuth2 client credentials.

---

## Acceptance Criteria
- [ ] AC1: POST `/api/auth/service-token` validates `clientId` and BCrypt-verifies `clientSecret` against latest active credential.
- [ ] AC2: On success, proxy request to Keycloak token endpoint and return `access_token`, `expires_in`, `token_type`, `scope`.
- [ ] AC3: Inactive or revoked account/credential returns 401.
- [ ] AC4: Audit TokenIssued event (no secrets logged). Retries on Keycloak 5xx up to 3 times.

Non-Functional
- [ ] AC5: p95 < 200ms excluding network; structured logs with correlationId.

---

## Technical Specification

- Controller: `Controllers/ServiceAccountController.cs`
```csharp path=null start=null
[HttpPost("/api/auth/service-token")]
public async Task<IActionResult> GenerateTokenAsync([FromBody] ClientCredentialsRequest request)
{
    // 1) Validate clientId exists & account active
    // 2) Verify BCrypt secret against latest active credential
    // 3) Call Keycloak token endpoint with grant_type=client_credentials
    // 4) Map/return response; audit
}
```
- Request DTO: `ClientCredentialsRequest { string ClientId; string ClientSecret; string[]? Scopes; }`.
- Validation: Ensure account `IsActive` and credential not `RevokedAt`/expired.
- Keycloak call: `application/x-www-form-urlencoded` with `client_id` and `client_secret`.
- Retry: Polly on `HttpRequestException` and 5xx; exponential backoff.

---

## Example
```http path=null start=null
POST /api/auth/service-token
Content-Type: application/json

{
  "clientId": "loan-origination-service",
  "clientSecret": "{{REDACTED}}",
  "scopes": ["service:read","service:write"]
}
```

Response (200):
```json path=null start=null
{ "access_token":"...","expires_in":3600,"token_type":"Bearer","scope":"service:read service:write" }
```

---

## Testing Requirements
- Valid credentials -> 200 + token.  
- Invalid secret -> 401.  
- Inactive/Revoked -> 401.  
- Keycloak 500 -> retried; eventually 502 with ProblemDetails.

---

## Definition of Done
- Endpoint implemented, validated, audited, tested, and documented.
