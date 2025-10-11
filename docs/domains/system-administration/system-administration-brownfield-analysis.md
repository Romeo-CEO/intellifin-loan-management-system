# IntelliFin System Administration - Brownfield Architecture Analysis

## Document Overview

**Purpose**: This document captures the CURRENT STATE of the IntelliFin Loan Management System's System Administration components and analyzes the gap between existing implementation and the planned transformation into an enterprise-grade "control plane" for governance, security, and compliance.

**Enhancement Scope**: Transform System Administration from an operational baseline into a **strategic governance and control hub** by introducing:
- Self-hosted IdP (Keycloak/OpenIddict) with branch-scoped JWTs
- Admin microservice for centralized orchestration
- Enhanced RBAC with operational roles, SoD, and just-in-time elevation
- Tamper-evident audit with WORM retention and offline merge
- Policy-driven configuration governance with GitOps
- Zero-trust runtime security (mTLS, network policies)
- In-country observability stack (OpenTelemetry, Prometheus, Grafana, Loki, Jaeger)
- Bastion-based PAM with JIT infrastructure access

## Final Decisions (Locked)

- Workflow engine: Self-hosted Camunda 8 (Zeebe + Operate + Tasklist) in Zambia; SaaS removed; workflow data and credentials remain in-country.
- Identity: Self-hosted Keycloak; Admin microservice is policy steward (RBAC/ABAC, JIT elevation, approvals, JML); optional locally brokered AAD B2C federation for staff only.
- ABAC enforcement: Edge enforcement in ApiGateway (YARP) with branch/scope blocking by default; per-service EF Core global query filters; JWT claims include sub, roles, branch_id, allowed_branches, scope, tenant_id, elevation_exp; correlation IDs propagated.
- Observability: OpenTelemetry across all services; Prometheus/Grafana for metrics; Loki for logs; Jaeger for traces; PII scrubbing in pipelines.
- Secrets/config: Vault Agent sidecar with Kubernetes auth; dynamic DB creds (Database engine), PKI for service certs, Transit for crypto; GitOps via ArgoCD; policy-driven approvals via Camunda.
- Zero-trust: Linkerd service mesh for automatic mTLS; Kubernetes NetworkPolicies for micro-segmentation; least-privilege service accounts.
- Audit retention: 10 years enforced via MinIO WORM (object lock) with cryptographic hash chaining and offline audit merge; unified correlation.
- PAM: Teleport bastion for JIT infra access with Camunda approvals and full session recording; break-glass with mandatory postmortem.
- Supply chain: Cosign image signing; Syft SBOM; Trivy scanning; Kyverno policy gates.

### Document Scope

**Focused Analysis**: This document focuses on System Administration domain components that will be affected by the control plane enhancement, specifically:
- Authentication & Authorization (Identity Service)
- Audit & Compliance
- Configuration Management
- Secrets Management (Vault integration)
- Workflow Orchestration (Camunda integration)
- Observability & Monitoring

### Change Log

| Date       | Version | Description                                    | Author  |
|------------|---------|------------------------------------------------|---------|
| 2025-10-10 | 1.0     | Initial brownfield analysis for control plane | Winston |

---

## Quick Reference - Key Files and Entry Points

### Critical Files for Understanding Current System

**Identity Service (Current Authentication/Authorization)**
- **Main Entry**: `apps/IntelliFin.IdentityService/Program.cs`
- **User Model**: `apps/IntelliFin.IdentityService/Models/ApplicationUser.cs`
- **Service Configuration**: `apps/IntelliFin.IdentityService/Extensions/ServiceCollectionExtensions.cs`
- **Configuration**: `apps/IntelliFin.IdentityService/appsettings.json`
- **Controllers**:
  - `apps/IntelliFin.IdentityService/Controllers/PermissionRoleBridgeController.cs`
  - `apps/IntelliFin.IdentityService/Controllers/RoleRulesController.cs`
  - `apps/IntelliFin.IdentityService/Controllers/RuleValidationController.cs`

**Audit & Compliance (Current Implementation)**
- **Audit Controller**: `apps/IntelliFin.FinancialService/Controllers/AuditTrailController.cs`
- **Monitoring Entities**: `libs/IntelliFin.Shared.DomainModels/Entities/MonitoringEntities.cs`
- **Audit Service Interface**: `libs/IntelliFin.Shared.DomainModels/Services/IAuditService.cs`

**Configuration & Secrets**
- **Environment Variables**: `.env` (development configuration)
- **Deployment Config**: `docs/deployment/README.md`
- **Service Appsettings**: `apps/*/appsettings.json` (per-service)

**Infrastructure Documentation**
- **Tech Stack**: `docs/architecture/tech-stack.md`
- **System Architecture**: `docs/architecture/system-architecture.md`
- **Infrastructure & Operations**: `docs/domains/system-administration/infrastructure-and-operations.md`
- **Security & Access Management**: `docs/domains/system-administration/security-and-access-management.md`

**Workflow Orchestration**
- **Workflow Service**: `apps/IntelliFin.LoanOriginationService/Services/WorkflowService.cs`
- **External Task Worker**: `apps/IntelliFin.LoanOriginationService/Services/ExternalTaskWorkerService.cs`
- **BPMN Definitions**: `apps/IntelliFin.LoanOriginationService/BPMN/*.bpmn`

### Enhancement Impact Areas

**Files/Areas That Will Be Created or Significantly Modified**:

1. **NEW: Admin Microservice** (to be created)
   - `apps/IntelliFin.AdminService/` - Centralized control plane service
   - Orchestrates identity, access, policy, audit across all services

2. **MODIFIED: Identity Service** (major refactoring required)
   - Replace ASP.NET Core Identity with Keycloak/OpenIddict
   - Add branch-scoped JWT claims
   - Implement rotating refresh tokens
   - Add AAD B2C federation (optional)
   - Implement step-up MFA with Camunda approval workflows

3. **NEW/MODIFIED: Audit System** (significant enhancement)
   - Add tamper-evident chain storage
   - Implement MinIO WORM retention
   - Add global correlation IDs across all services
   - Create offline audit merge capability
   - Enhance `IAuditService` with integrity verification

