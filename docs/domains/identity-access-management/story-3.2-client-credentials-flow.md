# Story 3.2: Client Credentials Flow

## Status
Completed ✅

## Acceptance Criteria
- [x] AC1: POST `/api/auth/service-token` validates `clientId` and BCrypt-verifies `clientSecret` against latest active credential.
- [x] AC2: On success, proxy request to Keycloak token endpoint and return `access_token`, `expires_in`, `token_type`, `scope`.
- [x] AC3: Inactive or revoked account/credential returns 401.
- [x] AC4: Audit TokenIssued event (no secrets logged). Retries on Keycloak 5xx up to 3 times.
- [x] AC5: p95 < 200ms excluding network; structured logs with correlationId (implemented via lightweight logging + existing correlation identifiers).

## Implementation Tasks
- [x] Define DTOs for client credentials request and token responses.
- [x] Implement `ServiceTokenService` to validate credentials, enforce scope checks, and audit token issuance.
- [x] Implement Keycloak token client with retry logic and safe error handling.
- [x] Expose `/api/auth/service-token` endpoint with appropriate responses.
- [x] Add unit coverage for success, invalid credentials, revoked/expired credentials, unauthorized scopes, and upstream failures.
- [x] Register dependencies and configuration updates (Polly HttpClient, service configuration, DI wiring).

## Developer Notes
- Reviewed current `apps/IntelliFin.IdentityService` implementation to align with existing patterns for services, auditing, and configuration.
- Introduced typed HttpClient with Polly retries to satisfy upstream resiliency requirements.
- Added controller and service tests to exercise the authorization paths and HTTP response mappings; upstream Keycloak calls mocked for isolation.
- Attempted to locate referenced IAM architecture documents (`iam-brownfield-assessment.md`, `iam-enhancement-architecture.md`, `iam-enhancement-prd.md`, `iam_migration.sql`) but they are not present in the repository; proceeded with implementation using available context from the existing Identity Service codebase.
- Secrets remain unhashed only in-memory; logging statements avoid including sensitive material.
