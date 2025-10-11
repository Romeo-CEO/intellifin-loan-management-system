# Requirements Summary

This document summarizes the functional, non-functional, and compatibility requirements for the System Administration Control Plane enhancement.

---

## Functional Requirements (27 Total)

### Identity & Access Management (FR1-FR11)

**FR1**: Admin microservice unified API for user/role/permission management  
**FR2**: Keycloak self-hosted IdP with OIDC/OAuth2  
**FR3**: Branch-scoped JWT claims (branchId, branchName, branchRegion)  
**FR4**: Rotating refresh tokens with Redis token family tracking  
**FR5**: Azure AD B2C federation support (optional)  
**FR6**: Expanded operational roles with role hierarchy  
**FR7**: Segregation of Duties (SoD) enforcement  
**FR8**: Just-in-time (JIT) privilege elevation via Camunda workflows  
**FR9**: Step-up MFA for sensitive operations  
**FR10**: Automated user lifecycle management (JML - Joiner/Mover/Leaver)  
**FR11**: Quarterly access recertification workflows  

### Audit & Compliance (FR12-FR16)

**FR12**: Centralized audit event collection in Admin Service  
**FR13**: Tamper-evident cryptographic chaining  
**FR14**: MinIO WORM storage with 7-year retention (BoZ compliance)  
**FR15**: W3C Trace Context correlation ID propagation  
**FR16**: Offline CEO app audit merge capability  

### Configuration & Secrets (FR17-FR18)

**FR17**: Policy-driven configuration changes with Camunda approval  
**FR18**: Vault integration for dynamic secrets with 90-day rotation  

### Security & Infrastructure (FR19-FR23)

**FR19**: Container image signing (Cosign) and SBOM generation (Syft)  
**FR20**: GitOps configuration management with ArgoCD  
**FR21**: Mutual TLS (mTLS) for inter-service communication  
**FR22**: Kubernetes NetworkPolicies for micro-segmentation  
**FR23**: OpenTelemetry SDK integrated across all services  

### Observability & Operations (FR24-FR27)

**FR24**: Grafana dashboards (BoZ compliance, cost-performance, SLA)  
**FR25**: Bastion host PAM with JIT access and session recording  
**FR26**: Time-bound infrastructure access with auto-expiration  
**FR27**: Automated DR runbooks with quarterly testing (RPO <1hr, RTO <4hr)  

---

## Non-Functional Requirements (20 Total)

### Performance (NFR1-NFR5)

**NFR1**: Admin microservice 99.9% availability  
**NFR2**: JWT validation <50ms p95  
**NFR3**: Branch claims reduce DB queries by 80% (200ms → 120ms)  
**NFR4**: Audit ingestion 10,000 events/sec peak  
**NFR5**: Audit chain verification <5 seconds for 1M records  

### Observability Performance (NFR6-NFR8)

**NFR6**: OpenTelemetry adaptive sampling (100% errors, 10% normal), <1TB/year traces  
**NFR7**: Prometheus 30-day retention, <500GB storage  
**NFR8**: Loki 90-day retention, <2TB storage  

### Security Performance (NFR9-NFR11)

**NFR9**: mTLS handshake <20ms p95 overhead  
**NFR10**: Keycloak 1,000 concurrent auth requests, <500ms p95  
**NFR11**: JIT elevation approval notification <15 seconds  

### Deployment & Operations (NFR12-NFR15)

**NFR12**: GitOps deployment <5 minutes, auto-rollback in 2 minutes  
**NFR13**: Vault secret rotation zero downtime, 5-minute overlap  
**NFR14**: Container image CVE scanning blocks critical vulnerabilities  
**NFR15**: PAM session recordings uploaded <30 seconds  

### System Constraints (NFR16-NFR20)

**NFR16**: Maintain existing memory footprint per service (±20%)  
**NFR17**: DR failover achieves RPO <1hr, RTO <4hr (quarterly validated)  
**NFR18**: PII auto-redacted in logs/traces (Zambian Data Protection Act)  
**NFR19**: BoZ compliance (annual pen testing, quarterly vulnerability scans)  
**NFR20**: ADRs documented for all major technology choices  

---

## Compatibility Requirements (8 Total)

### System Compatibility

**CR1: API Compatibility** - All existing REST endpoints functional, dual-token support (30 days)  
**CR2: Database Compatibility** - User/role tables preserved, foreign keys maintained  
**CR3: UI/UX Consistency** - Existing IntelliFin visual identity preserved  
**CR4: Integration Compatibility** - External integrations (PMEC, TransUnion, Tingg, SMS) unaffected  

### Application Compatibility

**CR5: Offline App Compatibility** - CEO desktop app sync protocol backward compatible  
**CR6: RabbitMQ Compatibility** - Message contracts unchanged, correlation ID additive  
**CR7: Audit Compatibility** - Existing audit events queryable, dual schema support  
**CR8: Configuration Compatibility** - Dual config sources (appsettings.json + Vault) during migration  

---

## Requirement Traceability

Each story in the epic maps to specific requirements. See phase story documents for detailed traceability:

- **Phase 1 Stories** → FR2, FR23, NFR6-NFR8 (Foundation: Keycloak, OpenTelemetry)
- **Phase 2 Stories** → FR3-FR4, FR21-FR22, NFR2-NFR3, NFR9 (Security: mTLS, branch claims)
- **Phase 3 Stories** → FR12-FR16, NFR4-NFR5 (Audit: Centralized, tamper-evident, WORM)
- **Phase 4 Stories** → FR6-FR11, FR17-FR18 (Governance: JIT, MFA, SoD, Vault rotation)
- **Phase 5 Stories** → FR19-FR20, FR25-FR26 (Zero-Trust: GitOps, PAM, bastion)
- **Phase 6 Stories** → FR24, FR27, NFR17 (Observability: Dashboards, DR automation)

---

## Related Documents

- **Epic Overview**: `epic-overview.md`
- **Full PRD**: `../system-administration-control-plane-prd.md`
- **Architecture**: `../system-administration-control-plane-architecture.md`