4. **NEW: Configuration Governance** (to be created)
   - Policy-driven configuration management
   - Camunda workflow approval for sensitive changes
   - GitOps integration for config-as-code
   - Vault secret rotation automation

5. **NEW: Observability Stack** (major infrastructure addition)
   - OpenTelemetry instrumentation across all services
   - Prometheus/Grafana for metrics
   - Loki for centralized logging
   - Jaeger for distributed tracing
   - Compliance dashboards
   - Cost-performance monitoring

6. **NEW: Zero-Trust Runtime** (infrastructure enhancement)
   - mTLS between all services (Linkerd service mesh)
   - Network policies in Kubernetes
   - Least-privilege service accounts
   - Signed SBOM-scanned container images

7. **NEW: PAM & Bastion Access** (to be created)
   - Bastion-based privileged access management
   - JIT infrastructure access with approval workflows
   - Full session recording and evidence capture

---

## Section 1: High-Level Architecture Overview

### Current Architecture Summary

**Repository Structure**: Monorepo (apps + libs + shared infrastructure)
- **Type**: NX-style monorepo with multiple microservices
- **Package Manager**: npm (for frontend), dotnet CLI (for backend)
- **Build System**: .NET 9 SDK, Docker Compose for local infrastructure

**Technology Stack** (As-Is):

| Category               | Technology            | Version     | Current Usage                                  |
|------------------------|-----------------------|-------------|------------------------------------------------|
| **Backend Language**   | C#                    | 12.0 (.NET 9)| All microservices                             |
| **Backend Framework**  | ASP.NET Core          | 9.0         | Web API development with Minimal APIs          |
| **Authentication**     | ASP.NET Core Identity | 9.0         | User auth, RBAC, JWT tokens                    |
| **Database**           | SQL Server            | 2022        | Primary data storage                           |
| **Caching**            | Redis                 | 7.2+        | Session mgmt, token revocation (denylist)      |
| **Message Queue**      | RabbitMQ              | 3.12+       | Event-driven communication                     |
| **Workflow Engine**    | Camunda 8 (Zeebe)     | 8.4+        | Loan origination workflows, PMEC integration   |
| **Object Storage**     | MinIO                 | 2024-01     | KYC document storage                           |
| **Secrets Management** | HashiCorp Vault       | 1.15+       | Credential storage (configured but underutilized)|
| **Logging**            | Serilog + File        | 3.1+        | File-based structured logging (not centralized)|
| **Monitoring**         | Not implemented       | N/A         | **GAP**: No APM, metrics, or tracing           |

### Current Microservices Architecture

**Existing Services**:

1. **IntelliFin.IdentityService** (port 5235)
   - ASP.NET Core Identity for user management
   - JWT token generation (15-min access, 7-day refresh)
   - Redis-based token revocation (denylist)
   - Role-based authorization with custom rules
   - **Current Roles**: V1 business roles (Loan Officer, Credit Analyst, Head of Credit, Finance Officer, CEO, System Administrator)
   - **Branch-scoped access**: Basic implementation via `BranchId` in ApplicationUser

2. **IntelliFin.ApiGateway** (port 5033)
   - Unified API entry point
   - Routes requests to microservices
   - JWT validation on all routes

3. **IntelliFin.ClientManagement** (port 5224)
   - Customer profile management
   - KYC/AML compliance
   - Uses MinIO for document storage

4. **IntelliFin.LoanOriginationService** (port 5193)
   - Loan application processing
   - Integrates with Camunda for workflows
   - BPMN-defined approval processes

5. **IntelliFin.FinancialService** (port varies)
   - General Ledger operations
   - **Contains**: Audit Trail Controller (see critical gap below)
   - Compliance and reporting functions

6. **IntelliFin.Communications** (port 5218)
   - Multi-channel notifications (SMS, Email, In-App)
   - SignalR for real-time push

7. **IntelliFin.Collections** (port varies)
   - Collections lifecycle management

8. **IntelliFin.PmecService**, **IntelliFin.CreditBureau**, **IntelliFin.GeneralLedger**, etc.
   - Domain-specific services

9. **IntelliFin.Desktop.OfflineCenter** (MAUI desktop app)
   - CEO offline operations
   - Local SQLite with sync

### Current System Administration Functions

**Scattered Across Services** (**CRITICAL GAP**):
- **Identity/Auth**: Centralized in IdentityService
- **Audit Trail**: Implemented in FinancialService (misplaced)
- **Config Management**: Per-service appsettings.json files
- **Secrets**: Vault configured but underutilized
- **Monitoring**: Basic health checks only, no APM

---

## Section 2: Current State Deep Dive

### 2.1 Authentication & Authorization (Identity Service)

#### Current Implementation

**Technology**: ASP.NET Core Identity + JWT Bearer authentication

**ApplicationUser Model** (`apps/IntelliFin.IdentityService/Models/ApplicationUser.cs`):
```csharp
public class ApplicationUser : IdentityUser
{
    public Guid? TenantId { get; set; }           // Multi-tenancy support
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string? BranchId { get; set; }          // Branch scoping (basic)
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    
    // Computed properties
    public bool IsPlatformUser => TenantId == null;
    public bool IsTenantUser => TenantId.HasValue;
}
```

**JWT Configuration** (`appsettings.json`):
```json
{
  "Jwt": {
    "Issuer": "IntelliFin.Identity",
    "Audience": "intellifin-api",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7,
    "RequireHttps": false  // Dev setting
  }
}
```

**Authentication Flow** (as implemented):
1. User submits credentials to IdentityService
2. Service validates via ASP.NET Core Identity
3. JWT access token generated (15-minute expiry)
4. Refresh token generated (7-day expiry)
5. Tokens stored in secure HttpOnly cookies
6. Token validation on each request via JwtBearer middleware
7. Revoked tokens checked against Redis denylist

**Redis Token Revocation**:
```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "KeyPrefix": "intellifin:identity:",
    "TokenDenylistTimeoutMinutes": 60
  }
}
```

