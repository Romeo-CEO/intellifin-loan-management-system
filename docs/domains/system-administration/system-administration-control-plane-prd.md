# System Administration Control Plane - Brownfield Enhancement PRD

## Document Overview

**Project**: IntelliFin Loan Management System - System Administration Control Plane Enhancement  
**Document Type**: Product Requirements Document (Brownfield Enhancement)  
**Version**: 1.0  
**Date**: 2025-10-10  
**Author**: John (PM)  
**Status**: Ready for Architecture Phase

---

## Change Log

| Change | Date | Version | Description | Author |
|--------|------|---------|-------------|---------|
| Initial PRD | 2025-10-10 | 1.0 | Brownfield PRD for System Administration Control Plane Enhancement | John (PM) |

---

## Section 1: Intro Project Analysis and Context

### 1.1 Analysis Source

✅ **Document-project output available at**: `docs/domains/system-administration/system-administration-brownfield-analysis.md`

### 1.2 Current Project State

**IntelliFin Loan Management System** is a cloud-native, microservices-based loan management platform deployed in Zambia for data sovereignty compliance. The system currently includes:

- **Architecture**: 9+ microservices (IdentityService, ApiGateway, ClientManagement, LoanOrigination, FinancialService, Communications, Collections, etc.)
- **Technology Stack**: .NET 9 (C# 12), ASP.NET Core, SQL Server 2022, Redis, RabbitMQ, Camunda 8 (Zeebe), MinIO, HashiCorp Vault
- **Current System Administration**: 
  - ASP.NET Core Identity embedded in IdentityService
  - Basic RBAC with 6-8 V1 business roles
  - JWT authentication (15-min access, 7-day refresh)
  - Audit trail implemented in FinancialService (architectural misplacement)
  - Vault configured but underutilized
  - No observability stack (major gap)
  - No PAM or bastion access

**Primary Purpose**: Manage the complete loan lifecycle from origination through collections for microfinance lending in Zambia, with special focus on payroll-deducted loans and government employee lending via PMEC integration.

### 1.3 Available Documentation Analysis

✅ **Using existing project analysis from document-project output**

**Key documents from brownfield analysis**:
- ✅ Tech Stack Documentation (complete)
- ✅ Source Tree/Architecture (comprehensive 9 microservices)
- ✅ API Documentation (internal APIs documented)
- ✅ External Integration Documentation (PMEC, TransUnion, Tingg, SMS)
- ✅ Technical Debt Documentation (7 critical items identified)
- ✅ Infrastructure & Operations docs
- ✅ Security & Access Management docs

### 1.4 Enhancement Scope Definition

**Enhancement Type**: ☑️ **Multiple Categories**
- ☑️ New Feature Addition (Admin microservice, PAM, observability stack)
- ☑️ Integration with New Systems (Keycloak, OpenTelemetry, bastion)
- ☑️ Performance/Scalability Improvements (mTLS, observability)
- ☑️ Technology Stack Upgrade (replace ASP.NET Identity with self-hosted IdP)
- ☑️ Major Feature Modification (audit system, configuration management)

**Enhancement Description**:

Transform IntelliFin's System Administration from an operational baseline into an **enterprise-grade "control plane"** that orchestrates identity, access, policy, audit, and operational governance across all services. This includes introducing a dedicated Admin microservice, self-hosted IdP (Keycloak), enhanced RBAC with operational roles and just-in-time elevation, tamper-evident audit with WORM retention, policy-driven configuration governance, zero-trust runtime security, in-country observability stack (OpenTelemetry + Prometheus + Grafana + Loki + Jaeger), and bastion-based privileged access management.

**Impact Assessment**: ☑️ **Major Impact (architectural changes required)**
- New Admin microservice creation
- Identity Service major refactoring (extract IdP)
- All services require OpenTelemetry instrumentation
- Infrastructure-wide changes (mTLS, NetworkPolicies, observability stack)
- Audit system relocation and enhancement
- Configuration management overhaul

### 1.5 Goals and Background Context

**Goals**:
- **Establish Admin microservice as governance orchestration hub** that coordinates identity, access, policy, audit, and operational controls across all services
- Implement self-hosted IdP (Keycloak) with branch-scoped JWTs, rotating refresh tokens, and optional AAD B2C federation to enable scalable, federated identity
- Expand RBAC to real operational roles (Collections, Compliance, Treasury, GL, Auditors, Risk, Branch Mgmt) with strict SoD enforcement, step-up MFA, and JIT elevation via Camunda approval workflows
- Harden audit trail with tamper-evident cryptographic chains, MinIO WORM retention (7-year BoZ compliance), global correlation IDs, and offline audit merge capability
- Enforce policy-driven, workflow-approved configuration changes with Vault secret rotation automation, GitOps config-as-code, and signed SBOM-scanned container images
- Implement zero-trust runtime with mTLS service-to-service encryption, Kubernetes NetworkPolicies for micro-segmentation, and least-privilege service accounts
- Deploy in-country observability stack (OpenTelemetry + Prometheus/Grafana + Loki + Jaeger) for compliance with data sovereignty while enabling distributed tracing and metrics
- Mature operational resilience with tested DR/backup runbooks, automated RPO/RTO validation, and compliance dashboards for BoZ reporting
- Add bastion-based PAM with JIT infrastructure access, Camunda approval workflows, and full session recording for audit evidence
- Enable cost-performance monitoring dashboards to optimize infrastructure spend while maintaining SLA commitments

**Background Context**:

IntelliFin's System Administration layer represents a **V1 MVP foundation** that has successfully supported core loan management operations but now requires architectural maturation to meet the demands of a regulated financial services platform. The current implementation—featuring ASP.NET Core Identity for authentication, basic RBAC with 6-8 roles, and file-based audit trails—was appropriately designed for rapid market entry but has reached critical inflection points that necessitate evolution into a comprehensive control plane.

**Regulatory Pressure**: Bank of Zambia (BoZ) compliance requirements mandate 7-year audit retention (currently documented but not technically enforced via WORM), quarterly access recertifications (not implemented), and comprehensive incident response capabilities (not automated). Data sovereignty requirements further constrain the ability to use cloud-based observability services, requiring in-country deployment of the full monitoring stack.

**Operational Pain**: Production incident diagnosis takes hours due to the absence of distributed tracing and centralized log aggregation. Manual configuration changes across per-service `appsettings.json` files have caused configuration drift and deployment issues. Secrets management via Vault is configured but underutilized, leaving credentials exposed in configuration files. The architectural misplacement of audit functionality in FinancialService (rather than System Administration) violates domain boundaries and complicates compliance reporting.

**Scale Challenges**: Adding operational roles beyond the original 6 (Collections, Treasury, GL, Compliance, Auditors, Risk, Branch Management) requires RBAC expansion with strict Segregation of Duties enforcement. Multi-branch operations need branch-scoped JWT claims for efficient authorization. Offline operations (CEO desktop app) require audit log merging capabilities that currently rely on manual reconciliation, creating compliance gaps.

**Security Maturity**: Financial services operations require zero-trust networking (mTLS between services, Kubernetes NetworkPolicies) that doesn't exist. Privileged infrastructure access uses direct SSH/RDP without audit trails, just-in-time elevation, or bastion controls. Federation with Azure AD B2C for enterprise customer integration is not supported by the embedded identity provider.

**Root Architectural Issue**: System Administration was built as a **supporting service** (provide authentication and basic audit) rather than a **governance orchestration layer** that enforces policies, coordinates approvals, and provides operational visibility across the ecosystem. Infrastructure components like Vault, Camunda (beyond loan workflows), and MinIO (beyond documents) are present but underutilized because no orchestration layer coordinates their usage. The proposed Admin microservice represents the missing architectural piece that transforms scattered capabilities into a cohesive control plane.

**Strategic Investment**: This enhancement represents a foundational investment in architectural maturity rather than tactical feature additions. Continuing with incremental fixes would accumulate exponential technical debt; investing 12 months in a comprehensive control plane addresses structural limitations and positions IntelliFin for regulatory compliance, operational excellence, and sustainable scale.

---

## Section 2: Requirements

### 2.1 Functional Requirements

**FR1**: The Admin microservice shall provide a unified API for user management, role assignment, permission management, and access governance across all IntelliFin services, replacing scattered administrative functions.

**FR2**: The system shall integrate **Keycloak** as a self-hosted Identity Provider (IdP) supporting OIDC/OAuth2 standards, with migration path from existing ASP.NET Core Identity user base.

**FR3**: JWT access tokens shall include branch-scoped claims (branchId, branchName, branchRegion) automatically propagated to all downstream services for efficient authorization filtering without additional database queries.

**FR4**: Refresh tokens shall implement rotation strategy where each refresh operation issues a new refresh token and invalidates the previous one, with Redis tracking of token families for revocation chain detection.

**FR5**: The IdP shall support optional federation with Azure AD B2C for enterprise customer SSO, allowing external identity sources while maintaining local user management for internal operations (Keycloak's built-in identity brokering).

**FR6**: RBAC shall support expanded operational roles (Collections Officer, Compliance Officer, Treasury Officer, GL Accountant, Auditor, Risk Manager, Branch Manager) beyond the original 6 V1 roles, with role hierarchy and inheritance capabilities.

**FR7**: The system shall enforce Segregation of Duties (SoD) by detecting and preventing conflicting role assignments (e.g., cannot hold both "Loan Processor" and "Loan Approver" roles) with configurable SoD policies.

**FR8**: Just-in-time (JIT) privilege elevation shall be implemented via Camunda approval workflows, allowing temporary permission grants with automatic expiration and audit trail of elevation requests/approvals.

**FR9**: Step-up multi-factor authentication (MFA) shall be required for sensitive operations (high-value loan approvals, configuration changes, audit access) with Camunda workflow coordination for MFA challenges.

**FR10**: User lifecycle management (JML - Joiner/Mover/Leaver) shall be automated with onboarding workflows, role change workflows, and offboarding workflows that trigger in Camunda based on HR system events.

**FR11**: Quarterly access recertification workflows shall be implemented in Camunda, requiring managers to review and approve continued access for their team members with automated privilege revocation for non-responses.

**FR12**: The Admin microservice shall collect and centralize audit events from all services, replacing the current audit implementation in FinancialService, with unified query API and compliance reporting.

**FR13**: Audit logs shall implement tamper-evident cryptographic chaining where each audit event includes a hash of the previous event's data, creating a verifiable chain that detects tampering attempts.

**FR14**: Audit logs shall be stored in MinIO with WORM (Write-Once-Read-Many) object locking enabled, enforcing 7-year retention per Bank of Zambia requirements with immutable storage guarantees.

**FR15**: All services shall propagate W3C Trace Context correlation IDs (trace-id, span-id) through HTTP headers and message queue properties, enabling distributed request tracing across microservices.

**FR16**: The offline CEO desktop application shall batch audit events locally and merge them into the central audit system upon reconnection, with conflict resolution and duplicate detection.

**FR17**: Configuration changes to sensitive parameters (JWT expiry, lockout thresholds, audit retention periods) shall require Camunda workflow approval with manager sign-off before deployment.

**FR18**: HashiCorp Vault shall be actively integrated for runtime secret injection, replacing hardcoded secrets in `appsettings.json`, with dynamic database credentials and automatic secret rotation every 90 days.

**FR19**: Container images shall be signed with Cosign/Notary signatures and include Software Bill of Materials (SBOM) generated via Syft, with CI/CD pipeline verification before deployment.

**FR20**: GitOps configuration management shall be implemented with Git repository as source of truth for Kubernetes ConfigMaps, triggering ArgoCD synchronization for automated deployment with rollback capabilities.

**FR21**: All inter-service communication shall use mutual TLS (mTLS) with certificate-based authentication, either via service mesh (Istio/Linkerd) or manual HttpClient certificate configuration.

**FR22**: Kubernetes NetworkPolicies shall implement micro-segmentation, restricting service-to-service communication to explicitly allowed paths and blocking lateral movement by default.

**FR23**: OpenTelemetry SDK shall be integrated into all microservices, exporting traces to **Jaeger**, metrics to Prometheus, and logs to **Loki** via OTLP protocol.

**FR24**: Grafana dashboards shall provide BoZ compliance metrics (audit event counts, access recertification status, security incidents), cost-performance metrics (per-service infrastructure spend, resource utilization), and SLA monitoring (request latency p50/p95/p99, error rates).

**FR25**: Bastion host infrastructure shall provide privileged access management (PAM) with JIT access requests triggering Camunda approval workflows, automatic credential generation via Vault, and full SSH session recording stored in MinIO.

**FR26**: Infrastructure access (SSH, RDP, kubectl) shall be time-bound with automatic credential expiration after approval period, requiring re-approval for continued access with audit trail of all access grants.

**FR27**: Disaster recovery runbooks shall be automated and tested quarterly, with validation of RPO (1 hour) and RTO (4 hours) targets through actual failover exercises to secondary Zambian data center.

### 2.2 Non-Functional Requirements

**NFR1**: The Admin microservice shall maintain 99.9% availability measured monthly, with graceful degradation allowing authentication/authorization to continue during downstream service outages.

**NFR2**: JWT token validation and authorization checks shall complete within 50ms at p95, ensuring minimal overhead added to existing request processing times.

**NFR3**: Branch-scoped JWT claims shall reduce database queries for authorization filtering by 80%, improving API response times for multi-branch queries from average 200ms to 120ms.

**NFR4**: Audit event ingestion shall support 10,000 events per second peak throughput, with buffering and batching to handle burst traffic from all microservices without data loss.

**NFR5**: Tamper-evident audit chain verification shall complete within 5 seconds for 1 million audit records, enabling compliance audits without performance impact.

**NFR6**: OpenTelemetry trace sampling shall use adaptive sampling (100% for errors, 10% for normal requests) to limit trace storage growth to <1TB per year while capturing all production issues.

**NFR7**: Prometheus metrics shall be retained for 30 days with 15-second scrape intervals, consuming no more than 500GB storage for the full IntelliFin deployment.

**NFR8**: Centralized logs in **Loki** shall be retained for 90 days with full-text search capability via LogQL, consuming no more than 2TB storage for the full IntelliFin deployment.

**NFR9**: mTLS handshake overhead shall add no more than 20ms latency to inter-service requests measured at p95, validated through load testing at 2x expected production traffic.

**NFR10**: **Keycloak IdP** shall support 1,000 concurrent authentication requests with response times <500ms at p95, ensuring no user-facing authentication delays during peak usage (8am-10am branch opening hours).

**NFR11**: Camunda approval workflows for JIT elevation shall complete manager approval/rejection within 15 seconds of decision, with real-time notification via SignalR to requesting users.

**NFR12**: Configuration changes via GitOps shall deploy to Kubernetes within 5 minutes of ArgoCD sync detection, with automated rollback triggered if health checks fail within 2 minutes post-deployment.

**NFR13**: Vault secret rotation shall complete with zero downtime through rolling updates, validating new credentials before invalidating old ones with 5-minute overlap for in-flight requests.

**NFR14**: Container image vulnerability scanning shall block deployment of images with critical CVEs, with exception workflow requiring CISO approval and documented remediation plan.

**NFR15**: Bastion session recordings shall be uploaded to MinIO within 30 seconds of session termination, with automatic encryption and compliance retention enforcement.

**NFR16**: The enhanced system shall maintain existing memory footprint per service (±20%), ensuring no infrastructure scaling required beyond planned observability backend additions (Prometheus, Grafana, Loki, Jaeger).

**NFR17**: DR failover exercises shall achieve RPO of <1 hour and RTO of <4 hours, validated through quarterly tests with full audit log integrity verification post-recovery.

**NFR18**: All personally identifiable information (PII) in logs and traces shall be automatically redacted via OpenTelemetry processors, ensuring compliance with Zambian Data Protection Act.

**NFR19**: The system shall comply with Bank of Zambia prudential guidelines for information security, with annual penetration testing and quarterly vulnerability assessments documented for regulatory review.

**NFR20**: Documentation for the control plane architecture shall be maintained in the `docs/domains/system-administration/` folder with architectural decision records (ADRs) for all major technology choices (IdP selection, observability stack, mTLS approach, PAM solution).

### 2.3 Compatibility Requirements

**CR1: Existing API Compatibility** - All existing REST API endpoints across IntelliFin microservices shall remain functional during and after the enhancement, with JWT token format changes handled via dual-token support during migration (accept both ASP.NET Core Identity tokens and new IdP tokens for 30-day transition period).

**CR2: Database Schema Compatibility** - Existing user, role, and permission tables in SQL Server shall be preserved with data migration to Keycloak's schema, maintaining referential integrity for foreign keys in business tables (e.g., `CreatedBy`, `UpdatedBy` fields in loan applications).

**CR3: UI/UX Consistency** - Admin portal and user-facing authentication screens shall maintain existing IntelliFin visual identity (colors, logos, typography) and interaction patterns, ensuring no user re-training required for login flows.

**CR4: Integration Compatibility** - External integrations (PMEC, TransUnion, Tingg, Africa's Talking SMS) shall continue functioning without modification, as authentication/authorization changes are internal to IntelliFin with existing API contracts maintained.

**CR5: Offline Desktop App Compatibility** - CEO offline desktop application (MAUI) shall continue supporting local SQLite operations with backward-compatible sync protocol, adding audit merge capability without breaking existing offline loan origination workflows.

**CR6: RabbitMQ Message Compatibility** - Existing RabbitMQ message contracts shall remain unchanged, with correlation ID addition handled via message headers/properties that older consumers can ignore (additive change only).

**CR7: Audit Log Backward Compatibility** - Existing audit events in FinancialService database shall be accessible via unified Admin microservice API, with query interface supporting both legacy and new audit event schemas during transition.

**CR8: Configuration File Compatibility** - Services shall support dual configuration sources during migration (existing `appsettings.json` and Vault dynamic secrets), allowing gradual secret migration without big-bang cutover.

---

## Section 3: User Interface Enhancement Goals

### 3.1 Integration with Existing UI

The System Administration Control Plane enhancement will introduce new administrative interfaces while maintaining consistency with IntelliFin's existing Next.js 15 + TypeScript frontend design system. New UI components will:

**Design System Integration**:
- Utilize existing Tailwind CSS custom component library (per tech stack documentation)
- Follow established color palette, typography, and spacing standards from `lms-ux-style-guide.md`
- Leverage existing React Query + Zustand state management patterns for API calls and client state
- Maintain existing SignalR integration for real-time notifications (e.g., approval workflow updates)

**Authentication Flow Integration**:
- Keycloak-hosted login screens will be branded with IntelliFin visual identity (logo, colors)
- Existing session timeout behavior (30 minutes inactivity) preserved
- Step-up MFA screens will follow existing modal/dialog patterns for consistency
- SSO redirect flows (AAD B2C federation) will use existing loading states and error handling

**Navigation Integration**:
- Admin portal accessible via existing navigation sidebar with role-based visibility
- Existing breadcrumb and page title patterns maintained for new admin screens
- Current user profile dropdown will expand to show JIT elevation status when active

### 3.2 Modified/New Screens and Views

**New Admin Portal Screens** (accessible to System Administrator, Compliance, Auditor roles):

1. **User Management Dashboard** (`/admin/users`)
   - User listing with search/filter (branch, role, status)
   - User detail modal with role assignments, permissions, audit history
   - JML workflow initiation (onboarding, role change, offboarding)

2. **Role & Permission Management** (`/admin/roles`)
   - Role hierarchy visualization
   - Permission catalog with role-permission mapping matrix
   - SoD policy configuration with conflict detection preview

3. **Access Governance** (`/admin/access-governance`)
   - Quarterly recertification campaign dashboard
   - Manager access review interface with approve/revoke actions
   - JIT elevation request/approval workflow UI
   - Active elevated sessions monitoring

4. **Audit Trail Explorer** (`/admin/audit`)
   - Audit event search with advanced filters (date range, actor, action, entity)
   - Audit event detail view with correlation ID trace links
   - Integrity verification dashboard showing tamper-evident chain status
   - Compliance report generation (BoZ formats: PDF, Excel)

5. **Configuration Management** (`/admin/configuration`)
   - Policy-driven configuration editor with approval workflow status
   - Vault secret inventory (metadata only, no secret values displayed)
   - GitOps config sync status with rollback capabilities
   - Pending configuration change approvals (for managers)

6. **Observability Dashboards** (`/admin/observability`)
   - Embedded Grafana dashboards (iframe or Grafana API integration)
   - Quick links to Jaeger trace search, Loki log exploration
   - Compliance metrics dashboard (BoZ reporting KPIs)
   - Cost-performance dashboard (per-service infrastructure spend)

7. **PAM Access Requests** (`/admin/pam`)
   - Infrastructure access request form (server, duration, justification)
   - Active PAM sessions monitoring with session recording links
   - PAM access history with audit trail

**Modified Existing Screens**:

1. **Login Screen** (now redirects to Keycloak)
   - Branding maintained, but hosted by Keycloak realm
   - "Login with Azure AD" button added for federated users

2. **User Profile Screen** (`/profile`)
   - Add "My Access" section showing current roles, permissions, JIT elevations
   - Add "Request JIT Elevation" button (if eligible)

3. **Manager Dashboard** (existing)
   - Add "Pending Approvals" widget showing JIT elevation requests, access recertification tasks, config change approvals

### 3.3 UI Consistency Requirements

**Visual Consistency**:
- All new screens shall use the existing IntelliFin color palette (primary, secondary, accent colors from style guide)
- Typography shall match existing heading hierarchy (H1-H6) and body text styles
- Iconography shall use the existing icon library (Heroicons or equivalent)
- Form inputs, buttons, and controls shall use existing component library with no custom variants

**Interaction Consistency**:
- Table pagination, sorting, and filtering shall match existing patterns (e.g., loan applications table)
- Modal dialogs shall use existing modal component with consistent close/cancel/submit patterns
- Loading states shall use existing skeleton loaders and spinner components
- Error handling shall use existing toast notification system (not custom alerts)

**Responsive Design**:
- Admin portal screens shall be tablet-optimized (existing Next.js responsive breakpoints)
- Mobile support for read-only audit viewing and approval workflows (no mobile admin editing)

**Accessibility**:
- WCAG 2.1 AA compliance maintained (existing standard)
- Keyboard navigation support for all admin workflows
- Screen reader compatibility for audit trail exploration

---

## Section 4: Technical Constraints and Integration Requirements

### 4.1 Existing Technology Stack

**Languages**: 
- Backend: C# 12.0 (.NET 9)
- Frontend: TypeScript 5.3+

**Frameworks**: 
- Backend: ASP.NET Core 9.0 (Minimal APIs)
- Frontend: Next.js 15+ with React
- UI: Tailwind CSS 3.4+ with custom component library

**Database**: 
- Primary: SQL Server 2022 (Always On for HA)
- Offline: SQLite (CEO desktop app)
- Keycloak: PostgreSQL (Keycloak requirement - separate database)

**Infrastructure**: 
- Orchestration: Kubernetes 1.28+ with Helm 3.13+
- IaC: Terraform for infrastructure provisioning
- Container Registry: (to be determined - likely Azure Container Registry or self-hosted Harbor)
- CI/CD: GitHub Actions

**External Dependencies**: 
- **Keycloak** (NEW - to be deployed)
- Camunda 8 Cloud SaaS (existing - may evaluate self-hosted for data sovereignty)
- HashiCorp Vault 1.15+ (existing but underutilized)
- MinIO (existing for documents, expanding to WORM audit storage)
- OpenTelemetry Collector (NEW)
- Prometheus + Grafana (NEW)
- **Loki** (NEW)
- **Jaeger** (NEW)

### 4.2 Integration Approach

**Database Integration Strategy**:
- **Keycloak Database**: Separate PostgreSQL database for Keycloak (Keycloak's requirement), not SQL Server
- **User Migration**: One-time ETL from ASP.NET Core Identity tables to Keycloak's user storage
- **Audit Database**: Continue using SQL Server for audit events, with MinIO as immutable WORM storage for compliance
- **Admin Service Database**: New SQL Server database `IntelliFin_AdminService` for admin-specific tables (policy configs, JIT elevation history)
- **Foreign Key Compatibility**: Preserve existing `CreatedBy`/`UpdatedBy` userId references - map Keycloak user IDs to maintain referential integrity

**API Integration Strategy**:
- **Admin Service**: New REST API (`IntelliFin.AdminService`) exposing `/api/admin/*` endpoints
- **Keycloak Integration**: Admin Service acts as Keycloak admin client (Keycloak Admin REST API) for user/role management
- **Existing Services**: Modified to call Admin Service for audit logging (replace direct `IAuditService` calls)
- **API Gateway**: Yarp-based gateway updated to route `/api/admin/*` to Admin Service, validate Keycloak JWT tokens
- **Backward Compatibility**: Dual-token support during migration (accept both old ASP.NET Identity JWTs and new Keycloak JWTs for 30 days)

**Frontend Integration Strategy**:
- **Next.js App Router**: New admin portal pages under `/app/admin/*` route group
- **Authentication**: Integrate `next-auth` library with Keycloak provider (OIDC)
- **State Management**: Use React Query for Admin Service API calls, Zustand for local UI state (consistent with existing patterns)
- **Real-time Updates**: Existing SignalR hub extended with admin-specific events (approval notifications, audit alerts)

**Testing Integration Strategy**:
- **Unit Tests**: xUnit for .NET services (existing pattern), Jest for React components
- **Integration Tests**: TestContainers for spinning up Keycloak, Vault, MinIO in integration test environments
- **E2E Tests**: Playwright (existing) extended with admin portal flows
- **Performance Tests**: k6 or NBomber for load testing new Admin Service endpoints and Keycloak token validation

### 4.3 Code Organization and Standards

**File Structure Approach**:
```
apps/
  IntelliFin.AdminService/          # NEW microservice
    Controllers/
      UserManagementController.cs
      RoleManagementController.cs
      AccessGovernanceController.cs
      AuditTrailController.cs       # MOVED from FinancialService
      ConfigurationController.cs
      PamController.cs
    Services/
      KeycloakAdminService.cs
      AuditChainService.cs
      PolicyEnforcementService.cs
    Models/
      AdminModels.cs
    Program.cs
    appsettings.json
  IntelliFin.IdentityService/        # REFACTORED (lighten, delegate to Keycloak)
    - Keep as Keycloak client for token validation
    - Remove ASP.NET Core Identity (migrate to Keycloak)
  IntelliFin.ApiGateway/             # MODIFIED (add Keycloak JWT validation)
  # ... other services (add OpenTelemetry instrumentation)

libs/
  IntelliFin.Shared.Observability/   # NEW shared library
    OpenTelemetryExtensions.cs
    CorrelationIdMiddleware.cs
  IntelliFin.Shared.AdminClient/     # NEW client library
    AdminServiceClient.cs           # For services to call Admin Service

infra/
  keycloak/                          # NEW
    realm-export.json
    themes/intellifin/               # Custom Keycloak theme
  observability/                     # NEW
    prometheus/
    grafana/
      dashboards/
    loki/
    jaeger/
  helm/
    admin-service/                   # NEW Helm chart
    keycloak/                        # NEW Helm chart
```

**Naming Conventions**:
- Follow existing C# conventions (PascalCase for types, camelCase for locals)
- Admin Service controllers: `{Domain}Controller` pattern (existing)
- OpenTelemetry spans: `{ServiceName}.{ClassName}.{MethodName}` (NEW standard)
- Grafana dashboards: `intellifin-{domain}-{metric-type}.json` (NEW standard)

**Coding Standards**:
- Adhere to existing `.editorconfig` and code analysis rules
- Add OpenTelemetry instrumentation attributes to all public service methods
- Add correlation ID propagation to all HTTP and message queue calls
- Follow existing error handling patterns (ProblemDetails for APIs)

**Documentation Standards**:
- Architectural Decision Records (ADRs) in `docs/domains/system-administration/adrs/` for major decisions
- API documentation via OpenAPI/Swagger (existing pattern)
- Keycloak realm configuration documented in `docs/domains/system-administration/keycloak-setup.md`
- Observability runbooks in `docs/domains/system-administration/observability-runbooks.md`

### 4.4 Deployment and Operations

**Build Process Integration**:
- **Existing**: `dotnet build IntelliFin.sln` builds all services
- **NEW**: Add `apps/IntelliFin.AdminService` to solution file
- **NEW**: Add `docker build` for Admin Service image
- **NEW**: Add Helm chart linting (`helm lint`) to CI pipeline
- **NEW**: Add Cosign image signing step after Docker build
- **NEW**: Add Syft SBOM generation and Trivy vulnerability scanning

**Deployment Strategy**:
- **GitOps**: ArgoCD monitors Git repository for Helm chart changes
- **Phased Rollout**: Deploy observability stack first (Phase 1), then Keycloak (Phase 1), then Admin Service (Phase 3)
- **Canary Deployments**: Admin Service uses Argo Rollouts for progressive delivery (10% → 50% → 100% over 30 minutes)
- **Database Migrations**: Use Entity Framework migrations for Admin Service database, manual Keycloak user migration scripts
- **Rollback Strategy**: ArgoCD sync-revert for Kubernetes resources, database migration rollback scripts for EF

**Monitoring and Logging**:
- **Metrics**: Prometheus scrapes `/metrics` endpoint from all services (OpenTelemetry exporter)
- **Traces**: Jaeger receives traces via OTLP gRPC from OpenTelemetry Collector
- **Logs**: Loki ingests logs via Promtail (file scraping) or OTLP from OpenTelemetry Collector
- **Dashboards**: Grafana pre-configured with IntelliFin dashboards (BoZ compliance, cost-performance, SLA)
- **Alerts**: Prometheus Alertmanager configured with critical alert rules (e.g., audit chain break, Keycloak down, mTLS failure)

**Configuration Management**:
- **Secrets**: Vault secrets injected via Kubernetes Vault Agent sidecar (CSI driver)
- **ConfigMaps**: Managed via GitOps (ArgoCD syncs from Git)
- **Environment-specific**: Helm values files per environment (`values-dev.yaml`, `values-prod.yaml`)
- **Policy Enforcement**: Policy-as-Code via Open Policy Agent (OPA) gatekeeper for Kubernetes admission control

### 4.5 Risk Assessment and Mitigation

**Technical Risks**:

| Risk | Impact | Likelihood | Mitigation Strategy |
|------|--------|------------|---------------------|
| **Keycloak user migration data loss** | High | Medium | Run migration in staging first, validate all user/role data migrated, maintain ASP.NET Identity database read-only for 90 days as backup |
| **OpenTelemetry performance overhead** | Medium | Medium | Adaptive sampling (10% normal, 100% errors), benchmark trace processing overhead, use async exporters to avoid blocking |
| **mTLS certificate management complexity** | Medium | High | Evaluate service mesh (Istio) for automatic cert rotation vs. manual cert-manager, pilot with 2 services before full rollout |
| **MinIO WORM lock prevents audit corrections** | High | Low | Design audit correction workflow (append correction event, don't delete), test WORM compliance mode extensively in staging |
| **Grafana dashboard performance on large datasets** | Low | Medium | Pre-aggregate metrics via Prometheus recording rules, limit dashboard time ranges to 7 days default, add query caching |

**Integration Risks**:

| Risk | Impact | Likelihood | Mitigation Strategy |
|------|--------|------------|---------------------|
| **Keycloak token format breaks existing JWT consumers** | High | Medium | Dual-token support (accept both old and new tokens for 30 days), extensive integration testing, gradual service migration |
| **Correlation ID propagation gaps** | Medium | High | Automated tests to verify correlation ID in all outbound HTTP/RabbitMQ calls, OpenTelemetry auto-instrumentation for common libraries |
| **Admin Service becomes single point of failure** | High | Low | Deploy Admin Service with 3 replicas, implement circuit breakers in consumers, cache critical data (roles/permissions) in Redis |
| **GitOps config sync delays cause outages** | Medium | Medium | ArgoCD sync-waves for ordered deployments, automated smoke tests post-sync, rollback automation if health checks fail |

**Deployment Risks**:

| Risk | Impact | Likelihood | Mitigation Strategy |
|------|--------|------------|---------------------|
| **Observability stack consumes excessive resources** | Medium | Medium | Right-size Prometheus/Loki/Jaeger storage, implement retention policies (30d metrics, 90d logs, 7d traces), monitor infrastructure cost |
| **Phased rollout delays full control plane functionality** | Low | High | Acceptable - 12-month phased approach reduces risk, interim workarounds documented per phase |
| **Keycloak upgrade path unclear** | Medium | Low | Test Keycloak upgrades in staging quarterly, subscribe to Keycloak security mailing list, plan for 6-month upgrade cadence |

**Known Constraints from Brownfield Analysis**:

- **Camunda Cloud SaaS dependency**: Currently using Camunda 8 Cloud SaaS - evaluate self-hosted Camunda for data sovereignty (may impact cost)
- **Non-standard ports in dev**: Docker Compose uses non-standard ports (SQL Server 31433) - maintain compatibility in local dev setup
- **Branch scoping manual filtering**: Current `BranchId` in user model requires manual filtering - new branch-scoped JWT claims solve this but need careful testing
- **Offline CEO app sync**: Audit merge from offline desktop app is complex - Phase 3 delivery, extensive conflict resolution testing required

---

## Section 5: Epic and Story Structure

### 5.1 Epic Approach

**Epic Structure Decision**: **Single Comprehensive Epic** with phased story delivery

**Rationale**:

The System Administration Control Plane enhancement, while substantial (27 FRs + 20 NFRs), represents a **tightly integrated architectural transformation** where components are interdependent:

- Admin microservice coordinates identity (Keycloak), audit (tamper-evident chain), config (Vault/GitOps), and workflows (Camunda)
- Observability stack (OpenTelemetry → Jaeger/Loki/Prometheus) requires instrumentation across all services
- Zero-trust runtime (mTLS, NetworkPolicies) is infrastructure-wide
- PAM (bastion + JIT access) builds on Keycloak identity and Camunda workflows

Splitting into multiple epics would create artificial boundaries and complicate dependency management. Instead, we use **one epic with 6 phases** aligned to the migration strategy from brownfield analysis (Months 1-2: Foundation, Months 3-4: Enhanced Security, etc.).

**Benefits of Single Epic**:
- Clear end-to-end vision of control plane transformation
- Dependencies explicit within epic story sequence
- Easier to track progress toward complete governance capability
- Aligns with 12-month budget/resource planning window

**Epic Title**: "System Administration Control Plane: Enterprise Governance & Observability"

---

## Section 6: Epic 1 - System Administration Control Plane

### Epic Goal

Transform IntelliFin's System Administration from a tactical support layer into a strategic control plane that orchestrates identity, access, policy, audit, and operational governance across all microservices, enabling Bank of Zambia compliance, zero-trust security, comprehensive observability, and scalable operations for regulated financial services in Zambia.

### Integration Requirements

**Critical Integration Points**:
- **Keycloak ↔ All Services**: JWT token validation, OIDC authentication flows
- **Admin Service ↔ Keycloak**: User/role management via Keycloak Admin API
- **Admin Service ↔ All Services**: Centralized audit event collection via REST/RabbitMQ
- **OpenTelemetry ↔ All Services**: Trace/metric/log export via OTLP protocol
- **Vault ↔ All Services**: Dynamic secret injection via Kubernetes Vault Agent
- **Camunda ↔ Admin Service**: JIT elevation, config approval, access recertification workflows
- **MinIO WORM ↔ Admin Service**: Immutable audit log storage with 7-year retention
- **GitOps (ArgoCD) ↔ Kubernetes**: Configuration deployment with automated sync

**Integration Validation Requirements**:
- All existing API endpoints remain functional (CR1)
- Database foreign keys preserved (CR2)
- External integrations (PMEC, TransUnion, Tingg) unaffected (CR4)
- Offline desktop app sync compatible (CR5)
- RabbitMQ message contracts backward compatible (CR6)

---

### Story Sequence

**CRITICAL STORY SEQUENCING FOR BROWNFIELD**: Stories are ordered to minimize risk to the existing system, ensure incremental value delivery, and maintain system integrity throughout the 12-month enhancement.

---

## Phase 1: Foundation (Stories 1.1-1.9) - Months 1-2

### Story 1.1: Keycloak Deployment and Realm Configuration

**As a** System Administrator,  
**I want** Keycloak deployed to Kubernetes with IntelliFin realm configured,  
**so that** we have a self-hosted Identity Provider ready for user migration and OIDC integration.

**Acceptance Criteria**:
1. Keycloak 24+ deployed to Kubernetes with PostgreSQL backend via Helm chart
2. IntelliFin realm created with custom theme matching existing branding (logo, colors)
3. OIDC client configurations created for Admin Service and API Gateway
4. Keycloak admin console accessible to System Administrators with MFA enabled
5. Health checks configured with Prometheus metrics endpoint exposed
6. Backup/restore procedures tested for Keycloak PostgreSQL database

**Integration Verification**:
- **IV1**: Existing IdentityService remains operational, no user-facing changes
- **IV2**: Keycloak isolated in separate namespace, no interference with existing services
- **IV3**: Keycloak deployment documented in `docs/domains/system-administration/keycloak-setup.md`

**Dependencies**: None (greenfield Keycloak deployment)

**Estimated Effort**: 3-5 days

---

### Story 1.2: ASP.NET Core Identity User Migration to Keycloak

**As a** System Administrator,  
**I want** existing users, roles, and permissions migrated from ASP.NET Core Identity to Keycloak,  
**so that** we preserve user access and avoid forced re-login/re-registration.

**Acceptance Criteria**:
1. ETL script extracts users from `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles` tables
2. Keycloak user import via Admin API creates users with preserved IDs (mapped to Keycloak UUIDs)
3. Role mappings preserved (Loan Officer → Loan Officer realm role)
4. User attributes migrated (FirstName, LastName, BranchId, TenantId)
5. Password hashes NOT migrated (users must reset password on first Keycloak login - security best practice)
6. Migration validation report generated (user count, role count, any errors)
7. Rollback script prepared to revert to ASP.NET Core Identity if issues detected

**Integration Verification**:
- **IV1**: ASP.NET Core Identity database remains intact (read-only) for 90-day safety period
- **IV2**: User ID mapping table created in Admin Service database for foreign key compatibility
- **IV3**: Existing `CreatedBy`/`UpdatedBy` references in business tables remain valid via ID mapping

**Dependencies**: Story 1.1 (Keycloak deployed)

**Estimated Effort**: 5-7 days

---

### Story 1.3: API Gateway Keycloak JWT Validation (Dual-Token Support)

**As a** developer,  
**I want** API Gateway to validate both old ASP.NET Core Identity JWTs and new Keycloak JWTs,  
**so that** we support gradual service migration without breaking existing clients.

**Acceptance Criteria**:
1. API Gateway JWT middleware extended to accept two token issuers (IntelliFin.Identity and Keycloak)
2. Keycloak public key retrieved via JWKS endpoint for signature validation
3. Branch-scoped claims (branchId, branchName) extracted from Keycloak tokens and propagated downstream
4. Existing authentication endpoints (`/api/auth/*`) remain functional during transition
5. Token type logged in audit trail for migration tracking
6. 30-day dual-token support window configured (after which old tokens rejected)

**Integration Verification**:
- **IV1**: All existing API endpoints validate successfully with old JWTs (regression testing)
- **IV2**: New Keycloak JWTs accepted by API Gateway and propagated to downstream services
- **IV3**: Performance testing confirms <10ms additional latency for dual-token validation

**Dependencies**: Story 1.2 (user migration complete), existing API Gateway

**Estimated Effort**: 3-5 days

---

### Story 1.4: Admin Microservice Scaffolding and Deployment

**As a** System Administrator,  
**I want** Admin microservice deployed with basic health checks and API structure,  
**so that** we have the control plane orchestration hub ready for feature implementation.

**Acceptance Criteria**:
1. `IntelliFin.AdminService` ASP.NET Core 9 project created with Minimal APIs
2. Database context created for Admin Service database (`IntelliFin_AdminService`)
3. Entity Framework migrations initialized
4. Docker image built and pushed to container registry with Cosign signature
5. Helm chart created for Admin Service deployment to Kubernetes
6. Health check endpoint (`/health`) operational and monitored by Prometheus
7. OpenAPI/Swagger documentation generated for Admin Service API

**Integration Verification**:
- **IV1**: Admin Service deployed to Kubernetes in `admin` namespace with 3 replicas
- **IV2**: API Gateway routes `/api/admin/*` to Admin Service successfully
- **IV3**: Admin Service connects to SQL Server and Keycloak (Admin API client configured)

**Dependencies**: Story 1.1 (Keycloak), existing Kubernetes infrastructure

**Estimated Effort**: 3-4 days

---

### Story 1.5: Keycloak Admin Client Integration (User Management API)

**As a** System Administrator,  
**I want** Admin Service to expose user management APIs backed by Keycloak Admin API,  
**so that** I can manage users, roles, and permissions through a unified IntelliFin interface.

**Acceptance Criteria**:
1. Admin Service implements `IKeycloakAdminService` wrapping Keycloak Admin REST API
2. User management endpoints created: GET/POST/PUT/DELETE `/api/admin/users`
3. Role management endpoints created: GET/POST/PUT/DELETE `/api/admin/roles`
4. Permission assignment endpoint: POST `/api/admin/users/{id}/roles`
5. Keycloak API calls include retry logic with exponential backoff (Polly library)
6. Error responses from Keycloak translated to IntelliFin ProblemDetails format
7. Audit events logged for all user/role management actions

**Integration Verification**:
- **IV1**: User changes via Admin Service API reflected in Keycloak admin console
- **IV2**: Existing IdentityService user endpoints deprecated (redirect to Admin Service or return 410 Gone)
- **IV3**: Performance test confirms <500ms p95 for user CRUD operations

**Dependencies**: Story 1.4 (Admin Service deployed), Story 1.1 (Keycloak)

**Estimated Effort**: 5-7 days

---

### Story 1.6: OpenTelemetry Shared Library and Instrumentation Bootstrap

**As a** developer,  
**I want** OpenTelemetry SDK integrated into all microservices with basic instrumentation,  
**so that** we have distributed tracing, metrics, and logging foundation for observability.

**Acceptance Criteria**:
1. `IntelliFin.Shared.Observability` library created with OpenTelemetry SDK dependencies
2. Extension method `AddOpenTelemetryInstrumentation()` configures OTLP exporters (Jaeger, Prometheus, Loki)
3. Automatic instrumentation for ASP.NET Core HTTP requests, HttpClient calls, Entity Framework queries
4. W3C Trace Context propagation enabled for HTTP headers and RabbitMQ message properties
5. All services updated to call `AddOpenTelemetryInstrumentation()` in `Program.cs`
6. Service name, version, and environment tagged in all telemetry (resource attributes)
7. Adaptive trace sampling configured (100% errors, 10% normal requests)

**Integration Verification**:
- **IV1**: Existing service functionality unaffected (no breaking changes from instrumentation)
- **IV2**: Correlation IDs automatically generated and propagated across service calls
- **IV3**: Performance overhead <5% measured via load testing (within NFR16 tolerance)

**Dependencies**: None (new shared library)

**Estimated Effort**: 5-7 days

---

### Story 1.7: Jaeger Deployment and Trace Collection

**As a** DevOps engineer,  
**I want** Jaeger deployed to Kubernetes receiving traces from OpenTelemetry Collector,  
**so that** I can visualize distributed request traces across IntelliFin microservices.

**Acceptance Criteria**:
1. Jaeger all-in-one deployed to Kubernetes via Helm chart (or Jaeger Operator)
2. OpenTelemetry Collector deployed as DaemonSet receiving OTLP traces from services
3. OTLP Collector exports traces to Jaeger backend (OTLP/gRPC)
4. Jaeger UI accessible at `https://jaeger.intellifin.local` (ingress configured)
5. Trace retention configured for 7 days with automated cleanup
6. Sample traces validated showing request flow: API Gateway → Loan Origination → Credit Bureau
7. Jaeger Prometheus metrics exposed for monitoring trace processing

**Integration Verification**:
- **IV1**: Trace collection does not impact service latency (async exporters validated)
- **IV2**: Trace data searchable by correlation ID, service name, operation name
- **IV3**: Error traces (5xx responses) automatically highlighted in Jaeger UI

**Dependencies**: Story 1.6 (OpenTelemetry instrumentation)

**Estimated Effort**: 3-5 days

---

### Story 1.8: Prometheus and Grafana Deployment

**As a** DevOps engineer,  
**I want** Prometheus and Grafana deployed to Kubernetes collecting metrics from all services,  
**so that** I can monitor system health, performance, and compliance KPIs.

**Acceptance Criteria**:
1. Prometheus deployed via Helm chart (Prometheus Operator or kube-prometheus-stack)
2. ServiceMonitor resources created for automatic scraping of service `/metrics` endpoints
3. Prometheus retention configured for 30 days with 15-second scrape intervals
4. Grafana deployed with Prometheus data source pre-configured
5. Initial dashboards imported: Kubernetes cluster metrics, service health, API latency p50/p95/p99
6. Alertmanager configured with basic alert rules (Keycloak down, high error rate, audit chain break)
7. Grafana accessible at `https://grafana.intellifin.local` with SSO via Keycloak

**Integration Verification**:
- **IV1**: Prometheus successfully scrapes metrics from all instrumented services
- **IV2**: Grafana dashboards display real-time metrics with <15-second refresh
- **IV3**: Alert test confirms notification delivery to Slack/email within 1 minute

**Dependencies**: Story 1.6 (OpenTelemetry metrics), Kubernetes

**Estimated Effort**: 3-5 days

---

### Story 1.9: Loki Deployment and Centralized Logging

**As a** DevOps engineer,  
**I want** Loki deployed to Kubernetes collecting logs from all services via Promtail,  
**so that** I can perform centralized log search and analysis without SSH-ing to pods.

**Acceptance Criteria**:
1. Loki deployed via Helm chart with S3-compatible storage backend (MinIO for in-country compliance)
2. Promtail deployed as DaemonSet scraping pod logs (stdout/stderr)
3. OpenTelemetry Collector optionally exports structured logs to Loki via OTLP
4. Log retention configured for 90 days with automated deletion
5. Grafana Loki data source configured with LogQL query examples documented
6. Sample LogQL queries validated: error logs, audit events, specific correlation IDs
7. PII redaction configured via Promtail pipeline stages (mask NRC numbers, phone numbers)

**Integration Verification**:
- **IV1**: Existing file-based logging remains functional during transition (dual logging)
- **IV2**: Log search performance validated (<3 seconds for 1-hour time range queries)
- **IV3**: Log volume within NFR8 estimate (2TB storage for 90-day retention)

**Dependencies**: Story 1.6 (OpenTelemetry logs), MinIO

**Estimated Effort**: 3-5 days

---

**Phase 1 Summary**: 9 stories, ~6-8 weeks
- Foundation established with Keycloak, Admin Service, OpenTelemetry, Jaeger, Prometheus, Grafana, Loki

---

## Phase 2: Enhanced Security (Stories 1.10-1.13) - Months 3-4

### Story 1.10: Rotating Refresh Token Implementation

**As a** security engineer,  
**I want** Keycloak refresh tokens to rotate on every refresh operation,  
**so that** we reduce security risk from long-lived refresh token theft.

**Acceptance Criteria**:
1. Keycloak realm configured with `Rotate Refresh Tokens` policy enabled
2. Redis tracking of refresh token families for revocation chain detection
3. Token revocation endpoint (`/api/auth/revoke`) extended to revoke entire token family
4. Frontend updated to handle refresh token rotation (store new refresh token from response)
5. Token theft detection: If revoked token in family used, entire family invalidated and user logged out
6. Audit events logged for refresh operations and token family revocations
7. Documentation updated with refresh token rotation flow diagrams

**Integration Verification**:
- **IV1**: Existing refresh token logic updated without breaking active sessions
- **IV2**: Performance test confirms refresh operation completes <500ms
- **IV3**: Token theft simulation validates revocation chain works (security test)

**Dependencies**: Story 1.3 (API Gateway Keycloak integration)

**Estimated Effort**: 3-5 days

---

### Story 1.11: Branch-Scoped JWT Claims Implementation

**As a** developer,  
**I want** JWT tokens to include branch-scoped claims (branchId, branchName, branchRegion),  
**so that** API services can filter data by branch without additional database queries.

**Acceptance Criteria**:
1. Keycloak user attributes `branchId`, `branchName`, `branchRegion` mapped to user profiles
2. Keycloak client protocol mapper configured to include branch claims in JWT access tokens
3. API Gateway middleware extracts branch claims and adds to request context (`HttpContext.Items`)
4. Service authorization policies updated to use branch claims for data filtering
5. Performance testing validates 80% reduction in authorization queries (NFR3 target)
6. Branch claim audit: Log when user accesses data outside their branch (potential SoD violation)
7. Documentation updated with branch claim usage patterns for developers

**Integration Verification**:
- **IV1**: Existing branch-based queries refactored to use JWT claims (backward compatible)
- **IV2**: Multi-branch users (managers) validated to have correct branch claim hierarchy
- **IV3**: Performance improvement measured: Loan application list query latency reduced from 200ms to <120ms

**Dependencies**: Story 1.3 (API Gateway Keycloak integration), user data migration

**Estimated Effort**: 5-7 days

---

### Story 1.12: mTLS Service-to-Service Communication

**As a** security engineer,  
**I want** all inter-service HTTP communication secured with mutual TLS,  
**so that** we prevent man-in-the-middle attacks and implement zero-trust networking.

**Acceptance Criteria**:
1. cert-manager deployed to Kubernetes for automated certificate lifecycle management
2. Internal CA created via cert-manager for issuing service certificates
3. Certificate resources created for each service with automatic rotation (30-day validity)
4. HttpClient configured in all services to present client certificate and validate server certificate
5. Service mesh evaluation documented (Istio vs. manual mTLS) with recommendation
6. mTLS handshake failure alerts configured in Prometheus Alertmanager
7. Performance testing validates <20ms p95 latency overhead (NFR9 target)

**Integration Verification**:
- **IV1**: Existing HTTP calls continue working with mTLS (transparent to application logic)
- **IV2**: Certificate rotation tested without service downtime (rolling update)
- **IV3**: Security test validates mTLS rejects connections without valid client certificates

**Dependencies**: Kubernetes, existing service HTTP communication

**Estimated Effort**: 7-10 days

---

### Story 1.13: Kubernetes NetworkPolicies for Micro-Segmentation

**As a** security engineer,  
**I want** Kubernetes NetworkPolicies restricting service-to-service communication,  
**so that** we implement zero-trust micro-segmentation and prevent lateral movement.

**Acceptance Criteria**:
1. Default-deny NetworkPolicy applied to all namespaces (deny all ingress/egress by default)
2. Per-service NetworkPolicies created allowing only required communication paths
3. API Gateway policy: Allow ingress from LoadBalancer, allow egress to all services
4. Service policies: Allow ingress from API Gateway only, allow egress to database/Redis/RabbitMQ
5. Admin Service policy: Allow egress to Keycloak, Vault, all services (for audit collection)
6. NetworkPolicy testing via pod-to-pod connectivity tests (validate blocked paths)
7. NetworkPolicy violations logged and alerted via Prometheus

**Integration Verification**:
- **IV1**: Existing service communication paths remain functional (whitelisted in policies)
- **IV2**: Unauthorized communication attempts blocked (test with temporary pod)
- **IV3**: Performance impact negligible (<1ms latency addition per iptables rules)

**Dependencies**: Kubernetes CNI with NetworkPolicy support (Calico, Cilium), Story 1.12 (mTLS)

**Estimated Effort**: 5-7 days

---

**Phase 2 Summary**: 4 stories, ~6-8 weeks
- Enhanced security with rotating tokens, branch claims, mTLS, NetworkPolicies

---

## Phase 3: Audit & Compliance (Stories 1.14-1.18) - Months 5-6

### Story 1.14: Audit Event Centralization in Admin Service

**As a** Compliance Officer,  
**I want** all audit events collected centrally in Admin Service,  
**so that** I have a unified audit trail for regulatory reporting and compliance.

**Acceptance Criteria**:
1. Audit event schema defined in Admin Service (`AuditEvent` entity with correlation ID, tamper-evident chain)
2. Admin Service exposes `/api/admin/audit/events` POST endpoint for audit event ingestion
3. All services updated to call Admin Service audit endpoint (replace direct `IAuditService` calls)
4. RabbitMQ option: Services publish audit events to `audit.events` exchange, Admin Service consumes
5. Audit event batching implemented to handle burst traffic (10,000 events/sec per NFR4)
6. Existing audit data in FinancialService database migrated to Admin Service (one-time ETL)
7. Audit query API implemented: GET `/api/admin/audit/events` with filtering (date, actor, action, entity)

**Integration Verification**:
- **IV1**: Existing audit queries in FinancialService redirected to Admin Service API (CR7 compatibility)
- **IV2**: Performance testing validates <100ms p95 for audit event ingestion
- **IV3**: Audit data integrity validated post-migration (record count, sample verification)

**Dependencies**: Story 1.4 (Admin Service), existing audit implementation in FinancialService

**Estimated Effort**: 7-10 days

---

### Story 1.15: Tamper-Evident Audit Chain Implementation

**As a** Compliance Officer,  
**I want** audit events cryptographically chained with hash links,  
**so that** I can detect any tampering attempts and prove audit integrity to Bank of Zambia regulators.

**Acceptance Criteria**:
1. `AuditEvent` entity extended with `PreviousEventHash` and `CurrentEventHash` fields
2. Hash calculation: SHA-256(PreviousHash + EventId + Timestamp + Actor + Action + EntityType + EntityId)
3. Genesis audit event created with `PreviousEventHash = null` on system initialization
4. Audit chain integrity verification API: POST `/api/admin/audit/verify-integrity` (validates chain for date range)
5. Chain break detection: Flag events with invalid `PreviousEventHash` and alert Compliance Officers
6. Integrity verification dashboard in Admin UI showing chain status (valid/broken)
7. Performance testing validates chain verification <5 seconds for 1M records (NFR5)

**Integration Verification**:
- **IV1**: Existing audit events function normally (legacy events without hash chain marked as pre-chain)
- **IV2**: Chain verification detects simulated tampering (unit test with hash modification)
- **IV3**: Audit performance unaffected (<10ms additional processing per event)

**Dependencies**: Story 1.14 (centralized audit), cryptography libraries

**Estimated Effort**: 5-7 days

---

### Story 1.16: MinIO WORM Audit Storage Integration

**As a** Compliance Officer,  
**I want** audit logs stored in MinIO with WORM object locking and 7-year retention,  
**so that** I comply with Bank of Zambia audit retention requirements with immutable storage guarantees.

**Acceptance Criteria**:
1. MinIO bucket `audit-logs` created with Object Lock enabled (Compliance mode)
2. Audit events exported to MinIO as JSON Lines (JSONL) files daily at midnight UTC
3. Object Lock retention configured for 2555 days (7 years per BoZ requirement)
4. Audit export includes tamper-evident hash chain for offline verification
5. MinIO access logging enabled with audit trail of who accessed archived audit logs
6. Disaster recovery tested: MinIO replication to secondary Zambian data center validated
7. Admin UI provides audit archive search with MinIO query integration (read-only)

**Integration Verification**:
- **IV1**: Existing SQL Server audit database remains as active/searchable data (90-day retention)
- **IV2**: Older audit data (>90 days) migrated to MinIO and queryable via Admin Service
- **IV3**: WORM lock prevents deletion/modification (test with manual delete attempt)

**Dependencies**: Story 1.15 (audit chain), MinIO existing deployment, Story 1.14 (centralized audit)

**Estimated Effort**: 5-7 days

---

### Story 1.17: Global Correlation ID Propagation

**As a** DevOps engineer,  
**I want** W3C Trace Context correlation IDs propagated across all HTTP and RabbitMQ calls,  
**so that** I can trace requests end-to-end through distributed microservices.

**Acceptance Criteria**:
1. `IntelliFin.Shared.Observability` library includes `CorrelationIdMiddleware` (already in Story 1.6)
2. API Gateway generates correlation ID (traceparent header) if not present in incoming request
3. HttpClient calls automatically include `traceparent` header in outbound requests
4. RabbitMQ message publishing adds `correlation_id` and `traceparent` to message properties
5. All audit events include `CorrelationId` field extracted from trace context
6. Jaeger trace UI links to audit events via correlation ID (URL deep link)
7. Loki log queries filterable by correlation ID (label extraction configured)

**Integration Verification**:
- **IV1**: Existing ErrorLog `CorrelationId` field populated automatically (already exists per brownfield analysis)
- **IV2**: End-to-end trace validated: API Gateway → Loan Origination → Credit Bureau → Collections (single correlation ID)
- **IV3**: Performance overhead <1ms per hop (W3C header parsing is lightweight)

**Dependencies**: Story 1.6 (OpenTelemetry), Story 1.7 (Jaeger), Story 1.9 (Loki), Story 1.14 (audit)

**Estimated Effort**: 3-5 days

---

### Story 1.18: Offline CEO App Audit Merge Implementation

**As a** CEO,  
**I want** offline audit events from my desktop app merged into the central audit system,  
**so that** my offline loan approvals are included in compliance audit trails.

**Acceptance Criteria**:
1. CEO desktop app batches audit events locally in SQLite during offline periods
2. Sync endpoint `/api/admin/audit/merge-offline` accepts batch audit event uploads
3. Conflict resolution: Detect duplicate events via correlation ID + timestamp and skip
4. Offline audit events inserted into tamper-evident chain at appropriate chronological position
5. Chain re-hashing: Events after merged offline events recalculate `PreviousEventHash`
6. Merge audit trail: Log merge operations with event count, conflicts detected, re-hash count
7. CEO app UI indicates sync status (pending events, last successful sync timestamp)

**Integration Verification**:
- **IV1**: Existing CEO offline loan origination workflows unaffected (audit is additive)
- **IV2**: Integrity verification passes post-offline merge (no chain breaks introduced)
- **IV3**: Performance test: 1000-event offline batch merge completes <30 seconds

**Dependencies**: Story 1.15 (audit chain), Story 1.14 (centralized audit), CEO desktop app existing functionality

**Estimated Effort**: 7-10 days

---

**Phase 3 Summary**: 5 stories, ~8-10 weeks
- Audit hardened with centralized audit, tamper-evident chain, WORM storage, correlation IDs, offline merge

---

## Phase 4: Governance & Workflows (Stories 1.19-1.24) - Months 7-8

### Story 1.19: JIT Privilege Elevation with Camunda Workflows

**As a** developer,  
**I want** just-in-time (JIT) privilege elevation with Camunda approval workflows,  
**so that** I can request temporary elevated permissions for production debugging without permanent admin access.

**Acceptance Criteria**:
1. Camunda BPMN process `access-elevation-approval.bpmn` created with human task for manager approval
2. Admin Service endpoint POST `/api/admin/access/elevate` triggers Camunda workflow with elevation request
3. Elevation request includes: userId, requestedRoles, justification, duration (max 8 hours)
4. Manager receives real-time notification via SignalR with approve/reject actions
5. Upon approval, temporary role assignments created in Keycloak with TTL metadata
6. Scheduled job checks TTL and automatically revokes expired elevations every 5 minutes
7. Audit events logged: elevation requested, approved/rejected, activated, expired, manually revoked
8. Admin UI shows active elevated sessions with "Revoke Now" button for emergency de-escalation

**Integration Verification**:
- **IV1**: Existing permanent role assignments unaffected by JIT elevation logic
- **IV2**: User JWT tokens refreshed automatically after elevation approval (new roles in token)
- **IV3**: Performance test: Elevation approval-to-activation completes <15 seconds (NFR11)

**Dependencies**: Story 1.5 (Keycloak Admin API), Camunda 8 existing integration, Story 1.3 (JWT tokens)

**Estimated Effort**: 7-10 days

---

### Story 1.20: Step-Up MFA Integration

**As a** System Administrator,  
**I want** step-up multi-factor authentication for sensitive operations,  
**so that** high-risk actions (high-value loan approvals, config changes) require secondary authentication.

**Acceptance Criteria**:
1. Keycloak configured with OTP (Time-based One-Time Password) authenticator via Google Authenticator
2. Camunda BPMN process `step-up-mfa-challenge.bpmn` created with MFA validation task
3. Sensitive API endpoints decorated with `[RequiresMfa]` attribute triggering MFA challenge
4. MFA challenge flow: API returns 401 with `mfa_required` error → Frontend redirects to MFA screen → User enters OTP → MFA verification endpoint validates → Original API request retried with MFA-validated token
5. MFA-validated tokens include `amr` (Authentication Methods Reference) claim with `mfa` value
6. MFA validation expires after 15 minutes (re-challenge required for new sensitive operations)
7. Admin UI allows System Administrators to configure which operations require MFA (FR9)

**Integration Verification**:
- **IV1**: Non-sensitive operations continue without MFA (no user friction for normal workflows)
- **IV2**: MFA enrollment flow tested (user onboarding with QR code generation)
- **IV3**: MFA failure handling: User locked out after 3 failed OTP attempts (existing lockout policy)

**Dependencies**: Story 1.1 (Keycloak), Camunda workflows, frontend integration

**Estimated Effort**: 7-10 days

---

### Story 1.21: Expanded Operational Roles and SoD Enforcement

**As a** Compliance Officer,  
**I want** expanded RBAC roles with Segregation of Duties (SoD) enforcement,  
**so that** we prevent conflicting role assignments and meet BoZ compliance requirements.

**Acceptance Criteria**:
1. New Keycloak realm roles created: Collections Officer, Compliance Officer, Treasury Officer, GL Accountant, Auditor, Risk Manager, Branch Manager (FR6)
2. Role hierarchy configured in Keycloak: Branch Manager > Collections Officer, CEO > all roles
3. SoD policy matrix defined in Admin Service database: conflicting role pairs (e.g., Loan Processor + Loan Approver)
4. Role assignment endpoint validates SoD policy before creating assignment (returns 409 Conflict if SoD violation)
5. SoD override workflow: Compliance Officer can approve SoD exceptions via Camunda with documented justification
6. Admin UI displays SoD conflict warnings when assigning roles with "Request Exception" workflow trigger
7. Quarterly SoD compliance report generated showing all active role assignments and any approved exceptions

**Integration Verification**:
- **IV1**: Existing V1 roles (6-8 original roles) preserved and functional
- **IV2**: Users with expanded roles tested across all IntelliFin services (loan origination, collections, reporting)
- **IV3**: SoD policy enforced at API level (attempt to assign conflicting roles via API blocked)

**Dependencies**: Story 1.5 (Keycloak Admin API), Story 1.19 (Camunda workflows for exceptions)

**Estimated Effort**: 7-10 days

---

### Story 1.22: Policy-Driven Configuration Management

**As a** System Administrator,  
**I want** sensitive configuration changes to require policy validation and approval,  
**so that** we prevent unauthorized or risky config changes that could impact production.

**Acceptance Criteria**:
1. Configuration policy schema defined in Admin Service: `config-policy.yaml` with fields (name, value, requires_approval, approval_workflow)
2. Admin Service endpoint POST `/api/admin/config/policies` manages policy definitions
3. Configuration change requests validated against policy: If `requires_approval: true`, trigger Camunda workflow
4. Camunda BPMN process `config-change-approval.bpmn` routes to manager for approval
5. Approved config changes persisted to Kubernetes ConfigMaps via Kubernetes API
6. Config change audit: Log change request, approver, old value, new value, deployment timestamp
7. Config rollback API: POST `/api/admin/config/rollback` reverts to previous version (Git history-based)
8. Admin UI shows pending config change approvals with approve/reject actions

**Integration Verification**:
- **IV1**: Non-sensitive config changes (e.g., log levels) bypass approval workflow (immediate effect)
- **IV2**: Sensitive config changes (e.g., JWT expiry, audit retention) blocked until approved
- **IV3**: Config deployment verified via ArgoCD sync status (Story 1.25 dependency)

**Dependencies**: Story 1.19 (Camunda workflows), Kubernetes ConfigMaps, Story 1.25 (GitOps for full automation)

**Estimated Effort**: 7-10 days

---

### Story 1.23: Vault Secret Rotation Automation

**As a** Security Engineer,  
**I want** HashiCorp Vault secrets automatically rotated every 90 days,  
**so that** we reduce risk from long-lived credentials and meet security best practices.

**Acceptance Criteria**:
1. Vault database secrets engine configured for SQL Server with dynamic credential generation
2. Services updated to retrieve database credentials from Vault at startup (no hardcoded connection strings)
3. Vault lease renewal implemented: Services refresh database credentials before TTL expiration
4. Secret rotation schedule: Vault rotates root database credentials every 90 days
5. Zero-downtime rotation: Rolling service updates handle credential transition (5-minute overlap per NFR13)
6. Vault audit logging enabled with audit events sent to Admin Service for centralized trail
7. Admin UI displays Vault secret inventory (metadata only: secret path, TTL, last rotation)
8. Rotation failure alerts: Prometheus monitors Vault metrics and alerts on rotation errors

**Integration Verification**:
- **IV1**: Existing services migrate from `appsettings.json` connection strings to Vault without downtime
- **IV2**: Database connectivity maintained during credential rotation (test with rolling update)
- **IV3**: Vault secret access audited (verify audit events in Admin Service audit trail)

**Dependencies**: Story 1.4 (Admin Service), Vault existing deployment, Story 1.14 (centralized audit)

**Estimated Effort**: 7-10 days

---

### Story 1.24: Quarterly Access Recertification Workflows

**As a** Compliance Officer,  
**I want** automated quarterly access recertification with manager reviews,  
**so that** we ensure users only retain necessary access and meet BoZ compliance requirements.

**Acceptance Criteria**:
1. Camunda BPMN process `access-recertification.bpmn` scheduled to trigger quarterly (cron-based)
2. Recertification campaign generated: List all users with their current roles and permissions
3. Managers receive notifications via SignalR/email with recertification tasks in Admin UI
4. Manager review UI shows team members' access with approve/revoke/modify actions
5. Non-response escalation: If manager doesn't respond within 14 days, escalate to CEO
6. Auto-revocation: After 30 days with no response, automatically revoke all non-essential roles (keep base user role)
7. Recertification audit trail: Log campaign start, manager decisions, escalations, auto-revocations
8. Compliance dashboard shows recertification status: completion percentage, overdue reviews, revoked access count

**Integration Verification**:
- **IV1**: Existing user access unaffected during recertification campaign (reviews are non-blocking)
- **IV2**: Revoked access immediately removes roles from Keycloak (JWT tokens invalidated on next refresh)
- **IV3**: Performance test: Campaign generation for 500 users completes <5 minutes

**Dependencies**: Story 1.5 (Keycloak Admin API), Story 1.19 (Camunda workflows), Story 1.21 (expanded roles)

**Estimated Effort**: 7-10 days

---

**Phase 4 Summary**: 6 stories, ~8-10 weeks
- Governance operational with JIT elevation, MFA, SoD, policy-driven config, Vault rotation, access recertification

---

## Phase 5: Zero-Trust & PAM (Stories 1.25-1.29) - Months 9-10

### Story 1.25: GitOps Configuration Deployment with ArgoCD

**As a** DevOps Engineer,  
**I want** ArgoCD managing Kubernetes configuration deployment from Git,  
**so that** configuration changes are version-controlled, auditable, and automatically synced to clusters.

**Acceptance Criteria**:
1. ArgoCD deployed to Kubernetes with admin console accessible at `https://argocd.intellifin.local`
2. Git repository `intellifin-k8s-config` created with Helm charts and Kubernetes manifests
3. ArgoCD Application resources created for each microservice (Admin Service, Identity Service, etc.)
4. Auto-sync enabled: ArgoCD monitors Git repo and syncs changes to Kubernetes within 5 minutes (NFR12)
5. Health checks configured: ArgoCD validates deployment health post-sync (pod ready, health endpoint responding)
6. Automated rollback: If health checks fail, ArgoCD reverts to previous Git commit and alerts DevOps
7. ArgoCD SSO integrated with Keycloak for developer access control
8. Sync history and audit trail visible in ArgoCD UI (who deployed what, when)

**Integration Verification**:
- **IV1**: Existing kubectl/helm deployments replaced by GitOps (no manual cluster changes)
- **IV2**: Config change flow tested: Developer creates PR → Manager approves → Merge → ArgoCD syncs → Pods updated
- **IV3**: Rollback tested: Introduce failing config → Health check detects → ArgoCD auto-reverts

**Dependencies**: Kubernetes, Git repository, Story 1.22 (policy-driven config for workflow integration)

**Estimated Effort**: 5-7 days

---

### Story 1.26: Container Image Signing and SBOM Generation

**As a** Security Engineer,  
**I want** container images cryptographically signed with Cosign and SBOM included,  
**so that** we verify image integrity and have software bill of materials for vulnerability management.

**Acceptance Criteria**:
1. Cosign installed in CI/CD pipeline (GitHub Actions)
2. Signing key generated and stored in Vault (private key) with public key distributed to clusters
3. CI/CD pipeline updated: After `docker build`, run `cosign sign` with Vault-retrieved key
4. Syft generates SBOM in SPDX format during build: `syft <image> -o spdx-json > sbom.json`
5. SBOM attached to image as OCI artifact or stored in artifact repository
6. Trivy scans SBOM for known CVEs and blocks deployment if critical vulnerabilities found (NFR14)
7. Kubernetes admission controller validates Cosign signatures before pod creation (reject unsigned images)
8. Admin UI displays image inventory with signature status, SBOM link, CVE count

**Integration Verification**:
- **IV1**: Existing unsigned images blocked by admission controller (test with unsigned nginx image)
- **IV2**: Signed images deploy successfully with signature verification logged
- **IV3**: CVE detection tested: Introduce vulnerable dependency → Trivy blocks → Developer fixes → Re-scan passes

**Dependencies**: CI/CD pipeline (GitHub Actions), Kubernetes admission controller (e.g., Kyverno, OPA Gatekeeper)

**Estimated Effort**: 7-10 days

---

### Story 1.27: Bastion Host Deployment with PAM

**As a** System Administrator,  
**I want** bastion host for privileged infrastructure access,  
**so that** SSH/RDP access to servers is centrally controlled, audited, and secure.

**Acceptance Criteria**:
1. Bastion host deployed as hardened VM/container in DMZ network with minimal attack surface
2. Bastion accessible only via VPN with MFA (no direct internet exposure)
3. Bastion integrates with Keycloak for authentication (OIDC device flow or LDAP)
4. PAM solution evaluated: HashiCorp Boundary, Teleport, or StrongDM (document recommendation)
5. Bastion allows SSH to production servers only with approved access (no direct SSH from workstations)
6. Session initiation logs sent to Admin Service audit trail (who, when, target server)
7. Bastion health monitoring configured with Prometheus metrics (active sessions, authentication failures)

**Integration Verification**:
- **IV1**: Existing direct SSH access disabled (firewall rules updated, only bastion allowed)
- **IV2**: Developers can SSH via bastion with Keycloak authentication (test user flow)
- **IV3**: Unauthorized access attempts blocked and alerted (test with non-approved user)

**Dependencies**: Network infrastructure (DMZ, VPN), Keycloak authentication, Story 1.14 (centralized audit)

**Estimated Effort**: 7-10 days

---

### Story 1.28: JIT Infrastructure Access with Vault Dynamic Credentials

**As a** DevOps Engineer,  
**I want** just-in-time infrastructure access with Camunda approval and Vault-generated credentials,  
**so that** server access is time-bound, approved, and automatically revoked.

**Acceptance Criteria**:
1. Camunda BPMN process `infrastructure-access-request.bpmn` created with manager approval task
2. Admin Service endpoint POST `/api/admin/pam/request-access` triggers workflow with access request (server, duration, justification)
3. Upon approval, Vault SSH secrets engine generates temporary SSH key pair with TTL (max 8 hours)
4. Vault provisions temporary Unix user on target server via SSH CA or dynamic secret injection
5. Approved user receives SSH private key via secure download (one-time link, expires in 5 minutes)
6. Automatic revocation: Vault deletes Unix user and invalidates SSH key after TTL expiration
7. Access request audit trail: request, approval, credentials issued, access started, credentials revoked
8. Admin UI shows active infrastructure sessions with "Revoke Now" button for emergency termination

**Integration Verification**:
- **IV1**: Existing permanent SSH access migrated to JIT model (admins request access as needed)
- **IV2**: SSH key works during TTL, fails after expiration (test with 5-minute TTL)
- **IV3**: Approval workflow tested: DevOps requests → Manager approves → Credentials issued within 15 seconds (NFR11)

**Dependencies**: Story 1.27 (bastion host), Story 1.19 (Camunda JIT workflows), Vault SSH secrets engine

**Estimated Effort**: 10-14 days

---

### Story 1.29: SSH Session Recording in MinIO

**As a** Compliance Officer,  
**I want** all SSH sessions recorded and stored in MinIO,  
**so that** I have audit evidence of administrator actions for compliance and forensic analysis.

**Acceptance Criteria**:
1. Bastion host configured with session recording (ttyrec, asciinema, or PAM solution built-in)
2. Session recordings uploaded to MinIO bucket `ssh-recordings` in MP4 or asciicast format
3. MinIO retention policy: 7-year retention for session recordings (same as audit logs per BoZ)
4. Session recording metadata stored in Admin Service database (sessionId, userId, targetServer, startTime, endTime, recordingUrl)
5. Admin UI provides session playback: Embedded video player or asciicast player for reviewing sessions
6. Search interface: Find sessions by user, server, date range, commands executed (if text-based recording)
7. Session recordings automatically encrypted in MinIO (server-side encryption enabled)

**Integration Verification**:
- **IV1**: SSH sessions function normally with recording enabled (no latency impact per NFR15)
- **IV2**: Recording upload completes within 30 seconds of session termination (NFR15)
- **IV3**: Session playback tested: View 1-hour session recording in Admin UI successfully

**Dependencies**: Story 1.27 (bastion host), MinIO existing deployment, Story 1.14 (centralized audit)

**Estimated Effort**: 7-10 days

---

**Phase 5 Summary**: 5 stories, ~8-10 weeks
- Zero-trust infrastructure with GitOps, signed images, bastion PAM, JIT access, session recording

---

## Phase 6: Advanced Observability (Stories 1.30-1.34) - Months 11-12

### Story 1.30: BoZ Compliance Dashboards in Grafana

**As a** Compliance Officer,  
**I want** Grafana dashboards showing Bank of Zambia compliance KPIs,  
**so that** I can monitor regulatory metrics and generate compliance reports for BoZ audits.

**Acceptance Criteria**:
1. Grafana dashboard `BoZ-Compliance-Overview.json` created with panels:
   - Audit event count (daily, monthly, annual)
   - Access recertification completion rate (quarterly)
   - Security incident count (categorized by severity)
   - Loan classification accuracy (Current, Special Mention, Substandard, Doubtful, Loss)
   - User access violations (SoD conflicts, unauthorized access attempts)
2. Dashboard data sourced from Prometheus metrics (Admin Service exports compliance metrics)
3. Time range selector: Default 30 days, extendable to 1 year for annual reports
4. Export functionality: Download dashboard as PDF for BoZ submission
5. Real-time updates: Dashboard auto-refreshes every 30 seconds for live monitoring
6. Alert annotations: Compliance violations highlighted in red with drill-down links to audit trail
7. Role-based access: Compliance Officers and Auditors have read-only dashboard access

**Integration Verification**:
- **IV1**: Dashboard metrics match source data (validate against SQL Server audit tables)
- **IV2**: PDF export includes all panels with readable formatting (test multi-page export)
- **IV3**: Dashboard performance: Loads in <5 seconds for 30-day time range

**Dependencies**: Story 1.8 (Grafana), Story 1.14 (centralized audit), Story 1.24 (access recertification)

**Estimated Effort**: 5-7 days

---

### Story 1.31: Cost-Performance Monitoring Dashboards

**As a** CFO,  
**I want** dashboards showing infrastructure cost and performance metrics per service,  
**so that** I can optimize spending while maintaining SLA commitments.

**Acceptance Criteria**:
1. Grafana dashboard `Cost-Performance-Overview.json` created with panels:
   - Per-service infrastructure cost (CPU, memory, storage) with monthly projections
   - Cost per transaction (e.g., cost per loan application processed)
   - Resource utilization (CPU %, memory %, pod count) with efficiency score
   - SLA compliance (p50/p95/p99 latency, error rate %, uptime %)
   - Cost anomaly detection (highlight services with >20% cost increase month-over-month)
2. Cost data sourced from Kubernetes resource metrics (Prometheus) with cost model ($ per CPU-hour, $ per GB-month)
3. Cost allocation tags: Services tagged by domain (Loan Origination, Collections, etc.) for business unit chargeback
4. Optimization recommendations: Panel shows underutilized services (avg CPU <30%) for right-sizing
5. Forecasting: Trend lines project next 3 months' costs based on historical usage
6. Export to CSV: Cost breakdown exportable for finance reporting
7. Role-based access: CFO, CTO, Service Owners have read-only dashboard access

**Integration Verification**:
- **IV1**: Cost estimates within 10% accuracy (validate against actual cloud bills)
- **IV2**: Performance metrics match Jaeger trace data (cross-validate latency numbers)
- **IV3**: Dashboard identifies real optimization opportunity (test with intentionally over-provisioned service)

**Dependencies**: Story 1.8 (Grafana), Story 1.6 (OpenTelemetry metrics), Kubernetes resource metrics

**Estimated Effort**: 7-10 days

---

### Story 1.32: Automated Alerting and Incident Response

**As a** DevOps Engineer,  
**I want** automated alerts for critical system events with incident response playbooks,  
**so that** I can respond quickly to production issues and minimize downtime.

**Acceptance Criteria**:
1. Prometheus Alertmanager configured with alert rules for critical events:
   - Keycloak down (no successful authentication in 5 minutes)
   - Audit chain break detected (tamper-evident hash mismatch)
   - mTLS failure (service-to-service TLS handshake errors >10/minute)
   - High error rate (service error rate >5% for 5 minutes)
   - Vault unavailable (secret fetch failures)
   - Database connection pool exhaustion
2. Alert routing configured: Critical alerts → PagerDuty/Slack, warnings → email
3. Incident response playbooks created in Admin UI: Step-by-step resolution guides per alert type
4. Camunda BPMN process `incident-response.bpmn` automates initial response (e.g., scale pods, failover database)
5. Alert silencing: On-call engineer can silence alerts during maintenance windows via Admin UI
6. Alert history dashboard: View all alerts (fired, resolved, silenced) with MTTR (Mean Time To Resolution) metrics
7. Post-incident review workflow: Camunda triggers RCA (Root Cause Analysis) task after major incidents

**Integration Verification**:
- **IV1**: Test alerts fire correctly (simulate Keycloak downtime → Verify alert received)
- **IV2**: Alert resolution tested (fix issue → Verify alert auto-resolves)
- **IV3**: Incident playbook effectiveness validated (on-call engineer follows playbook → Issue resolved within RTO)

**Dependencies**: Story 1.8 (Prometheus + Alertmanager), PagerDuty/Slack integration, Camunda workflows

**Estimated Effort**: 7-10 days

---

### Story 1.33: Disaster Recovery Runbook Automation

**As a** DevOps Engineer,  
**I want** automated disaster recovery runbooks with quarterly testing,  
**so that** I can confidently fail over to secondary data center and meet RPO/RTO targets.

**Acceptance Criteria**:
1. DR runbook scripts created in `infra/disaster-recovery/` folder:
   - `failover-to-secondary.sh`: Switches DNS to secondary data center, promotes read replica to primary
   - `validate-failover.sh`: Tests health checks, audit integrity, service availability
   - `failback-to-primary.sh`: Restores primary data center after recovery
2. Automated quarterly DR tests scheduled via CI/CD (cron job triggers runbook execution)
3. DR test validation: Verify RPO <1 hour (check last replicated transaction timestamp), RTO <4 hours (time to full service restoration)
4. Audit integrity verification post-DR: Run tamper-evident chain validation across all audit data
5. DR test report generated: Success/failure status, RPO/RTO achieved, issues encountered, remediation plan
6. Runbook version control: DR scripts in Git with changelog documenting updates
7. Admin UI displays DR test history and upcoming test schedule

**Integration Verification**:
- **IV1**: DR test does not impact production (test in isolated environment or during maintenance window)
- **IV2**: Failover completed within 4-hour RTO target (NFR17)
- **IV3**: Audit data integrity maintained post-failover (zero data loss, chain intact)

**Dependencies**: Secondary Zambian data center infrastructure, Story 1.15 (audit chain), Story 1.16 (MinIO replication)

**Estimated Effort**: 10-14 days

---

### Story 1.34: DR Testing with RPO/RTO Validation

**As a** CTO,  
**I want** quarterly disaster recovery tests with automated RPO/RTO validation,  
**so that** I have documented evidence of business continuity readiness for BoZ regulators.

**Acceptance Criteria**:
1. DR test execution automated via Camunda workflow `disaster-recovery-test.bpmn` (scheduled quarterly)
2. Pre-test checklist: Verify secondary data center health, backup freshness, team availability
3. DR test execution: Run `failover-to-secondary.sh`, monitor RTO timer, validate service health
4. RPO validation: Query last replicated transaction timestamp, calculate time delta (target: <1 hour)
5. RTO validation: Measure time from failover initiation to all health checks passing (target: <4 hours)
6. Post-test report generated: Includes RPO/RTO actual vs. target, issues encountered, screenshots of dashboards
7. Compliance evidence: DR test report stored in MinIO with WORM retention for BoZ audits
8. Lessons learned: Camunda workflow triggers retrospective task for team to document improvements

**Integration Verification**:
- **IV1**: DR test report includes all required BoZ compliance evidence (audit trail, system screenshots)
- **IV2**: RPO/RTO targets met in test (validate within tolerances: RPO <1 hour, RTO <4 hours)
- **IV3**: Audit integrity verified: Post-DR audit chain validation passes (no broken links)

**Dependencies**: Story 1.33 (DR runbooks), Camunda workflows, Story 1.16 (MinIO WORM for compliance evidence)

**Estimated Effort**: 5-7 days

---

**Phase 6 Summary**: 5 stories, ~8-10 weeks
- Advanced observability with compliance dashboards, cost monitoring, automated alerting, DR automation, quarterly testing

---

## Epic Summary

### All 34 Stories Defined

**Epic Title**: System Administration Control Plane: Enterprise Governance & Observability

**Total Duration**: 12 months (48-60 weeks)

**Story Breakdown by Phase**:
- **Phase 1 (Foundation)**: 9 stories - Keycloak, Admin Service, OpenTelemetry, Jaeger, Prometheus, Grafana, Loki
- **Phase 2 (Enhanced Security)**: 4 stories - Rotating tokens, branch claims, mTLS, NetworkPolicies
- **Phase 3 (Audit & Compliance)**: 5 stories - Centralized audit, tamper-evident chain, WORM, correlation IDs, offline merge
- **Phase 4 (Governance & Workflows)**: 6 stories - JIT elevation, MFA, SoD, policy config, Vault rotation, recertification
- **Phase 5 (Zero-Trust & PAM)**: 5 stories - GitOps, image signing, bastion, JIT infra access, session recording
- **Phase 6 (Advanced Observability)**: 5 stories - BoZ dashboards, cost monitoring, alerting, DR runbooks, DR testing

**Total Estimated Effort**: ~180-240 days of development work (avg 6-8 days per story)

**Technology Stack Decisions** (locked in):
- **Identity Provider**: Keycloak
- **Distributed Tracing**: Jaeger
- **Centralized Logging**: Loki
- Metrics: Prometheus + Grafana
- Telemetry: OpenTelemetry
- Workflows: Camunda 8 (Zeebe)
- Secrets: HashiCorp Vault
- WORM Storage: MinIO

---

## Document Status

**PRD Status**: ✅ Complete and Ready for Architecture Phase

**Next Steps**:
1. **Architecture Phase**: Create detailed architecture document with ADRs for technology choices
2. **PO Validation**: Product Owner reviews PRD with po-master-checklist
3. **Document Sharding**: PO agent shards PRD into `docs/domains/system-administration/prd/` for IDE integration
4. **Story Creation**: SM agent creates stories from epic structure

**Document Location**: `docs/domains/system-administration/system-administration-control-plane-prd.md`

**Related Documents**:
- Brownfield Analysis: `docs/domains/system-administration/system-administration-brownfield-analysis.md`
- Architecture (to be created): `docs/domains/system-administration/system-administration-control-plane-architecture.md`

---

**Document End**
