# System Administration Control Plane - Architecture (Sharded)

This directory provides quick reference to the architecture document sections for IDE-friendly development.

---

## 📄 Full Architecture Document

**Primary Reference**: [`../system-administration-control-plane-architecture.md`](../system-administration-control-plane-architecture.md)

**Document Size**: 2,492 lines  
**Sections**: 12 major sections + 10 ADRs

---

## 📖 Architecture Document Map

### Section 1: Executive Summary (Lines 1-134)
- **1.1 Architectural Vision** - Control plane transformation principles
- **1.2 Architecture Transformation** - Before/After diagrams
- **1.3 Technology Stack Decision Matrix** - All major tech choices with ADR references

### Section 2: System Context (Lines 136-198)
- **2.1 Context Diagram** - System boundaries and actors
- **2.2 Actors and External Systems** - Users, external integrations, infrastructure

### Section 3: Architecture Overview (Lines 200-340)
- **3.1 Logical Architecture Layers** - Presentation → Gateway → Control Plane → Business → Infrastructure
- **3.2 Component Interaction Flows** - Authentication flow, JIT elevation flow

### Section 4: Component Architecture (Lines 342-717)
- **4.1 Admin Microservice** - Component diagram, database schema (SQL), 40+ API endpoints
- **4.2 Keycloak Identity Provider** - Realm config, client configs, role hierarchy, AAD B2C federation
- **4.3 Observability Stack** - OpenTelemetry C# code, deployment manifests, Grafana dashboards

### Section 5: Data Architecture (Lines 1030-1121)
- **5.1 Data Flow Diagram** - Sources → Processing → Storage
- **5.2 Data Retention Strategy** - Hot/Warm/Cold storage per data type
- **5.3 Data Sovereignty Compliance** - Zambian data center architecture

### Section 6: Integration Architecture (Lines 1123-1303)
- **6.1 Integration Patterns** - Sync (REST) vs Async (RabbitMQ)
- **6.2 API Gateway Integration** - Yarp config, JWT validation C# code

### Section 7: Security Architecture (Lines 1305-1585)
- **7.1 Zero-Trust Architecture** - 6-layer security model
- **7.2 mTLS Implementation** - cert-manager YAML, HttpClient C# code
- **7.3 NetworkPolicy Examples** - Kubernetes YAML
- **7.4 Secrets Management with Vault** - Integration architecture, pod annotations

### Section 8: Observability Architecture (Lines 1587-1749)
- **8.1 Observability Pillars** - Traces/Metrics/Logs unified by OpenTelemetry
- **8.2 Correlation ID Flow** - W3C Trace Context implementation
- **8.3 Alert Rules** - Prometheus Alertmanager YAML

### Section 9: Deployment Architecture (Lines 1751-1964)
- **9.1 Kubernetes Namespace Strategy** - Namespace segregation
- **9.2 Helm Chart Structure** - Chart organization, values-prod.yaml
- **9.3 GitOps with ArgoCD** - Application manifest, deployment workflow

### Section 10: ADRs (Lines 1966-2241)
- **ADR-001**: Identity Provider - Keycloak
- **ADR-002**: Distributed Tracing - Jaeger
- **ADR-003**: Centralized Logging - Loki
- **ADR-006**: mTLS - Manual cert-manager (vs service mesh)
- **ADR-007**: GitOps - ArgoCD
- **ADR-008**: PAM Solution - HashiCorp Boundary (proposed)
- **ADR-010**: Audit Storage - MinIO WORM

### Section 11: Migration Strategy (Lines 2243-2331)
- **11.1 Phased Migration Timeline** - 12-month week-by-week breakdown
- **11.2 Migration Risks and Mitigation** - Risk matrix with mitigations
- **11.3 Rollback Strategy** - Per-phase rollback plans with time estimates

### Section 12: Quality Attributes (Lines 2333-2471)
- **12.1 Performance** - Targets, load testing strategy
- **12.2 Availability** - 99.9% uptime, HA design, graceful degradation
- **12.3 Scalability** - Horizontal/vertical scaling, bottleneck analysis
- **12.4 Security** - Threat model, security testing strategy
- **12.5 Maintainability** - Operational complexity, documentation strategy
- **12.6 Observability** - Three pillars coverage, alerting

---

## 🔑 Key Architecture Artifacts

### Database Schema
**Admin Service Database** (Section 4.1.2, Lines 398-503)
- SQL Server schema with 9 tables
- Tamper-evident audit chain structure
- SoD rules and exceptions
- JIT elevation history
- PAM session recordings