**Session Management** (`appsettings.json`):
```json
{
  "Session": {
    "TimeoutMinutes": 30,
    "MaxConcurrentSessions": 3,
    "TrackUserActivity": true,
    "ActivityUpdateIntervalMinutes": 5
  }
}
```

**Account Lockout** (implemented):
```json
{
  "AccountLockout": {
    "MaxFailedAttempts": 5,
    "LockoutDurationMinutes": 15,
    "EnableLockout": true
  }
}
```

#### Current Role-Based Access Control (RBAC)

**Implemented Roles** (per documentation):
- `Loan Officer`: Application processing, customer management
- `Credit Analyst`: Credit decisions up to ZMW 50,000
- `Head of Credit`: High-value approvals, risk grade C/D/F
- `Finance Officer`: Disbursement authorization
- `CEO`: Offline authorization, executive dashboards, high-value dual-control
- `System Administrator`: User/role management, system config
- `Compliance`: Audit access, regulatory reporting (partial)
- `Manager`: Supervisory access (partial)

**Custom Authorization Features** (partially implemented):
- **Permission Catalog Service**: `IPermissionCatalogService` 
- **Role Composition Service**: `IRoleCompositionService`
- **Rule-Based Authorization**: `IRuleEngineService` (for dynamic rules)
- **Tenant Resolver**: `ITenantResolver` (multi-tenancy)

**Branch-Aware Authorization** (basic implementation):
- Users have `BranchId` field
- No formalized Attribute-Based Access Control (ABAC)
- No enforcement in APIs/data queries (manual filtering required)

#### Critical Gaps vs. Enhanced Requirements

| **Requirement**                              | **Current State**                     | **Gap Analysis**                                                                                     |
|----------------------------------------------|---------------------------------------|------------------------------------------------------------------------------------------------------|
| **Self-hosted IdP (Keycloak/OpenIddict)**     | ASP.NET Core Identity (embedded)      | **MAJOR GAP**: Need to extract identity into dedicated IdP with OIDC/OAuth2 standards                |
| **Branch-scoped JWT claims**                  | Manual `BranchId` in user model       | **GAP**: Not in JWT claims, no automatic enforcement at API gateway level                            |
| **Rotating refresh tokens**                   | Static 7-day refresh tokens           | **MAJOR GAP**: No rotation strategy, security risk for long-lived tokens                             |
| **AAD B2C federation (optional)**             | Not implemented                       | **MAJOR GAP**: No external IdP federation capability                                                 |
| **Real operational roles**                    | V1 business roles (6 roles)           | **SIGNIFICANT GAP**: Missing Collections, Treasury, GL, Auditors, Risk, Branch Mgmt roles            |
| **Strict Segregation of Duties (SoD)**        | Basic role separation                 | **GAP**: No formalized SoD enforcement, no conflicting role detection                                |
| **Step-up MFA**                               | Not implemented                       | **MAJOR GAP**: No MFA for sensitive operations                                                       |
| **Just-in-time (JIT) elevation**              | Not implemented                       | **MAJOR GAP**: No temporary privilege elevation                                                      |
| **Camunda-driven approval workflows**         | Workflows exist for loan origination  | **GAP**: Not integrated with identity/access elevation                                               |
| **Formal JML lifecycle**                      | Manual user creation/deactivation     | **GAP**: No joiner/mover/leaver automation                                                           |
| **Quarterly access recertification**          | Not implemented                       | **MAJOR GAP**: No periodic access review process                                                     |
| **Branch-aware ABAC in APIs/data**            | Manual filtering required             | **MAJOR GAP**: No policy enforcement layer, no API-level ABAC                                        |

---

### 2.2 Audit & Compliance

#### Current Implementation

**Primary Audit Controller**: `apps/IntelliFin.FinancialService/Controllers/AuditTrailController.cs`

**CRITICAL ARCHITECTURAL ISSUE**: Audit trail is implemented in FinancialService, not System Administration. This is a **major misplacement** that needs correction in the new Admin microservice.

**Current Audit Features** (as implemented):
- **Query audit events** with filtering/pagination
- **Audit statistics** for date ranges
- **Generate audit reports** (PDF, Excel, JSON, CSV, HTML)
- **Verify audit trail integrity** (basic checksums)
- **Audit dashboard** with metrics

**Audit Service Interface** (`IAuditService`):
```csharp
public interface IAuditService
{
    Task LogEventAsync(AuditEventContext context, CancellationToken ct);
    Task<AuditQueryResult> QueryEventsAsync(AuditQuery query, CancellationToken ct);
    Task<AuditStatistics> GetStatisticsAsync(DateTime start, DateTime end, CancellationToken ct);
    Task<AuditReport> GenerateReportAsync(AuditReportRequest request, CancellationToken ct);
    Task<IntegrityVerificationResult> VerifyIntegrityAsync(DateTime start, DateTime end, CancellationToken ct);
}
```

**Audit Event Context** (current structure):
```csharp
public class AuditEventContext
{
    public string Actor { get; set; }          // User ID
    public string Action { get; set; }         // Action performed
    public string EntityType { get; set; }     // Resource type
    public string EntityId { get; set; }       // Resource ID
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public AuditEventCategory Category { get; set; }
    public AuditEventSeverity Severity { get; set; }
    public Dictionary<string, object> Data { get; set; }
}
```

**Audit Event Categories** (enum):
- `DataAccess`, `DataModification`, `SecurityEvent`, `ReportGeneration`, `ConfigurationChange`, `UserManagement`

**Monitoring Entities** (`libs/IntelliFin.Shared.DomainModels/Entities/MonitoringEntities.cs`):
- `ErrorLog`: Event processing errors with **CorrelationId** (basic)
- `PerformanceLog`: Performance metrics
- `HealthCheckLog`: Health check results

**Current Correlation ID Usage** (limited):
- `ErrorLog.CorrelationId` field exists
- No standardized correlation ID propagation across services
- No request tracing through microservices

