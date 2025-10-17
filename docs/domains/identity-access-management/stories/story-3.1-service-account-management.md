# Story 3.1: Service Account Management

## Status
Completed ✅

## Story Information
Epic: Service-to-Service Auth (Epic 3)  
Story ID: 3.1  
Priority: High  
Effort: 4 SP (6–8 hours)  
Dependencies: 1.1 (Schema), 1.2 (Realm), 1.3 (SDK)  
Blocks: 3.2 (Client Credentials)

---

## Story Statement
As a Platform Engineer, I want to manage service principals and secrets so services can authenticate securely using OAuth2 client credentials.

---

## Acceptance Criteria

### Functional
- [x] AC1: CreateServiceAccountAsync creates `ServiceAccounts` and initial `ServiceCredentials` (BCrypt hashed), returns `clientId` and plaintext secret once.
- [x] AC2: RotateSecretAsync creates new credential, returns new secret, previous credential remains valid until revoked or expiry.
- [x] AC3: RevokeServiceAccountAsync deactivates account and revokes all active credentials.
- [x] AC4: Optional Keycloak client registration performed with `serviceAccountsEnabled=true` and client credentials grant.
- [x] AC5: Audit events for create/rotate/revoke with actor and reason.

### Security/Non-Functional
- [x] AC6: Secrets length ≥ 32, cryptographically random, never logged, stored only as BCrypt hash; min work factor per policy.

---

## Technical Specification

- Interface:
```csharp path=null start=null
public interface IServiceAccountService
{
    Task<ServiceAccountDto> CreateServiceAccountAsync(ServiceAccountCreateRequest request, CancellationToken ct = default);
    Task<ServiceCredentialDto> RotateSecretAsync(Guid serviceAccountId, CancellationToken ct = default);
    Task RevokeServiceAccountAsync(Guid serviceAccountId, CancellationToken ct = default);
}
```
- IDs: `clientId` generated from slug(name)+random suffix; ensure unique via index.
- Hashing: BCrypt.Net work factor from `PasswordConfiguration` (≥12).
- Vault: Only Keycloak admin client secrets go to Vault; local service secrets not stored in plaintext anywhere.
- Optional Keycloak client create via Admin API; map scopes to client roles if needed.

---

## Implementation Steps
1) DTOs: `ServiceAccountCreateRequest { Name, Description, Scopes[]? }`, `ServiceAccountDto`, `ServiceCredentialDto`.
2) Secret generator using `RandomNumberGenerator.GetBytes(48)` -> base64url.
3) Create: insert account + credential in transaction; audit; optional Keycloak client registration (store that secret in Vault).
4) Rotate: insert new credential; audit; optionally revoke old after grace period.
5) Revoke: set `IsActive=false`; set all credentials `RevokedAt=UtcNow`.

---

## Testing Requirements
- Create_Succeeds_ReturnsPlainSecretOnce().  
- Rotate_CreatesNewCredential_AndOldRemainsValidUntilRevoked().  
- Revoke_DeactivatesAccount_AndDeniesTokenIssuance().  
- SecretsNotLogged_AndMeetLengthRequirements().

---

## Integration Verification
- [ ] DB rows exist for account and credential; hashes non-empty.  
- [ ] Keycloak client (if configured) created and visible; secret stored in Vault.  
- [ ] Audit events created for all actions.

---

## Rollback Plan
- Delete Keycloak client (if created) and mark local account inactive; remove recent credential row.

---

## Definition of Done
- Lifecycle operations implemented with tests and audits; optional Keycloak client integration documented.

---

## Developer Notes
- Service layer implemented in `ServiceAccountService` with full CRUD operations (Create, Rotate, Revoke, Get, List).
- REST API endpoints exposed via `PlatformServiceAccountController` under `/api/platform/service-accounts` route:
  - `POST /api/platform/service-accounts` - Create service account
  - `POST /api/platform/service-accounts/{id}/rotate` - Rotate secret
  - `DELETE /api/platform/service-accounts/{id}` - Revoke account
  - `GET /api/platform/service-accounts/{id}` - Retrieve single account (no credentials)
  - `GET /api/platform/service-accounts?isActive={bool}` - List accounts with optional filter
- Authorization enforced via policy `platform:service_accounts` requiring scope claim.
- Secrets generated with 48 bytes (configurable) via `RandomNumberGenerator`, encoded as base64url, never logged.
- BCrypt hashing with work factor ≥12 (configurable via `PasswordConfiguration`).
- Audit events logged for all operations with actor, reason (from header `X-Audit-Reason` or explicit parameter), and metadata.
- Optional Keycloak client provisioning with secret stored in Vault (path recorded in `KeycloakSecretVaultPath`).
- ClientId format: `{slug}-{randomSuffix}` with uniqueness validation and 20 retry attempts.
- Comprehensive unit tests for service layer (`ServiceAccountServiceTests.cs`) and controller (`PlatformServiceAccountControllerTests.cs`).
- GET and LIST operations never return credential secrets; plaintext secret only returned once during Create or Rotate.
- Credential expiry configurable via `CredentialExpiryDays`; `null` or ≤0 means no expiry.
- Revocation is idempotent; revoking an already-revoked account succeeds without error.
