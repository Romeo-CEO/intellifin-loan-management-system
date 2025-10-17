# Story 4.2: Token Introspection & Permission APIs

## Status
Completed ✅

## Acceptance Criteria
- [x] AC1: `/api/auth/introspect` validates issuer/signature, enriches claims, and returns the structured introspection payload with `active` state.
- [x] AC2: `/api/auth/check-permission` evaluates contextual permission rules and returns `{ allowed, reason }` with branch/tenant awareness.
- [x] AC3: Introspection checks Redis denylist (`revoked_token:`/`deny:`) and SQL `TokenRevocations` rows to return `active=false` for revoked tokens.
- [x] AC4: Added cached OIDC metadata retrieval and reused local validation parameters to keep the hot-path under 100 ms under normal load.

## Implementation Tasks
- [x] Model persistent token revocations and register EF Core mappings to support SQL deny-list lookups alongside Redis.
- [x] Implement `TokenIntrospectionService` with JWKS caching, issuer metadata resolution, revocation checks, and audit logging.
- [x] Implement `PermissionCheckService` to aggregate effective permissions, enforce branch/tenant context, and emit decision audits.
- [x] Expose secured `/api/auth/introspect` and `/api/auth/check-permission` endpoints with policy-based authorization, validation, and error handling.
- [x] Add configuration, DI registrations, and story documentation including developer notes.

## Developer Notes
- Analysed the existing `apps/IntelliFin.IdentityService` implementation (services, controllers, DI wiring) to align with current patterns before coding.
- Added a new `TokenRevocations` entity because no schema artifact existed; updated `LmsDbContext` and model snapshot manually as migrations are not part of the repo.
- Introduced an authorization configuration section to describe trusted issuers, audiences, and metadata endpoints—Keycloak settings default to the local dev realm.
- Implemented JWKS retrieval through `ConfigurationManager` with a dedicated HttpClient to avoid repeated metadata downloads; Redis denylist keys cover both previous (`revoked_token`) and new (`deny:`) patterns.
- Permission checks rely on the existing role/permission graph and user metadata for tenant context; if the metadata is missing the decision defaults to denial with a descriptive reason.
- The referenced IAM architecture documents (`iam-brownfield-assessment.md`, `iam-enhancement-architecture.md`, `iam-enhancement-prd.md`, `iam_migration.sql`) are still absent from the repository, so implementation proceeded based on available source analysis.