#### Critical Gaps vs. Enhanced Requirements

| **Requirement**                              | **Current State**                             | **Gap Analysis**                                                                                     |
|----------------------------------------------|-----------------------------------------------|------------------------------------------------------------------------------------------------------|
| **Tamper-evident audit chain**                | Basic integrity verification (checksums)      | **MAJOR GAP**: No cryptographic chaining (hash of previous record + current)                         |
| **MinIO WORM retention**                      | MinIO used for documents, not audit           | **MAJOR GAP**: Audit logs not stored in WORM-compliant storage                                       |
| **Global correlation IDs**                    | Partial (ErrorLog only)                       | **MAJOR GAP**: No standardized correlation ID across all services, no distributed tracing            |
| **Offline audit merge**                       | Not implemented                               | **MAJOR GAP**: No mechanism to merge offline desktop app audit logs                                  |
| **Centralized audit storage**                 | Per-service database                          | **GAP**: Audit data scattered across service databases                                               |
| **Audit event streaming**                     | Not implemented                               | **GAP**: No real-time audit event streaming for SIEM integration                                     |
| **Compliance reporting automation**           | Manual report generation only                 | **GAP**: No automated BoZ/regulatory report generation                                               |

---

### 2.3 Configuration Management & Secrets

#### Current Configuration Approach

**Per-Service Configuration**:
- Each microservice has `appsettings.json` + `appsettings.Development.json`
- Environment-specific overrides via environment variables
- No centralized configuration service

**Configuration Storage Locations**:
- **Application Config**: `apps/*/appsettings.json` (checked into source control)
- **Infrastructure Config**: `.env` file (development), Kubernetes ConfigMaps (production - assumed)
- **Secrets**: Hardcoded in dev (`appsettings.json`), Vault available but underutilized

