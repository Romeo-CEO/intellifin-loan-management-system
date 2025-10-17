# Story 5.1: Comprehensive Testing & Documentation

## Story Information
Epic: Hardening & Documentation (Epic 5)  
Story ID: 5.1  
Priority: High  
Effort: 5 SP (8–12 hours)  
Dependencies: 1.1–4.2

---

## Story Statement
As a QA/Platform Team, we need comprehensive tests and updated documentation to verify quality, performance, security, and provide operational guidance.

---

## Acceptance Criteria
- [ ] AC1: Unit coverage ≥ 80% overall, ≥ 85% for new IAM components.
- [ ] AC2: Integration tests validate: OIDC flow, dual token gateway, provisioning, tenancy, service accounts, introspection, SoD.
- [ ] AC3: Performance: auth p95 ≤ 250ms; provisioning ≥ 50 users/sec; gateway p95 ≤ 200ms token validation.
- [ ] AC4: Security: PKCE/state/nonce enforced; token revocation effective; SoD rules applied; no secrets in logs.
- [ ] AC5: Documentation updated: architecture deltas, API reference (Swagger), migration guide, ops runbook.

---

## Technical Specification

### Test Projects
- Extend `IntelliFin.IdentityService.Tests` with suites for Services, Controllers, HealthChecks.  
- Add integration suite using test Keycloak realm and ephemeral SQL/Redis.

### Performance
- k6/Locust scenarios: login flow (OIDC), gateway validation (custom vs Keycloak), provisioning bulk throughput.

### Security
- OIDC negative tests (CSRF/state replay/nonce mismatch).  
- Secret scanning in CI; ensure no plaintext secrets.

### Documentation
- Swagger examples for new endpoints; story links back to architecture sections.  
- Migration Guide: feature flag rollout plan, dual validation period, rollback steps.  
- Ops Runbook: Keycloak outage handling, secret rotation, token revocation, DLQ reprocessing.

---

## Tasks / Subtasks
- [ ] Add missing unit tests per stories 1.1–4.2.  
- [ ] Build integration environment (docker-compose or test containers).  
- [ ] Author k6 scripts and run baselines; attach results.  
- [ ] Run security checks (OWASP ZAP passive scan for OIDC endpoints).  
- [ ] Update docs in `domains/identity-access-management/` and Swagger.  
- [ ] Produce release readiness checklist.

---

## Verification
- CI green (build, tests, analyzers).  
- Perf results attached and within thresholds.  
- Docs reviewed and approved by Architect/PM.

---

## Definition of Done
- Tests comprehensive and passing; docs updated; sign-off from stakeholders; release checklist prepared.
