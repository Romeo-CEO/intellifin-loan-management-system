# Epic Overview: System Administration Control Plane

**Epic**: System Administration Control Plane: Enterprise Governance & Observability  
**Project**: IntelliFin Loan Management System  
**Type**: Brownfield Enhancement (Major)  
**Duration**: 12 months (48-60 weeks)  
**Total Stories**: 34 stories across 6 phases

---

## Epic Goal

Transform IntelliFin's System Administration from a tactical support layer into a strategic control plane that orchestrates identity, access, policy, audit, and operational governance across all microservices, enabling Bank of Zambia compliance, zero-trust security, comprehensive observability, and scalable operations for regulated financial services in Zambia.

---

## Current System State

**IntelliFin Loan Management System** is a cloud-native, microservices-based loan management platform deployed in Zambia for data sovereignty compliance.

### Architecture
- 9+ microservices (IdentityService, ApiGateway, ClientManagement, LoanOrigination, FinancialService, Communications, Collections, etc.)
- Technology Stack: .NET 9 (C# 12), ASP.NET Core, SQL Server 2022, Redis, RabbitMQ, Camunda 8 (Zeebe), MinIO, HashiCorp Vault

### Current System Administration
- ASP.NET Core Identity embedded in IdentityService
- Basic RBAC with 6-8 V1 business roles
- JWT authentication (15-min access, 7-day refresh)
- Audit trail implemented in FinancialService (architectural misplacement)
- Vault configured but underutilized
- No observability stack (major gap)
- No PAM or bastion access

---

## Enhancement Goals

1. **Establish Admin microservice as governance orchestration hub** coordinating identity, access, policy, audit, and operational controls
2. **Implement self-hosted IdP (Keycloak)** with branch-scoped JWTs, rotating refresh tokens, and optional AAD B2C federation
3. **Expand RBAC** to operational roles (Collections, Compliance, Treasury, GL, Auditors, Risk, Branch Mgmt) with SoD enforcement, MFA, and JIT elevation
4. **Harden audit trail** with tamper-evident cryptographic chains, MinIO WORM retention (7-year BoZ compliance), global correlation IDs
5. **Enforce policy-driven configuration** with Vault secret rotation, GitOps config-as-code, signed SBOM-scanned images
6. **Implement zero-trust runtime** with mTLS service-to-service encryption, Kubernetes NetworkPolicies
7. **Deploy in-country observability stack** (OpenTelemetry + Prometheus/Grafana + Loki + Jaeger) for data sovereignty
8. **Mature operational resilience** with tested DR/backup runbooks, automated RPO/RTO validation
9. **Add bastion-based PAM** with JIT infrastructure access, Camunda approval workflows, session recording
10. **Enable cost-performance monitoring** to optimize infrastructure spend

---

## Epic Structure

### Phase 1: Foundation (Months 1-2) - 9 Stories
**Focus**: Keycloak, Admin Service, OpenTelemetry, Jaeger, Prometheus, Grafana, Loki

Stories: 1.1-1.9

### Phase 2: Enhanced Security (Months 3-4) - 4 Stories
**Focus**: Rotating tokens, branch claims, mTLS, NetworkPolicies

Stories: 1.10-1.13

### Phase 3: Audit & Compliance (Months 5-6) - 5 Stories
**Focus**: Centralized audit, tamper-evident chain, WORM, correlation IDs, offline merge

Stories: 1.14-1.18

### Phase 4: Governance & Workflows (Months 7-8) - 6 Stories
**Focus**: JIT elevation, MFA, SoD, policy config, Vault rotation, recertification

Stories: 1.19-1.24

### Phase 5: Zero-Trust & PAM (Months 9-10) - 5 Stories
**Focus**: GitOps, image signing, bastion, JIT infra access, session recording

Stories: 1.25-1.29

### Phase 6: Advanced Observability (Months 11-12) - 5 Stories
**Focus**: BoZ dashboards, cost monitoring, alerting, DR runbooks, DR testing

Stories: 1.30-1.34

---

## Technology Stack Decisions

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Identity Provider** | Keycloak 24+ | Self-hosted, OIDC/OAuth2, federation support, mature admin API |
| **Distributed Tracing** | Jaeger | OpenTelemetry native, proven scale, Zambian data sovereignty |
| **Centralized Logging** | Loki | Cost-effective, LogQL query, integrates with Prometheus/Grafana |
| **Metrics** | Prometheus + Grafana | Industry standard, Kubernetes native, rich ecosystem |
| **Telemetry SDK** | OpenTelemetry | Vendor-neutral, future-proof, single instrumentation |
| **Service Mesh** | Manual mTLS (cert-manager) | Lighter weight than Istio, sufficient for 9 services |
| **GitOps** | ArgoCD | Kubernetes native, declarative, audit trail, rollback |
| **PAM Solution** | HashiCorp Boundary | Vault integration, JIT access, session recording |
| **Container Signing** | Cosign + Syft | CNCF standard, SBOM generation |
| **Audit Storage** | MinIO (WORM mode) | S3-compatible, on-prem, object locking, 7-year retention |

---

## Total Estimated Effort

**~180-240 days** of development work (avg 6-8 days per story)

---

## Related Documents

- **Full PRD**: `../system-administration-control-plane-prd.md`
- **Architecture**: `../system-administration-control-plane-architecture.md`
- **Brownfield Analysis**: `../system-administration-brownfield-analysis.md`
- **Requirements**: `requirements.md`
- **Phase Stories**: `phase-{1-6}-stories.md`