**Example Configuration** (`IntelliFin.IdentityService/appsettings.json`):
```json
{
  "Jwt": {
    "SigningKey": "dev-super-secret-signing-key-change-me-please-1234567890"  // ⚠️ Dev only
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

#### Current Secrets Management

**HashiCorp Vault** (configured but underutilized):
- Vault running on port 38200 (per `.env`)
- Documented in `docs/domains/system-administration/infrastructure-and-operations.md`
- **CRITICAL GAP**: Not actively used for runtime secrets

**Documented Vault Usage** (from infrastructure docs):
```
Secrets Management: HashiCorp Vault
- Secure storage of sensitive data
- Dynamic secrets generation
- Access control and auditing
- Integration with applications
- Compliance and audit trails
```

**Secrets Categories** (documented but not implemented):
- Database connection strings
- API keys and tokens
- Certificates and keys
- Configuration secrets
- Integration credentials
- System passwords

#### Critical Gaps vs. Enhanced Requirements

| **Requirement**                              | **Current State**                           | **Gap Analysis**                                                                                     |
|----------------------------------------------|---------------------------------------------|------------------------------------------------------------------------------------------------------|
| **Policy-driven configuration management**    | Manual `appsettings.json` edits             | **MAJOR GAP**: No policy enforcement, no validation before deployment                                |
| **Workflow-approved config changes**          | No approval process                         | **MAJOR GAP**: Sensitive config changes not gated by Camunda workflows                               |
| **Vault-backed secrets with rotation**        | Vault configured, not actively used         | **MAJOR GAP**: Secrets in config files, no automatic rotation                                        |
| **GitOps for config-as-code**                 | Git version control (manual)                | **GAP**: No pull request-based config deployment, no automated deployment validation                 |
| **Signed SBOM-scanned images**                | Docker images built, not signed             | **MAJOR GAP**: No container image signing, no SBOM generation, no vulnerability scanning             |
| **Configuration versioning & rollback**       | Git history only                            | **GAP**: No automated config rollback, no A/B config deployment                                      |
| **Environment-specific secret injection**     | Manual appsettings per environment          | **GAP**: No dynamic secret injection at runtime from Vault                                           |

---

### 2.4 Workflow Orchestration (Camunda Integration)

#### Current Implementation

**Camunda 8 (Zeebe) — Self-Hosted (Decision Locked):**
- Hosting: In-country Kubernetes (Infratel/Paratus), Zeebe + Operate + Tasklist
- Configuration: Service-to-service auth managed via Vault; no SaaS credentials in `.env`
- Usage: Loan origination workflows, PMEC integration workflows; plus access elevation, config approvals, access recertification, and incident response workflows

**Workflow Service** (`apps/IntelliFin.LoanOriginationService/Services/WorkflowService.cs`):
- Connects to Zeebe gateway
- Deploys BPMN process definitions
- Starts process instances
- Handles external tasks

**BPMN Processes** (defined):
- `apps/IntelliFin.LoanOriginationService/BPMN/loan-approval-process.bpmn`
- `apps/IntelliFin.LoanOriginationService/BPMN/risk-grade-decision.dmn`
- PMEC integration workflows

**Current Workflow Use Cases**:
1. **Loan Approval**: Multi-step approval workflows with human tasks
2. **Credit Assessment**: Automated decision making with DMN tables
3. **PMEC Verification**: Retry logic with exponential backoff

**External Task Worker** (`ExternalTaskWorkerService.cs`):
- Long-polling for external tasks from Zeebe
- Executes business logic
- Completes tasks with variables

#### Critical Gaps vs. Enhanced Requirements

| **Requirement**                              | **Current State**                            | **Gap Analysis**                                                                                     |
|----------------------------------------------|----------------------------------------------|------------------------------------------------------------------------------------------------------|
| **JIT elevation approval workflows**          | Workflows for loan origination only          | **MAJOR GAP**: No workflow for access elevation, temporary permission grants                         |
| **Configuration change approval**             | No workflow integration                      | **MAJOR GAP**: Sensitive config changes not routed through Camunda                                   |
| **Step-up authentication workflows**          | No MFA workflows                             | **MAJOR GAP**: No human task workflows for secondary authentication                                  |
| **Access recertification workflows**          | Not implemented                              | **MAJOR GAP**: No quarterly access review workflows with manager approvals                           |
| **Incident response workflows**               | Not implemented                              | **GAP**: No automated incident response playbooks in Camunda                                         |
| **Workflow audit integration**                | Basic workflow logs                          | **GAP**: Workflow decisions not fully integrated with audit trail                                    |

---

### 2.5 Observability & Monitoring

#### Current Implementation

**CRITICAL GAP**: Observability is the **most significant gap** in the current system.

**Current Logging** (Serilog):
- File-based structured logging: `logs/identityservice-.txt`
- Per-service log files
- No centralized log aggregation
- **Example** (`IntelliFin.IdentityService/Program.cs`):
  ```csharp
  Log.Logger = new LoggerConfiguration()
      .WriteTo.Console()
      .WriteTo.File("logs/identityservice-.txt", rollingInterval: RollingInterval.Day)
      .CreateLogger();
  ```

**Current Monitoring**:
- Basic `/health` endpoints per service
- `HealthCheckLog` entity for storing health check results
- No APM (Application Performance Monitoring)
- No distributed tracing
- No metrics collection

**Current Dashboards**:
- None (no Grafana, no Kibana)
- Audit dashboard in AuditTrailController (basic)

**Infrastructure Monitoring** (documented but not implemented):
```
Performance Monitoring: Prometheus + Grafana
Logging Stack: Loki (Grafana Loki)
Alerting System: Automated Alerts
```

#### Critical Gaps vs. Enhanced Requirements

| **Requirement**                              | **Current State**                           | **Gap Analysis**                                                                                     |
|----------------------------------------------|---------------------------------------------|------------------------------------------------------------------------------------------------------|
| **OpenTelemetry instrumentation**             | Not implemented                             | **MAJOR GAP**: No OTEL SDK, no traces/metrics/logs export                                            |
| **Prometheus/Grafana metrics**                | Not implemented                             | **MAJOR GAP**: No time-series metrics, no monitoring dashboards                                      |
|| **Loki centralized logging**                  | File-based logs per service                 | **MAJOR GAP**: No log aggregation, no searchable log index                                           |
|| **Jaeger distributed tracing**                | No tracing                                  | **MAJOR GAP**: No request tracing across microservices, no trace visualization                       |
| **Compliance dashboards**                     | None                                        | **MAJOR GAP**: No automated compliance KPI dashboards (e.g., BoZ metrics)                            |
| **Cost-performance monitoring**               | None                                        | **MAJOR GAP**: No infrastructure cost tracking, no cost allocation per service                       |
| **Alerting & incident response**              | None                                        | **MAJOR GAP**: No automated alerting, no PagerDuty/Opsgenie integration                              |
| **SLA/SLO monitoring**                        | None                                        | **GAP**: No service-level objectives, no SLA enforcement                                             |

---

### 2.6 Infrastructure & Deployment

#### Current Infrastructure

**Deployment Stack** (development):
- **Docker Compose** (`docker-compose.yml`) for local infrastructure:
  - SQL Server (port 31433)
  - RabbitMQ (ports 35672, 15672)
  - Redis (port 36379)
  - MinIO (ports 39000, 39001)
  - Vault (port 38200)

**Production Deployment** (documented):
- **Kubernetes**: Orchestration and scaling
- **Terraform**: Infrastructure as Code
- **GitFlow**: Source control with feature branches
- **GitHub Actions**: CI/CD pipelines
- **Zambian IaaS**: Infratel/Paratus Data Centers (data sovereignty)

**Network Security** (documented):
- Firewalls and load balancers
- TLS 1.3 for data in transit
- **NO mTLS** between services currently

**Container Security** (current state):
- Docker images built per service
- **NOT signed**
- **NO SBOM generation**
- **NO vulnerability scanning** in CI/CD

#### Critical Gaps vs. Enhanced Requirements

| **Requirement**                              | **Current State**                           | **Gap Analysis**                                                                                     |
|----------------------------------------------|---------------------------------------------|------------------------------------------------------------------------------------------------------|
| **mTLS between all services**                 | TLS at edge only                            | **MAJOR GAP**: No mutual TLS, no service-to-service encryption                                       |
| **Network policies (Kubernetes)**             | Basic networking                            | **GAP**: No NetworkPolicy resources, no micro-segmentation                                           |
| **Least-privilege service accounts**          | Default service accounts                    | **GAP**: No RBAC for service accounts, no workload identity                                          |
| **Signed container images**                   | Unsigned Docker images                      | **MAJOR GAP**: No Cosign/Notary signature verification                                               |
| **SBOM-scanned images**                       | No SBOM generation                          | **MAJOR GAP**: No software bill of materials, no CVE scanning                                        |
| **GitOps config deployment**                  | Manual kubectl/helm                         | **GAP**: No ArgoCD/FluxCD, no declarative config sync                                                |
| **DR/backup tested runbooks**                 | Documented, not tested                      | **GAP**: No automated DR testing, no RTO/RPO validation                                              |

---

### 2.7 Privileged Access Management (PAM)

#### Current Implementation

**CRITICAL GAP**: No Privileged Access Management system currently exists.

**Current Admin Access**:
- Direct SSH/RDP to servers (assumed)
- Shared credentials (bad practice)
- No session recording
- No just-in-time access

**System Administrator Role**:
- Permanent elevated permissions
- No time-bound access
- No approval workflow for infrastructure access

#### Critical Gaps vs. Enhanced Requirements

| **Requirement**                              | **Current State**                           | **Gap Analysis**                                                                                     |
|----------------------------------------------|---------------------------------------------|------------------------------------------------------------------------------------------------------|
| **Bastion-based access**                      | Not implemented                             | **MAJOR GAP**: No jump host, direct server access                                                    |
| **JIT infrastructure access**                 | Permanent access                            | **MAJOR GAP**: No temporary credential generation, no time-limited access                            |
| **Session recording**                         | Not implemented                             | **MAJOR GAP**: No audit trail of admin sessions, no compliance evidence                              |
| **Approval workflows for access**             | Not implemented                             | **MAJOR GAP**: No Camunda workflow for privileged access requests                                    |
| **Break-glass procedures**                    | Not implemented                             | **GAP**: No emergency access process with post-incident review                                       |

---

## Section 3: Technical Debt & Known Issues

### 3.1 Critical Technical Debt

1. **Audit Trail Architectural Misplacement**
   - **Issue**: Audit controller in FinancialService instead of System Administration
   - **Impact**: Violates domain boundaries, confuses responsibilities
   - **Remediation**: Move to new Admin microservice

2. **Vault Underutilization**
   - **Issue**: Vault configured but secrets still in `appsettings.json`
   - **Impact**: Security risk, secrets in source control
   - **Remediation**: Migrate all secrets to Vault with dynamic injection

3. **No Centralized Configuration Management**
   - **Issue**: Per-service config files, no central management
   - **Impact**: Config drift, inconsistent settings across services
   - **Remediation**: Implement configuration service or Kubernetes ConfigMaps with GitOps

4. **No Observability Stack**
   - **Issue**: File-based logging, no metrics, no tracing
   - **Impact**: Cannot diagnose production issues, no SLA monitoring
- **Remediation**: Full OpenTelemetry + Prometheus + Grafana + Loki stack

5. **No mTLS or Zero-Trust Networking**
   - **Issue**: Services communicate over plain HTTP (or HTTPS without mutual auth)
   - **Impact**: Vulnerable to man-in-the-middle, lateral movement attacks
   - **Remediation**: Implement service mesh (Istio/Linkerd) or manual mTLS

6. **Embedded Identity Provider**
   - **Issue**: ASP.NET Core Identity tightly coupled to IdentityService
   - **Impact**: Cannot scale identity, no external IdP integration
   - **Remediation**: Extract to Keycloak/OpenIddict as self-hosted IdP

7. **No Correlation ID Propagation**
   - **Issue**: Correlation IDs exist in error logs but not propagated
   - **Impact**: Cannot trace requests across microservices
   - **Remediation**: Implement W3C Trace Context standard

### 3.2 Workarounds & Gotchas

**Development Environment**:
- **Issue**: Port conflicts with non-standard ports (e.g., SQL Server on 31433)
- **Workaround**: Use `docker-compose.override.yml` for local customizations

**JWT Signing Key**:
- **Issue**: Dev key hardcoded in `appsettings.json`
- **Gotcha**: Must change for production, stored in plain text
- **Workaround**: Use Vault in production (not yet implemented)

**Camunda Workflow (Self-Hosted Decision)**:
- **Issue**: Remove any SaaS credentials from `.env`
- **Action**: Deploy in-country Camunda 8; manage credentials via Vault; ensure offline-capable workflows
- **Note**: All workflow data remains in Zambia

**Branch Scoping**:
- **Issue**: `BranchId` in user model but no API enforcement
- **Gotcha**: Developers must manually filter queries by branch
- **Workaround**: Custom middleware needed (not yet implemented)

**Offline Desktop App Sync**:
- **Issue**: Audit logs from offline app not merged with central audit
- **Gotcha**: Compliance gap for offline operations
- **Workaround**: Manual audit reconciliation (not formalized)

---

## Section 4: Integration Points & Dependencies

### 4.1 Internal Service Dependencies

**IdentityService Dependencies**:
- **SQL Server**: User/role/permission storage
- **Redis**: Token revocation denylist, session cache
- **Vault**: (planned) Secret storage

**FinancialService Dependencies** (Audit Trail):
- **SQL Server**: Audit event storage
- **All Services**: Audit event publishers

**LoanOriginationService Dependencies**:
- **Camunda 8 (self-hosted)**: Workflow orchestration
- **RabbitMQ**: Event publishing

**KycDocumentService Dependencies**:
- **MinIO**: Document storage (S3-compatible API)

### 4.2 External Integration Points

**PMEC (Government Payroll)**:
- Integration via `IntelliFin.PmecService`
- Retry logic with Camunda workflows

**TransUnion Zambia (Credit Bureau)**:
- Integration via `IntelliFin.CreditBureau`
- Cost optimization

**Tingg Payment Gateway**:
- Mobile money collections/disbursements

**SMS Gateway (Africa's Talking)**:
- Customer notifications

### 4.3 Infrastructure Dependencies

**Kubernetes (Production)**:
- All microservices deployed as pods
- No NetworkPolicies currently
- No mTLS enforcement

**Docker Compose (Development)**:
- Local infrastructure services
- Port mapping to non-standard ports

**Zambian Data Centers (Infratel/Paratus)**:
- Data sovereignty compliance
- Primary + secondary for DR

---

## Section 5: Enhancement Impact Analysis

### 5.1 New Components to Be Created

1. **IntelliFin.AdminService** (NEW microservice)
   - **Purpose**: Centralized control plane for identity, access, policy, audit
   - **Responsibilities**:
     - Orchestrate identity operations
     - Enforce policy-driven configuration changes
     - Centralize audit log collection
     - Manage workflow approvals for access
   - **Tech Stack**: ASP.NET Core 9, entity controllers for admin UI
- **Dependencies**: Keycloak, Camunda, Vault, MinIO (WORM audit)

2. **Keycloak (Self-Hosted IdP)**
   - **Purpose**: Replace ASP.NET Core Identity with standards-based IdP
   - **Features**:
     - OIDC/OAuth2 provider
     - Federation with AAD B2C (optional)
     - Step-up MFA
     - Branch-scoped JWT claims
   - **Deployment**: Kubernetes pod, high availability

3. **OpenTelemetry Collector**
   - **Purpose**: Centralized telemetry collection for traces, metrics, logs
   - **Deployment**: Sidecar or DaemonSet in Kubernetes

4. **Prometheus + Grafana**
   - **Purpose**: Metrics storage and visualization
   - **Dashboards**: Compliance metrics, cost-performance, SLA monitoring

5. **Loki**
   - **Purpose**: Centralized log aggregation and search
   - **Integration**: OpenTelemetry log exporter

6. **Jaeger**
   - **Purpose**: Distributed trace storage and visualization
   - **Integration**: OpenTelemetry trace exporter

7. **Bastion Host + PAM Solution**
   - **Purpose**: Privileged access management
   - **Options**: Teleport (selected)
   - **Features**: JIT access, session recording, audit trail

### 5.2 Files/Modules to Be Modified

**IdentityService (Major Refactoring)**:
- Extract ASP.NET Core Identity, integrate with Keycloak
- Add rotating refresh token logic
- Implement branch-scoped JWT claims
- Add AAD B2C federation configuration
- Integrate with Camunda for step-up MFA workflows

**All Microservices (Observability Instrumentation)**:
- Add OpenTelemetry SDK packages
- Instrument controllers, services, data access
- Propagate correlation IDs (W3C Trace Context)
- Add Prometheus metrics endpoints
- Configure OTEL exporters

**API Gateway (Enhanced Authorization, YARP)**:
- Enforce ABAC at the edge (branch/scope default-deny)
- Branch-aware request filtering
- JWT claim extraction and correlation ID propagation

**Audit Service (Enhanced & Moved)**:
- Move from FinancialService to AdminService
- Add tamper-evident chain (hash linking)
- Implement MinIO WORM storage for audit logs
- Add offline audit merge capability
- Global correlation ID integration

**Docker Compose & Kubernetes Manifests**:
- Add Keycloak service
- Add OpenTelemetry Collector
- Add Prometheus, Grafana, Loki, Jaeger
- Add NetworkPolicy resources
- Add mTLS sidecar configuration (Linkerd)

**CI/CD Pipelines (GitHub Actions)**:
- Add container image signing (Cosign)
- Add SBOM generation (Syft/SPDX)
- Add vulnerability scanning (Trivy)
- Add GitOps deployment (ArgoCD sync)

### 5.3 Integration Considerations

**Keycloak Integration**:
- **Migration Path**: Export users from ASP.NET Core Identity to Keycloak
- **Token Compatibility**: Ensure JWT claims match existing structure
- **Backward Compatibility**: Support both old and new tokens during migration

**Camunda Workflow Integration**:
- **New BPMN Processes**:
  - `access-elevation-approval.bpmn`: JIT permission requests
  - `config-change-approval.bpmn`: Sensitive configuration changes
  - `access-recertification.bpmn`: Quarterly access reviews
  - `incident-response.bpmn`: Security incident workflows

**MinIO WORM Integration**:
- **Configuration**: Enable object lock (WORM mode)
- **Retention Policy**: 10-year retention (client requirement)
- **Audit Log Format**: Immutable JSON lines (JSONL) with hash chains

**OpenTelemetry Integration**:
- **Exporter Configuration**: OTLP (gRPC) to OTEL Collector
- **Trace Sampling**: Adaptive sampling (100% for errors, 10% for normal)
- **Metric Cardinality**: Careful label design to avoid metric explosion

**mTLS Integration**:
- **Certificate Management**: Vault PKI engine or cert-manager (Kubernetes)
- **Service Mesh Option**: Linkerd (selected) for automatic mTLS
- **Manual mTLS**: HttpClient certificate authentication (simpler)

---

## Section 6: Development & Deployment

### 6.1 Local Development Setup

**Current Setup** (working):
```bash
# 1. Start infrastructure
docker-compose up -d

