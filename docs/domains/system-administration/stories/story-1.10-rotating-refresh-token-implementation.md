# Story 1.10: Rotating Refresh Token Implementation

### Metadata
- **ID**: 1.10 | **Points**: 5 | **Effort**: 3-5 days | **Priority**: P1
- **Dependencies**: 1.3 (API Gateway Keycloak integration)
- **Blocks**: None

### User Story
**As a** security engineer,
**I want** Keycloak refresh tokens to rotate on every refresh operation,
**so that** we reduce security risk from long-lived refresh token theft.

### Acceptance Criteria
1. Keycloak realm configured with `Rotate Refresh Tokens` policy enabled
2. Redis tracking of refresh token families for revocation chain detection
3. Token revocation endpoint (`/api/auth/revoke`) extended to revoke entire token family
4. Frontend updated to handle refresh token rotation (store new refresh token from response)
5. Token theft detection: If revoked token in family used, entire family invalidated and user logged out
6. Audit events logged for refresh operations and token family revocations
7. Documentation updated with refresh token rotation flow diagrams

### Implementation Overview
- Enabled Keycloak realm policies to revoke refresh tokens immediately, forbid reuse (`refreshTokenMaxReuse=0`), and limit lifespan to 30 minutes.
- Added Redis-backed token family tracking with reuse detection, exponential backoff-safe revocation, and audit logging inside the Identity Service.
- Extended `/api/auth/refresh` to emit rotated tokens plus family metadata and introduced `/api/auth/revoke` to invalidate an entire refresh-token family.
- Updated the MAUI offline client to persist rotated refresh tokens securely, request new tokens automatically, and clear stored credentials when rotation fails.

### Runtime Notes
- **Keycloak Configuration**: `infra/keycloak/realm/IntelliFin-realm.json` sets `revokeRefreshToken`, `refreshTokenMaxReuse`, and `refreshTokenLifespan` for the IntelliFin realm.
- **Identity Service**: `JwtTokenService` coordinates Redis token families via `TokenFamilyService`, logs `refresh_token.rotated` and `refresh_token.family_revoked` audit events, and enforces reuse detection before issuing new tokens.
- **Client Updates**: `FinancialApiService` now calls `/api/auth/refresh`, persists the returned refresh token (secure storage fallback to preferences), and updates `AuthenticationService.RefreshTokenAsync` to respond to rotation results.

### Validation Checklist
- [ ] Keycloak admin console confirms realm policy changes after import.
- [ ] Redis shows `token_family`, `token_family_latest`, and `token_family_revoked` keys per session.
- [ ] `POST /api/auth/refresh` returns HTTP 200 with new refresh token while invalid reuse returns HTTP 401 and revokes family.
- [ ] `POST /api/auth/revoke` removes the family and subsequent refresh attempts fail with 401.
- [ ] MAUI app refreshes access tokens without user intervention and clears storage on failure.