### API Endpoints
**Admin Service REST APIs** (Section 4.1.3, Lines 506-566)
- User Management: 6 endpoints
- Role Management: 7 endpoints
- Access Governance: 7 endpoints
- Audit Trail: 7 endpoints
- Configuration: 6 endpoints
- PAM: 4 endpoints

### Keycloak Configuration
**Realm & Client Setup** (Section 4.2, Lines 568-716)
- Realm settings (token lifespans, password policy)
- Client configurations (admin-service, api-gateway)
- Protocol mappers for branch claims
- Role hierarchy diagram

### OpenTelemetry Instrumentation
**C# Implementation** (Section 4.3.1, Lines 720-817)
- Complete `AddOpenTelemetryInstrumentation()` extension method
- Adaptive sampler implementation
- OTLP exporter configuration

### Security Configurations
**mTLS & NetworkPolicies** (Section 7.2-7.3, Lines 1348-1499)
- cert-manager Certificate YAML
- HttpClient mTLS configuration (C#)
- NetworkPolicy examples (default-deny, Admin Service)

### Deployment Manifests
**Helm & ArgoCD** (Section 9, Lines 1751-1964)
- Helm values-prod.yaml structure
- ArgoCD Application manifest
- GitOps deployment workflow

---

## 🎯 Quick Reference for Development

### For Story 1.1 (Keycloak Deployment)
- **Keycloak Config**: Section 4.2.1 (Lines 572-601)
- **Realm Setup**: Section 4.2.1 (Lines 577-601)
- **Deployment**: Section 9.2 (Helm chart structure)

### For Story 1.4 (Admin Service Scaffolding)
- **Component Architecture**: Section 4.1.1 (Lines 343-395)
- **Database Schema**: Section 4.1.2 (Lines 398-503)
- **API Endpoints**: Section 4.1.3 (Lines 506-566)

### For Story 1.6 (OpenTelemetry)
- **Instrumentation Code**: Section 4.3.1 (Lines 720-817)
- **Collector Deployment**: Section 4.3.2 (Lines 819-935)
- **Integration**: Section 8 (Lines 1587-1749)

### For Story 1.12 (mTLS)
- **Architecture Decision**: ADR-006 (Lines 2088-2123)
- **Implementation**: Section 7.2 (Lines 1348-1421)
- **Certificate Config**: Lines 1354-1390 (YAML)
- **HttpClient Config**: Lines 1392-1421 (C#)

---

## 🔗 Related Documentation

### Architecture Documents
- **Full Architecture**: `../system-administration-control-plane-architecture.md` (this is the main reference)
- **ADRs**: Embedded in Section 10 of architecture document (Lines 1966-2241)

### PRD Documents
- **Full PRD**: `../system-administration-control-plane-prd.md`
- **Sharded PRD**: `../prd/` (epic overview, requirements, story index)

### Analysis Documents
- **Brownfield Analysis**: `../system-administration-brownfield-analysis.md`

---

## 💡 Using This Architecture for Implementation

### For Developers

**Before starting a story**:
1. Read relevant architecture section (see Quick Reference above)
2. Review related ADRs for technology choice rationale
3. Check database schema for data model
4. Review API endpoints for contract design

**During implementation**:
- Reference code samples (OpenTelemetry, mTLS, Vault integration)
- Follow patterns established in architecture (error handling, logging)
- Use deployment manifests as templates (Helm, ArgoCD)

### For Architects

**When reviewing PRs**:
- Validate alignment with ADRs
- Check adherence to security architecture (Section 7)
- Verify observability instrumentation (Section 8)
- Confirm deployment follows GitOps patterns (Section 9)

---

## 📝 Document Maintenance

**Last Updated**: 2025-10-11  
**Version**: 1.0  
**Status**: Approved by Architecture Team, Ready for Development

**Change Log**:
- 2025-10-11: Initial architecture sharding reference
- 2025-10-11: Architecture document completed with 10 ADRs

---

## ✅ Architecture Review Checklist

Before beginning development, confirm:

- [ ] Reviewed architecture vision and principles (Section 1.1)
- [ ] Understand component interaction flows (Section 3.2)
- [ ] Familiar with Admin Service architecture (Section 4.1)
- [ ] Read relevant ADRs for story technology choices (Section 10)
- [ ] Reviewed security requirements (Section 7)
- [ ] Understand observability integration (Section 8)
- [ ] Know deployment strategy (Section 9)
- [ ] Aware of migration risks and rollback procedures (Section 11)

**Next**: Create Story 1.1 with @sm agent