# 2. Verify services
docker-compose ps

# 3. Run identity service
dotnet run --project apps/IntelliFin.IdentityService

# 4. Run other services (multiple terminals)
dotnet run --project apps/IntelliFin.ApiGateway
dotnet run --project apps/IntelliFin.ClientManagement
# ... etc
```

**Enhanced Setup** (required for control plane):
```bash
# 1. Start enhanced infrastructure (docker-compose v2)
docker-compose -f docker-compose.yml -f docker-compose.observability.yml up -d

# Services now include:
# - SQL Server, Redis, RabbitMQ, MinIO, Vault (existing)
# - Keycloak (NEW)
# - OpenTelemetry Collector (NEW)
# - Prometheus, Grafana (NEW)
# - Loki, Jaeger (NEW)

# 2. Initialize Keycloak (one-time)
./scripts/init-keycloak.sh

# 3. Run Admin microservice (NEW)
dotnet run --project apps/IntelliFin.AdminService

# 4. Run identity service (refactored)
dotnet run --project apps/IntelliFin.IdentityService

# 5. Access dashboards
# Grafana: http://localhost:3000
# Jaeger: http://localhost:16686
# Keycloak Admin: http://localhost:8080
```

### 6.2 Build & Deployment Process

**Current Process**:
- Manual `dotnet build`
- Docker image build per service
- Manual deployment (assumed)

**Enhanced Process** (GitOps):
1. **Code Commit** → GitHub
2. **CI Pipeline** (GitHub Actions):
   - Build .NET solution
   - Run unit/integration tests
   - Build Docker images
   - **NEW**: Sign images with Cosign
   - **NEW**: Generate SBOM
   - **NEW**: Scan for vulnerabilities (Trivy)
   - Push to container registry
3. **CD Pipeline**:
   - **NEW**: Update Git repo with new image tags
   - **NEW**: ArgoCD detects changes and syncs to Kubernetes
4. **Post-Deployment**:
   - **NEW**: Verify deployment with Grafana dashboards
   - **NEW**: Check Loki logs for errors
   - **NEW**: Alert on deployment anomalies

### 6.3 Configuration Management

**Current**: Manual `appsettings.json` edits, environment variables

**Enhanced (Policy-Driven)**:
```yaml
# Example: config-policy.yaml (stored in Git)
policies:
  - name: jwt-token-expiry
    value: 15  # minutes
    requires_approval: true
    approval_workflow: config-change-approval
    
  - name: max-failed-login-attempts
    value: 5
    requires_approval: false
    
- name: audit-retention-days
    value: 3650  # 10 years (client requirement)
    requires_approval: true
    approval_workflow: config-change-approval
```

**Process**:
1. Developer creates PR with config change
2. **NEW**: Automated validation against policy schema
3. **NEW**: If `requires_approval: true`, trigger Camunda workflow
4. Manager approves in Camunda
5. PR merged
6. ArgoCD deploys new config to Kubernetes ConfigMap
7. Services reload config (via ConfigMap watch)

---

## Section 7: Testing & Quality Assurance

### 7.1 Current Testing

**Testing Coverage**:
- Unit tests: xUnit (partial coverage)
- Integration tests: Minimal
- E2E tests: None
- Performance tests: None

### 7.2 Enhanced Testing Requirements

**New Test Categories**:
1. **Security Tests**:
   - mTLS handshake tests
   - JWT claim validation tests
   - ABAC policy enforcement tests
   - Audit tamper-evident chain tests

2. **Observability Tests**:
   - Verify correlation ID propagation
   - Trace sampling validation
   - Metric cardinality checks

3. **Workflow Tests**:
   - JIT access approval workflow E2E
   - Config change approval workflow
   - Access recertification workflow

4. **DR/Backup Tests**:
   - Automated DR failover tests
   - RPO/RTO validation tests
   - Audit log integrity after recovery

---

## Section 8: Migration Strategy

### 8.1 Phased Approach

**Phase 1: Foundation (Months 1-2)**
- Deploy Keycloak/OpenIddict
- Migrate users from ASP.NET Core Identity
- Implement OpenTelemetry instrumentation
- Deploy Prometheus + Grafana

**Phase 2: Enhanced Security (Months 3-4)**
- Implement rotating refresh tokens
- Add branch-scoped JWT claims
- Implement mTLS between services
- Deploy Loki for centralized logging

**Phase 3: Audit & Compliance (Months 5-6)**
- Create AdminService microservice
- Move audit trail to AdminService
- Implement tamper-evident audit chain
- Configure MinIO WORM for audit logs
- Implement offline audit merge

**Phase 4: Governance & Workflows (Months 7-8)**
- Implement JIT elevation with Camunda
- Add config change approval workflows
- Implement access recertification workflows
- Add step-up MFA

**Phase 5: Zero-Trust & PAM (Months 9-10)**
- Implement NetworkPolicies
- Deploy bastion host + PAM
- Implement JIT infrastructure access
- Add session recording

**Phase 6: Advanced Observability (Months 11-12)**
- Deploy Jaeger for tracing
- Build compliance dashboards
- Build cost-performance dashboards
- Implement automated alerting

### 8.2 Backward Compatibility

**Identity Migration**:
- Run Keycloak alongside ASP.NET Core Identity
- Support both token formats during transition
- Gradual user migration (no forced re-login)

**Audit Trail**:
- Keep existing audit data in FinancialService (read-only)
- New audit events to AdminService + MinIO WORM
- Unified query API in AdminService

**Configuration**:
- Support both `appsettings.json` and policy-driven config
- Gradual migration of config keys to GitOps repo

---

## Section 9: Compliance & Governance

### 9.1 Regulatory Requirements

**Bank of Zambia (BoZ)**:
- Audit retention: 10 years (3650+ days) ✅ Decision locked, ⚠️ Not enforced
- Data sovereignty: In-country hosting ✅ Implemented (Infratel/Paratus)
- Loan classification: Automated ✅ Implemented

**Money Lenders Act (Zambia)**:
- Interest rate cap: 48% EAR ✅ Implemented
- Customer data protection: TLS encryption ✅ Implemented
- Access control: RBAC ✅ Basic implementation

### 9.2 Compliance Gaps

**Audit Retention Enforcement**:
- **Current**: Documented, not technically enforced
- **Enhanced**: MinIO WORM with 10-year lock

**Access Recertification**:
- **Current**: Manual, ad-hoc
- **Enhanced**: Quarterly Camunda workflows with manager approval

**Incident Response**:
- **Current**: No formal process
- **Enhanced**: Automated incident workflows, evidence capture

---

## Appendix: Useful Commands

### Development Commands

```bash
# Build entire solution
dotnet build IntelliFin.sln -c Release

# Run specific service
dotnet run --project apps/IntelliFin.IdentityService

# Database migrations
dotnet ef database update -p libs/IntelliFin.Shared.DomainModels

# Docker Compose
docker-compose up -d            # Start all infrastructure
docker-compose logs sqlserver   # View SQL Server logs
docker-compose down -v          # Stop and remove volumes
```

### Observability Commands (Future)

```bash
# View traces in Jaeger
open http://localhost:16686

# Query logs in Loki
logcli query '{service="identityservice"}' --since=1h

# Prometheus metrics
curl http://localhost:9090/api/v1/query?query=up

# Grafana dashboards
open http://localhost:3000
```

### Debugging Commands

```bash
# Check Redis token denylist
redis-cli -p 36379
> KEYS intellifin:identity:denylist:*

# Check MinIO buckets
mc alias set local http://localhost:39000 minioadmin minioadmin
mc ls local/

# Check Vault secrets (if enabled)
vault kv list secret/intellifin
```

---

## Document Summary

This brownfield architecture document has analyzed the **current state** of IntelliFin's System Administration domain and identified **significant gaps** against the enhanced "control plane" requirements:

**Key Findings**:
1. ✅ **Strong Foundation**: Microservices architecture, Camunda workflows, basic RBAC
2. ⚠️ **Major Gaps**: Observability (no APM/tracing), no mTLS, no PAM, no policy-driven config
3. ⚠️ **Audit Misplacement**: Audit trail in wrong service (FinancialService vs. AdminService)
4. ⚠️ **Secrets Management**: Vault underutilized, secrets in config files
5. ⚠️ **Identity Provider**: Embedded ASP.NET Core Identity, needs extraction to Keycloak/OpenIddict

**Next Steps**:
1. Create detailed brownfield PRD for control plane enhancement
2. Design Admin microservice architecture
3. Plan Keycloak/OpenIddict migration
4. Design observability stack deployment
5. Define compliance dashboards and SLOs

---

**Document Status**: Complete  
**Next Artifact**: Brownfield PRD for System Administration Control Plane Enhancement
