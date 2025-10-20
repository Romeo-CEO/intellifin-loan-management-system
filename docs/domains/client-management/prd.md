# Client Management Compliance Engine - Product Requirements Document

## Document Control

### Change Log

| Change | Date | Version | Description | Author |
|--------|------|---------|-------------|--------|
| Initial PRD Creation | 2025-10-16 | 1.0 | Created from brownfield architecture analysis | PM John |

---

## 1. Intro Project Analysis and Context

### 1.1 Analysis Source

✅ **Document-project output available at:** `docs\domains\client-management\brownfield-architecture.md`

### 1.2 Current Project State

**Extracted from Brownfield Architecture:**

The Client Management module (`apps\IntelliFin.ClientManagement\`) currently exists as a **minimal ASP.NET Core 9 service** with only:
- Basic web application scaffold (Program.cs, appsettings.json)
- OpenTelemetry observability integration  
- Health check endpoint
- **NO business logic, data models, or domain services yet implemented**

Related services handling client-adjacent concerns:
- **IntelliFin.KycDocumentService**: Separate service for document upload/verification (already has MinIO integration)
- **IntelliFin.AdminService**: Centralized audit logging and compliance reporting
- **IntelliFin.Communications**: Multi-channel notification service (SMS, Email, In-App)

### 1.3 Available Documentation Analysis

**Note:** Document-project analysis available - using existing technical documentation

**Key documents from analysis:**
- ✅ Brownfield Architecture Document (comprehensive)
- ✅ Tech Stack Documentation (SQL Server, MinIO, Camunda, Vault, RabbitMQ)
- ✅ Source Tree/Architecture
- ✅ Data Models (7 core entities defined)
- ✅ API Integration Patterns
- ✅ External Service Documentation (AdminService, Communications, KycDocumentService)
- ✅ Technical Debt Documentation (5 key constraints identified)
- ✅ Deployment and Operations guide

### 1.4 Enhancement Scope Definition

**Enhancement Type:** ☑ **Major Feature Addition** + **Integration with New Systems**

**Enhancement Description:**

Transform the Client Management module from a minimal scaffold into a comprehensive **compliance engine** that serves as the single source of truth for all customer data. This enhancement adds unified customer records with full version history (SCD-2), MinIO-based document lifecycle management with dual-control verification, Camunda-orchestrated KYC/AML workflows, Vault-based risk profiling, consent-based communications, and BoZ 7-year retention compliance—all integrated with existing AdminService audit trails.

**Impact Assessment:** ☑ **Major Impact (architectural changes required)**

- New database schema (7 core entities)
- Camunda workflow engine integration (3 BPMNs)
- Vault secrets management integration
- MinIO document storage with Object Lock
- Integration with 3 existing services
- Migration path for KycDocumentService functionality

### 1.5 Goals and Background Context

**Goals:**
- Establish Client Management as the single source of truth for customer data across all branches
- Implement full audit trail with temporal versioning for regulatory compliance
- Automate KYC/AML verification workflows with dual-control enforcement
- Enable dynamic risk scoring using Vault-managed business rules
- Enforce BoZ 7-year document retention with WORM storage
- Support consent-based customer communication preferences
- Future-proof for PMEC integration with reserved fields

**Background Context:**

The Limelight Moneylink Services LMS currently has basic client documentation (profile management, KYC/AML concepts, communication workflows) but lacks a technical implementation. The existing Client Management service is a minimal scaffold awaiting business logic. Separately, the KycDocumentService handles document storage, creating split responsibilities. 

This enhancement is needed to transform Client Management into a true compliance engine that can pass Bank of Zambia inspections, support multi-branch unified customer records, provide complete audit trails for every customer interaction, and enable automated workflow orchestration for regulatory processes. The system must handle the reality of Zambian financial services: government payroll deductions via PMEC, sanctions screening, PEP checks, and strict document retention requirements—all while maintaining performance and usability for loan officers in multiple branches.

---

## 2. Requirements

### 2.1 Functional Requirements

**FR1: Unified Customer Records**

The system shall maintain a single customer profile per NRC across all branches, preventing duplicate customer records and enabling cross-branch access with proper audit attribution.

**FR2: Temporal Versioning (SCD-2)**

The system shall create a full snapshot version record in the ClientVersion table for every customer profile change, with ValidFrom/ValidTo timestamps, change summary JSON, and IsCurrent flag, enabling point-in-time historical queries.

**FR3: Document Upload and Metadata Storage**

The system shall store KYC documents in MinIO and maintain SQL metadata (ClientDocument table) including ObjectKey, FileHashSha256, UploadedBy, UploadedAt, ContentType, and RetentionUntil fields for each document.

**FR4: Dual-Control Document Verification**

The system shall enforce dual-control verification via Camunda workflows where one officer uploads a document and a different officer (VerifiedBy != UploadedBy) must verify it, with database triggers preventing self-verification.

**FR5: MinIO Object Lock Retention**

The system shall apply MinIO Object Lock in COMPLIANCE mode with 7-year retention on document upload, preventing deletion until RetentionUntil expires, satisfying BoZ regulatory requirements.

**FR6: KYC Workflow Orchestration**

The system shall trigger the `client_kyc_v1.bpmn` Camunda workflow on client creation, executing document completeness checks, AML screening, risk assessment, and human KYC officer review tasks.

**FR7: Enhanced Due Diligence (EDD) Escalation**

The system shall trigger the `client_edd_v1.bpmn` workflow when AML screening detects sanctions/PEP matches, risk scores exceed High threshold, or OCR confidence < 60%, requiring sequential Compliance Officer and CEO approvals.

**FR8: AML Sanctions and PEP Screening**

The system shall perform AML screening against sanctions lists and PEP databases (initially manual, API-ready), recording results in the AmlScreening table with ScreeningType, IsMatch, MatchDetails, and RiskLevel.

**FR9: Vault-Based Risk Scoring**

The system shall retrieve risk scoring rules from Vault path `intellifin/client-management/risk-scoring-rules`, execute JSONLogic/CEL rules against client factors (KYC completeness, AML risk, PEP status), and store computed RiskProfile with rules version traceability.

**FR10: Hot-Reload Risk Configuration**

The system shall poll Vault every 60 seconds for config changes, detect version/checksum updates, hot-reload risk scoring rules without service restart, and log RiskConfigUpdatedEvent to AdminService.

**FR11: Communication Consent Management**

The system shall maintain customer communication preferences in the CommunicationConsent table with ConsentType (Marketing, Operational, Regulatory), channel flags (SMS, Email, InApp, Call), and consent lifecycle tracking (ConsentGivenAt, ConsentRevokedAt).

**FR12: Consent-Based Notifications**

The system shall check CommunicationConsent before sending notifications via CommunicationsService, filtering by ConsentType='Operational' for KYC-related messages and respecting channel preferences.

**FR13: AdminService Audit Integration**

The system shall log every compliance action (client created/updated, document uploaded/verified, KYC status change, AML screening, EDD escalation, risk profile computed) to AdminService via HTTP POST with Actor, Action, EntityType, EntityId, CorrelationId, and EventData.

**FR14: PMEC Reserved Fields**

The system shall include reserved fields in the Client entity (PayrollNumber, Ministry, EmployerType, EmploymentStatus) and publish domain events (ClientPayrollLinkedEvent, ClientPayrollUpdatedEvent) to RabbitMQ exchange `client.events` for future PMEC module consumption.

**FR15: Document Expiry Monitoring**

The system shall track document expiry dates (ExpiryDate field in ClientDocument) and trigger automated expiry warning notifications 30 days before expiration via CommunicationsService.

**FR16: KycDocumentService Migration**

The system shall initially consume KycDocumentService via HTTP client for MinIO operations (Phase 1), then integrate DocumentLifecycleService internally (Phase 2), with feature flags to toggle between services during migration.

### 2.2 Non-Functional Requirements

**NFR1: Database Performance**

The system shall maintain sub-second query performance for current client record retrieval and < 2 second performance for temporal queries using indexed ValidFrom/ValidTo columns on ClientVersion table.

**NFR2: Camunda Worker Throughput**

Camunda workers shall process KYC verification tasks with < 5 minute end-to-end latency from client creation to KYC officer human task assignment, using long-polling with exponential backoff retry patterns.

**NFR3: Vault Configuration Latency**

Risk scoring config hot-reload shall detect Vault changes within 60 seconds and apply new rules without service downtime, with < 100ms overhead per risk score computation.

**NFR4: MinIO Document Upload**

Document upload to MinIO shall complete within 10 seconds for files up to 10MB, with SHA256 hash computation and metadata persistence in a single transaction.

**NFR5: Audit Event Throughput**

The system shall batch audit events and send to AdminService every 5 seconds or when buffer reaches 100 events, whichever occurs first, with < 1% event loss tolerance.

**NFR6: Service Availability**

The Client Management service shall achieve 99.5% uptime with health checks on /health, /health/db, /health/minio, /health/camunda endpoints and readiness probes with 30s delay.

**NFR7: Horizontal Scalability**

The system shall support horizontal pod autoscaling (HPA) from 2 to 10 replicas based on CPU > 70% threshold, with stateless service design and externalized session state.

**NFR8: Data Security**

All client PII shall be protected with TLS 1.3 in transit, SQL Server TDE at rest, MinIO SSE-S3 encryption, and JWT bearer token authentication with claims-based authorization.

**NFR9: Observability**

The system shall emit OpenTelemetry traces to Application Insights, structured logs to ELK stack via Serilog, and custom metrics for KYC workflow completion rate, EDD escalation rate, and risk score distribution.

**NFR10: Testing Coverage**

The enhancement shall achieve 90% code coverage for service layer logic, 80% for Camunda workers, and 100% for domain entities, with integration tests using TestContainers for SQL Server and MinIO.

### 2.3 Compatibility Requirements

**CR1: KycDocumentService HTTP API Compatibility**

The system shall consume existing KycDocumentService endpoints (POST /documents, GET /documents/{id}) without breaking changes during Phase 1 migration, maintaining identical request/response schemas.

**CR2: AdminService Audit Event Schema**

Audit events sent to AdminService shall conform to existing AuditEventDto schema (Actor, Action, EntityType, EntityId, CorrelationId, IpAddress, EventData) with backward compatibility for chain integrity verification.

**CR3: CommunicationsService Notification Contract**

Notifications sent to CommunicationsService shall use existing SendNotificationRequest schema with TemplateId, RecipientId, Channel, and PersonalizationData fields matching current contract.

**CR4: SQL Server Database Isolation**

Client Management shall use dedicated database `IntelliFin.ClientManagement` with separate connection pool, not sharing schema with other services, to enable independent scaling and migration.

**CR5: RabbitMQ Event Schema Versioning**

Domain events published to RabbitMQ exchange `client.events` shall include schema version in message headers and maintain backward compatibility for at least 2 versions to allow gradual consumer migration.

**CR6: Shared Library Version Alignment**

The service shall reference shared libraries (IntelliFin.Shared.Observability, IntelliFin.Shared.Audit, IntelliFin.Shared.Authentication) at versions compatible with other services to ensure consistent authentication and logging behavior.

**CR7: Camunda Topic Naming Convention**

Camunda worker topics shall follow Control-Plane pattern `client.{process}.{taskName}` (e.g., `client.kyc.verify-documents`) to align with AdminService conventions for topic naming, correlation IDs, and error handling.

---

## 3. User Interface Enhancement Goals

**[SKIPPED - Not applicable]**

This enhancement is backend-focused (service, database, workflows, integrations) with no user interface changes. UI interactions will occur through existing frontend applications that consume the Client Management API.

---

## 4. Technical Constraints and Integration Requirements

### 4.1 Existing Technology Stack

**From Brownfield Architecture Document:**

| Category | Technology | Version | Constraints/Notes |
|----------|------------|---------|-------------------|
| **Runtime** | .NET | 9.0 | C# 12, nullable enabled, implicit usings |
| **Web Framework** | ASP.NET Core | 9.0 | Minimal APIs pattern |
| **Database** | SQL Server | 2022 | Always On with primary/read replica |
| **ORM** | Entity Framework Core | 9.0 | To be added - not currently configured |
| **Document Storage** | MinIO | RELEASE.2024-01-16 | S3-compatible, Object Lock required |
| **Workflow Engine** | Camunda 8 (Zeebe) | 8.4+ | Zeebe .NET Client 2.6+ |
| **Secrets Management** | HashiCorp Vault | 1.15+ | KV v2 engine, VaultSharp client |
| **Message Queue** | RabbitMQ | 3.12+ | MassTransit 8.2+ for event publishing |
| **Caching** | Redis | 7.2+ | Not used in Client Management initially |
| **Observability** | OpenTelemetry + Serilog | Latest | Already configured via Shared.Observability |
| **Validation** | FluentValidation | 11.9+ | To be added for input validation |
| **HTTP Clients** | Refit | 7.0+ | For AdminService, Communications, KycDocumentService |

**Key Dependencies Already Present:**
- `IntelliFin.Shared.Observability` (Serilog + OTEL)
- `Microsoft.AspNetCore.OpenApi` (9.0.7)

**Key Dependencies to Add:**
- Entity Framework Core 9.0 (Microsoft.EntityFrameworkCore.SqlServer)
- Zeebe .NET Client 2.6+
- VaultSharp 1.15+
- MassTransit.RabbitMQ 8.2+
- Minio.NET 6.0+ (reuse pattern from KycDocumentService)
- Refit 7.0+
- FluentValidation 11.9+

### 4.2 Integration Approach

#### Database Integration Strategy

- **New Database:** `IntelliFin.ClientManagement` (isolated from other services)
- **Connection Pattern:** Connection string from Vault (`intellifin/db-passwords/client-svc`)
- **Migration Strategy:** EF Core code-first migrations with SQL script generation for production deployments
- **Transaction Scope:** Use distributed transactions (TransactionScope) only when coordinating ClientVersion + ClientDocument writes; otherwise rely on eventual consistency via domain events
- **Indexing Strategy:**
  - Primary: Clustered index on `Id` (GUID, sequential NEWSEQUENTIALID())
  - Unique: `Nrc`, `PayrollNumber` (when not null)
  - Temporal: Composite index on `(ClientId, ValidFrom, ValidTo)` for ClientVersion
  - Current lookup: Index on `(ClientId, IsCurrent)` for fast current version queries

#### API Integration Strategy

- **REST APIs:** ASP.NET Core Minimal APIs with OpenAPI documentation
- **Authentication:** JWT bearer tokens from IdentityService with claims-based authorization
- **Endpoints:**
  - `POST /api/clients` - Create client (triggers KYC workflow)
  - `GET /api/clients/{id}` - Get current client record
  - `GET /api/clients/{id}/versions` - Get version history
  - `GET /api/clients/{id}/versions/at/{timestamp}` - Point-in-time query
  - `POST /api/clients/{id}/documents` - Upload document (calls KycDocumentService Phase 1)
  - `PUT /api/clients/{id}/documents/{docId}/verify` - Verify document (dual-control)
  - `GET /api/clients/{id}/kyc-status` - Get KYC compliance status
  - `GET /api/clients/{id}/risk-profile` - Get current risk assessment
  - `PUT /api/clients/{id}/consents` - Update communication preferences
- **External Service Calls:**
  - AdminService: POST /api/audit/events (Refit client)
  - CommunicationsService: POST /api/communications/send (Refit client)
  - KycDocumentService: POST /documents, GET /documents/{id} (Phase 1 only)

#### Frontend Integration Strategy

N/A - This is a backend service. Frontend applications (Next.js web app, Electron CEO desktop) will consume these APIs but are out of scope for this PRD.

#### Testing Integration Strategy

- **Unit Tests:** xUnit with Moq for repository/service mocks, NSubstitute for complex interfaces
- **Integration Tests:** TestContainers for SQL Server and MinIO, WireMock for external service mocks
- **Workflow Tests:** Camunda Test SDK with mocked Zeebe gateway for BPMN validation
- **E2E Tests:** Not in scope for service layer - handled by separate QA automation
- **Test Database:** Separate `IntelliFin.ClientManagement.Test` database, migrations applied in test setup
- **Coverage Targets:** 90% services, 80% workers, 100% domain entities

### 4.3 Code Organization and Standards

#### File Structure Approach

Following Clean Architecture / DDD layering:

```
apps/IntelliFin.ClientManagement/
├── Controllers/              # API endpoints (thin layer)
├── Domain/
│   ├── Entities/            # Core domain models (Client, ClientVersion, etc.)
│   ├── Events/              # Domain events for RabbitMQ
│   └── ValueObjects/        # DDD value objects (Nrc, PayrollNumber)
├── Services/                # Business logic services
│   ├── ClientService.cs
│   ├── ClientVersioningService.cs
│   ├── DocumentLifecycleService.cs
│   ├── KycWorkflowService.cs
│   ├── RiskScoringService.cs
│   └── ConsentManagementService.cs
├── Workflows/
│   ├── CamundaWorkers/      # Zeebe job workers
│   └── WorkflowModels/      # Workflow context DTOs
├── Infrastructure/
│   ├── Persistence/         # EF Core DbContext, configurations
│   │   ├── ClientManagementDbContext.cs
│   │   ├── Configurations/ # Fluent API entity configs
│   │   └── Migrations/      # EF Core migrations
│   └── VaultClient/         # Vault integration
├── Integration/             # HTTP clients for external services
│   ├── AdminServiceClient.cs
│   ├── CommunicationsClient.cs
│   └── KycDocumentServiceClient.cs
├── Extensions/              # DI registration extensions
│   ├── ServiceCollectionExtensions.cs
│   └── ApplicationBuilderExtensions.cs
└── Middleware/              # Custom middleware (correlation ID, audit)
```

#### Naming Conventions

- **Entities:** PascalCase singular (Client, not Clients)
- **Services:** Interface + Implementation pattern (IClientService, ClientService)
- **Repositories:** Not used - EF Core DbContext provides repository abstraction
- **DTOs:** Suffix with Request/Response/Dto (CreateClientRequest, ClientResponse)
- **Events:** Suffix with Event (ClientCreatedEvent)
- **Workers:** Suffix with Worker (KycVerificationWorker)
- **Database Tables:** PascalCase plural matches entity name (Clients, ClientVersions)
- **API Routes:** kebab-case (/api/clients/{id}/kyc-status)

#### Coding Standards

- Follow existing `IntelliFin.Shared.` patterns from other services
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Async/await for all I/O operations
- Use Result<T> pattern for operation outcomes (from Shared.Infrastructure if available)
- Global exception handling via middleware
- FluentValidation for input validation
- Structured logging with Serilog (correlation IDs on all log entries)

#### Documentation Standards

- XML comments on all public APIs
- OpenAPI/Swagger documentation auto-generated
- README.md with service overview, setup instructions, API examples
- Architecture Decision Records (ADRs) for key decisions (stored in docs/adr/)

### 4.4 Deployment and Operations

#### Build Process Integration

- **.NET CLI:** `dotnet build`, `dotnet publish` with Release configuration
- **Docker:** Multi-stage Dockerfile (SDK build → runtime image)
- **Image Registry:** Harbor or Azure Container Registry
- **Build Pipeline:** Azure DevOps or GitHub Actions
- **Version Tagging:** Semantic versioning (major.minor.patch) based on git tags

#### Deployment Strategy

- **Platform:** Kubernetes (on-premises Infratel/Paratus data centers)
- **Deployment Tool:** Helm charts with GitOps (ArgoCD)
- **Environment Progression:** Dev → Staging → Production
- **Rollout Strategy:** Rolling updates with 25% max surge, zero downtime
- **Rollback:** Helm rollback command or ArgoCD sync to previous version
- **Database Migrations:** Applied via init container or manual kubectl exec before deployment
- **Feature Flags:** LaunchDarkly or custom FeatureFlag table for gradual rollouts

#### Monitoring and Logging

- **Metrics:** OpenTelemetry → Application Insights (custom metrics: kyc_workflow_duration, edd_escalation_count, risk_score_distribution)
- **Logs:** Serilog → ELK Stack (Elasticsearch, Logstash, Kibana) with correlation IDs
- **Traces:** Distributed tracing via OpenTelemetry for cross-service request flows
- **Health Checks:** Kubernetes liveness (/health) and readiness (/health/ready) probes
- **Alerts:** Prometheus AlertManager rules for error rate > 5%, latency > 2s, pod restarts > 3 in 10min

#### Configuration Management

- **Environment Variables:** Kubernetes ConfigMaps for non-sensitive config
- **Secrets:** HashiCorp Vault injected via Vault Agent sidecar or CSI driver
- **Config Structure:**
  - Connection strings → Vault (`intellifin/db-passwords/client-svc`)
  - MinIO credentials → Vault (`intellifin/minio/client-management`)
  - Risk scoring rules → Vault KV v2 (`intellifin/client-management/risk-scoring-rules`)
  - RabbitMQ connection → Vault (`intellifin/rabbitmq/client-management`)
- **Hot-Reload:** Risk scoring config only; all other config requires pod restart

### 4.5 Risk Assessment and Mitigation

#### Known Constraints from Architecture Document

**Known Constraint 1: Separate KycDocumentService Exists**

- **Risk:** Document management split between two services causes duplication and inconsistent dual-control enforcement
- **Impact:** High - Could lead to compliance gaps if verification rules differ
- **Mitigation:**
  - Phase 1: Client Management calls KycDocumentService via HTTP (no duplication)
  - Phase 2: Deprecate KycDocumentService, migrate to integrated DocumentLifecycleService
  - Phase 3: Archive old service post-migration
  - Feature flag to toggle between services during migration
  - Parallel run for 2 weeks before cutover

**Known Constraint 2: No EF Core DbContext Yet**

- **Risk:** Service has no database access configured
- **Impact:** Medium - Development blocker until configured
- **Mitigation:**
  - Add EF Core packages in first development sprint
  - Create ClientManagementDbContext with entity configurations
  - Generate initial migration
  - Test against local SQL Server before committing

**Known Constraint 3: No Camunda Worker Infrastructure**

- **Risk:** Service doesn't have Zeebe client or worker hosting configured
- **Impact:** High - KYC/EDD workflows cannot execute
- **Mitigation:**
  - Add Zeebe.Client package
  - Create CamundaWorkerHostedService (BackgroundService)
  - Implement worker registration pattern with topic subscriptions
  - Test with local Camunda instance before deploying BPMN

**Known Constraint 4: Vault Client Not Configured**

- **Risk:** No Vault integration for risk scoring config retrieval
- **Impact:** Medium - Risk profiling feature incomplete
- **Mitigation:**
  - Add VaultSharp package
  - Create VaultRiskConfigProvider with 60s polling
  - Implement caching and hot-reload callbacks
  - Test with local Vault instance

#### Technical Risks

- **Risk 1:** Camunda BPMN version conflicts when updating workflows
  - **Mitigation:** Deploy new BPMN versions alongside old (e.g., client_kyc_v2.bpmn), let in-flight workflows complete, migrate new clients to new version
- **Risk 2:** Vault hot-reload causing inconsistent risk scores mid-execution
  - **Mitigation:** Lock config version per scoring execution (snapshot at start), log config version in RiskProfile table
- **Risk 3:** MinIO Object Lock prevents legitimate document deletes
  - **Mitigation:** Use MinIO Legal Hold for admin overrides (requires CEO approval + audit), soft-delete pattern (IsArchived flag)

#### Integration Risks

- **Risk 1:** AdminService audit endpoint latency causes backpressure
  - **Mitigation:** Batch audit events (buffer 100 events or 5s), async fire-and-forget with retry queue
- **Risk 2:** CommunicationsService downtime prevents KYC approval notifications
  - **Mitigation:** Store failed notifications in dead letter queue, retry with exponential backoff, alert operations after 3 failures
- **Risk 3:** KycDocumentService API changes break Phase 1 integration
  - **Mitigation:** Version KycDocumentService API (v1/v2 routes), maintain v1 compatibility until migration complete

#### Deployment Risks

- **Risk 1:** Database migration fails in production
  - **Mitigation:** Generate SQL scripts for review, test migrations on staging clone of production, use EF Core migrations bundle for repeatable execution
- **Risk 2:** Camunda BPMN deployment fails after service deployment
  - **Mitigation:** Use Helm post-install hook to deploy BPMNs, validate BPMN syntax with zbctl before release, include BPMN rollback in deployment runbook
- **Risk 3:** Vault credential rotation breaks service connectivity
  - **Mitigation:** Use Vault Agent sidecar with automatic token renewal, implement connection retry logic with circuit breaker, alert on authentication failures

#### Mitigation Strategies Summary

- **Phased Migration:** KycDocumentService integration split into 3 phases to reduce risk
- **Feature Flags:** Toggle between old/new functionality during transitions
- **Parallel Runs:** Run old and new systems side-by-side for 2 weeks before cutover
- **Rollback Plans:** Document rollback procedures for all major changes
- **Monitoring:** Comprehensive alerting on integration failures, performance degradation
- **Testing:** 90% test coverage requirement enforced before production deployment

---

## 5. Epic and Story Structure

### 5.1 Epic Approach

**Epic Structure Decision:** **Single Comprehensive Epic**

**Rationale:**

This enhancement should be structured as **one comprehensive epic** rather than multiple separate epics for the following reasons:

1. **Tightly Coupled Components:** The Client Management compliance engine is a cohesive system where components depend on each other (customer versioning requires base Client entity, document lifecycle depends on customer records, KYC workflows orchestrate document verification and risk scoring, etc.)

2. **Single Business Goal:** All features serve one unified objective: "Transform Client Management into a BoZ-compliant customer data compliance engine." This is not multiple independent features—it's one integrated system with multiple capabilities.

3. **Brownfield Integration Complexity:** Enhancing an existing (minimal) service and integrating with 3 existing services (AdminService, Communications, KycDocumentService) creates complexity that would be awkward to split across multiple epics.

4. **Shared Technical Foundation:** All stories will build on the same foundational infrastructure (same database schema, same Camunda workflow engine integration, same Vault client configuration, same MinIO document storage patterns).

5. **Deployment Atomicity:** This enhancement should be deployed as a cohesive unit to avoid partial-state scenarios where documents are stored but KYC workflows aren't operational, or risk scoring is active but audit trails aren't logging correctly.

**Epic Naming:** "Client Management Compliance Engine - Unified Customer Records, Workflows, and Audit"

---

## 6. Epic 1: Client Management Compliance Engine

**Epic Goal:**

Transform the Client Management module from a minimal service scaffold into a comprehensive BoZ-compliant customer data compliance engine that provides unified customer records with temporal versioning, automated KYC/AML workflows, dual-control document verification, Vault-based risk profiling, and full audit trail integration—enabling Limelight Moneylink Services to pass regulatory inspections and support multi-branch operations.

**Integration Requirements:**

- Maintain backward compatibility with existing service contracts (AdminService audit schema, CommunicationsService notification format)
- Integrate with KycDocumentService via HTTP client (Phase 1) without breaking existing document storage functionality
- Ensure all audit events conform to AdminService chain integrity requirements
- Publish domain events to RabbitMQ exchange `client.events` with schema versioning for future PMEC module consumption
- Deploy Camunda BPMN processes (`client_kyc_v1.bpmn`, `client_edd_v1.bpmn`, `client_document_verification_v1.bpmn`) alongside service deployment
- Configure MinIO bucket with Object Lock COMPLIANCE mode before document storage begins
- Provision Vault secrets path `intellifin/client-management/risk-scoring-rules` before risk scoring service starts

---

### Story 1.1: Database Foundation and Entity Framework Core Setup

**As a** DevOps Engineer,  
**I want** to provision the Client Management database and configure Entity Framework Core with migrations,  
**so that** the service has a persistent data store ready for domain entities and can track schema changes through migrations.

**Acceptance Criteria:**

1. SQL Server database `IntelliFin.ClientManagement` created with service account `client_svc` and appropriate permissions
2. EF Core NuGet packages added to project (Microsoft.EntityFrameworkCore.SqlServer 9.0)
3. `ClientManagementDbContext` class created in `Infrastructure/Persistence` with DbContext base configuration
4. Connection string retrieved from Vault (`intellifin/db-passwords/client-svc`) and configured in `Program.cs`
5. Initial migration `InitialCreate` generated and applied to development database
6. Health check endpoint `/health/db` added to verify database connectivity
7. Integration test using TestContainers validates DbContext can connect and execute queries

**Integration Verification:**

- **IV1: Existing Service Health:** Health check `/health` endpoint remains functional and returns 200 OK
- **IV2: Observability Intact:** Serilog and OpenTelemetry instrumentation continue logging database connection events
- **IV3: Deployment Pipeline:** Service can still build, test, and deploy with new database dependency added

---

### Story 1.2: Shared Library References and Dependency Injection Configuration

**As a** Backend Developer,  
**I want** to add all required shared library references and configure dependency injection for core services,  
**so that** the service can use common authentication, audit, validation, and infrastructure patterns from other IntelliFin services.

**Acceptance Criteria:**

1. Project references added for:
   - `IntelliFin.Shared.Authentication` (JWT validation)
   - `IntelliFin.Shared.Audit` (audit abstractions)
   - `IntelliFin.Shared.DomainModels` (shared types)
   - `IntelliFin.Shared.Infrastructure` (common utilities)
   - `IntelliFin.Shared.Validation` (FluentValidation helpers)
2. `ServiceCollectionExtensions.cs` created in `Extensions/` folder with DI registration methods
3. JWT authentication configured in `Program.cs` with IdentityService as token authority
4. Global exception handling middleware registered
5. FluentValidation configured for automatic model validation
6. Correlation ID middleware added to inject correlation IDs into all log entries

**Integration Verification:**

- **IV1: Authentication Works:** Protected endpoints return 401 Unauthorized without valid JWT token
- **IV2: Logging Consistency:** Correlation IDs appear in all log entries matching pattern from other services
- **IV3: Shared Behavior:** Exception handling returns consistent error responses matching other IntelliFin services

---

### Story 1.3: Client Entity and Basic CRUD Operations (No Versioning)

**As a** Loan Officer,  
**I want** to create, retrieve, and update client profiles with basic information (NRC, name, contact, address),  
**so that** I can maintain customer records in the system before adding versioning complexity.

**Acceptance Criteria:**

1. `Client` entity class created in `Domain/Entities` with properties: Id, Nrc, FirstName, LastName, DateOfBirth, Gender, PrimaryPhone, PhysicalAddress, City, Province, Status, BranchId, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
2. `ClientConfiguration` EF Core fluent API configuration created with unique index on `Nrc`, foreign key to `Branch` table
3. `ClientService` interface and implementation created in `Services/` with methods: CreateClientAsync, GetClientByIdAsync, GetClientByNrcAsync, UpdateClientAsync
4. Minimal API endpoints created in `Controllers/ClientController.cs`:
   - POST /api/clients (create)
   - GET /api/clients/{id} (retrieve by ID)
   - GET /api/clients/by-nrc/{nrc} (retrieve by NRC)
   - PUT /api/clients/{id} (update)
5. `CreateClientRequest` and `ClientResponse` DTOs created with FluentValidation rules
6. Unit tests for ClientService with 90%+ coverage
7. Integration tests using TestContainers for end-to-end CRUD operations

**Integration Verification:**

- **IV1: Database Schema:** EF Core migration successfully creates `Clients` table with all constraints
- **IV2: API Documentation:** OpenAPI/Swagger documentation auto-generates for new endpoints
- **IV3: Audit Logging:** All CRUD operations log to Serilog with correlation IDs (AdminService integration comes later)

---

### Story 1.4: Client Versioning with SCD-2 Temporal Tracking

**As a** Compliance Officer,  
**I want** every client profile change to create a full snapshot version record with ValidFrom/ValidTo timestamps,  
**so that** I can query the customer's profile at any point in time for regulatory audits.

**Acceptance Criteria:**

1. `ClientVersion` entity class created in `Domain/Entities` with full snapshot fields plus ValidFrom, ValidTo, IsCurrent, VersionNumber, ChangeSummaryJson, ChangeReason, CreatedBy, IpAddress, CorrelationId
2. `ClientVersionConfiguration` EF Core configuration with composite unique index on `(ClientId, VersionNumber)`, index on `(ClientId, ValidFrom, ValidTo)`, index on `(ClientId, IsCurrent)`
3. Database trigger or check constraint ensures only one version can have `IsCurrent = true` per ClientId
4. `ClientVersioningService` created with methods: CreateVersionAsync, GetVersionHistoryAsync, GetVersionAtTimestampAsync
5. ClientService.UpdateClientAsync modified to:
   - Set current version's `IsCurrent = false` and `ValidTo = NOW()`
   - Create new ClientVersion with `IsCurrent = true`, `ValidFrom = NOW()`, `VersionNumber = previous + 1`
   - Update Client entity's `VersionNumber` field
6. New API endpoints:
   - GET /api/clients/{id}/versions (list all versions)
   - GET /api/clients/{id}/versions/{versionNumber} (get specific version)
   - GET /api/clients/{id}/versions/at/{timestamp} (point-in-time query)
7. Unit tests validate versioning logic, including SCD-2 constraints
8. Integration tests verify temporal queries return correct historical snapshots

**Integration Verification:**

- **IV1: Existing CRUD Intact:** Client creation and retrieval without versioning parameters still work correctly
- **IV2: Data Integrity:** Check constraint prevents multiple IsCurrent=true versions (test with duplicate insert attempt)
- **IV3: Performance:** Point-in-time queries complete in < 2 seconds with indexed ValidFrom/ValidTo columns

---

### Story 1.5: AdminService Audit Trail Integration

**As a** Compliance Officer,  
**I want** every client profile change, document action, and compliance event to be logged immutably in AdminService,  
**so that** we have a complete audit trail for BoZ regulatory inspections.

**Acceptance Criteria:**

1. `AdminServiceClient.cs` Refit interface created in `Integration/` with POST /api/audit/events and POST /api/audit/events/batch methods
2. `AuditEventDto` class created matching AdminService schema (Actor, Action, EntityType, EntityId, CorrelationId, IpAddress, EventData)
3. `AuditMiddleware` created to capture correlation ID and IP address for all requests
4. ClientService methods modified to call AdminServiceClient.LogAuditEventAsync after:
   - Client created (Action: ClientCreated)
   - Client updated (Action: ClientUpdated, EventData includes version number)
5. Audit event batching configured: buffer 100 events or 5 seconds, whichever first
6. Retry policy configured with exponential backoff (3 retries, 1s/2s/4s delays)
7. Dead letter queue configured for audit events that fail after max retries
8. Integration tests with WireMock verify audit events sent correctly

**Integration Verification:**

- **IV1: AdminService Contract:** Audit events conform to existing AuditEventDto schema without breaking AdminService
- **IV2: Chain Integrity:** Audit events include PreviousEventHash field if AdminService chain integrity is enabled
- **IV3: Resilience:** Service continues functioning if AdminService is temporarily unavailable (events queued for retry)

---

### Story 1.6: KycDocumentService Integration (Phase 1 - HTTP Client)

**As a** Loan Officer,  
**I want** to upload KYC documents for a client and have them stored in MinIO via KycDocumentService,  
**so that** I can attach supporting documents to customer profiles without duplicating MinIO integration logic.

**Acceptance Criteria:**

1. `KycDocumentServiceClient.cs` Refit interface created in `Integration/` with:
   - POST /documents (upload document)
   - GET /documents/{id} (retrieve document metadata)
   - GET /documents/{id}/download (get pre-signed URL)
2. `ClientDocument` entity created in `Domain/Entities` with fields: Id, ClientId, DocumentType, Category, ObjectKey (MinIO path), BucketName, FileName, ContentType, FileSizeBytes, FileHashSha256, UploadStatus, UploadedAt, UploadedBy, VerifiedAt, VerifiedBy, ExpiryDate, RetentionUntil
3. `DocumentLifecycleService` created with methods: UploadDocumentAsync (calls KycDocumentService), GetDocumentMetadataAsync, GenerateDownloadUrlAsync
4. API endpoints created:
   - POST /api/clients/{id}/documents (multipart/form-data file upload)
   - GET /api/clients/{id}/documents (list documents for client)
   - GET /api/clients/{id}/documents/{docId} (get document metadata)
   - GET /api/clients/{id}/documents/{docId}/download (get pre-signed URL)
5. Document upload flow:
   - Validate file size (max 10MB), content type (PDF, JPG, PNG)
   - Call KycDocumentService to store in MinIO
   - Store metadata in ClientDocument table with ObjectKey from response
   - Set RetentionUntil = NOW() + 7 years
6. Integration tests with WireMock for KycDocumentService

**Integration Verification:**

- **IV1: KycDocumentService Contract:** HTTP requests match existing KycDocumentService API schema
- **IV2: MinIO Storage:** Documents successfully stored in MinIO bucket with 7-year Object Lock retention
- **IV3: Metadata Consistency:** ClientDocument SQL records match MinIO object metadata (FileHashSha256, FileSizeBytes)

---

### Story 1.7: CommunicationsService Integration for Event-Based Notifications

**As a** Customer,  
**I want** to receive SMS and email notifications when my KYC status changes,  
**so that** I stay informed about my loan application progress.

**Acceptance Criteria:**

1. `CommunicationsClient.cs` Refit interface created in `Integration/` with POST /api/communications/send method
2. `SendNotificationRequest` DTO created matching CommunicationsService schema (TemplateId, RecipientId, Channel, PersonalizationData)
3. `CommunicationConsent` entity created in `Domain/Entities` with fields: Id, ClientId, ConsentType, SmsEnabled, EmailEnabled, InAppEnabled, CallEnabled, ConsentGivenAt, ConsentGivenBy, ConsentRevokedAt
4. `ConsentManagementService` created with methods: GetConsentAsync, UpdateConsentAsync, CheckConsentAsync
5. API endpoints created:
   - GET /api/clients/{id}/consents (get consent preferences)
   - PUT /api/clients/{id}/consents (update consent preferences)
6. Notification helper method created: SendConsentBasedNotificationAsync checks consent before calling CommunicationsClient
7. Integration tests with WireMock verify notifications sent only when consent granted

**Integration Verification:**

- **IV1: CommunicationsService Contract:** Notification requests conform to existing SendNotificationRequest schema
- **IV2: Consent Enforcement:** Notifications are NOT sent if customer has SmsEnabled=false or EmailEnabled=false
- **IV3: Template Compatibility:** TemplateId values reference existing templates in CommunicationsService

---

### Story 1.8: Document Dual-Control Verification Workflow

**As a** Compliance Officer,  
**I want** to enforce dual-control verification where one officer uploads a document and a different officer must verify it,  
**so that** we prevent fraud and satisfy BoZ dual-control requirements.

**Acceptance Criteria:**

1. `UploadStatus` enum added to ClientDocument: Uploaded, PendingVerification, Verified, Rejected
2. DocumentLifecycleService.UploadDocumentAsync sets UploadStatus=Uploaded, stores UploadedBy from JWT claims
3. New API endpoint: PUT /api/clients/{id}/documents/{docId}/verify with VerifyDocumentRequest (Approved: bool, RejectionReason?: string)
4. DocumentLifecycleService.VerifyDocumentAsync validates:
   - Current user (from JWT) != UploadedBy (enforces dual-control)
   - Document exists and UploadStatus = Uploaded
   - If approved: Set UploadStatus=Verified, VerifiedBy=current user, VerifiedAt=NOW()
   - If rejected: Set UploadStatus=Rejected, RejectionReason from request
5. Database trigger created to prevent self-verification: `CHECK (VerifiedBy IS NULL OR VerifiedBy <> UploadedBy)`
6. Audit events logged for both upload and verification actions
7. Unit tests verify dual-control enforcement (self-verification throws exception)

**Integration Verification:**

- **IV1: Database Constraint:** Trigger prevents self-verification even if service validation is bypassed
- **IV2: Audit Trail:** Both UploadedBy and VerifiedBy users logged to AdminService with distinct Action values
- **IV3: Existing Upload Flow:** Document upload without immediate verification still works (status remains Uploaded)

---

### Story 1.9: Camunda Worker Infrastructure and Topic Registration

**As a** System Architect,  
**I want** to integrate Camunda Zeebe client and register background workers for KYC workflow tasks,  
**so that** the service can participate in BPMN-orchestrated business processes.

**Acceptance Criteria:**

1. Zeebe .NET Client NuGet package added (version 2.6+)
2. `CamundaWorkerHostedService` created as BackgroundService in `Workflows/CamundaWorkers/`
3. Camunda connection configuration added to appsettings.json: GatewayAddress, WorkerName, MaxJobsToActivate, PollingIntervalSeconds
4. Base worker interface `ICamundaJobHandler` created with HandleJobAsync method
5. Worker registration method created: RegisterWorker<THandler>(topicName, jobType)
6. Example worker `HealthCheckWorker` created for topic `client.health.check` to validate infrastructure
7. Health check endpoint `/health/camunda` added to verify Zeebe gateway connectivity
8. Integration tests with Camunda Test SDK validate worker registration and job handling
9. Configuration includes error handling: retry with exponential backoff, DLQ after 3 failures

**Integration Verification:**

- **IV1: Service Startup:** Service starts successfully with Camunda workers registered (logs show topic subscriptions)
- **IV2: Worker Isolation:** Camunda worker failures do not crash main service (workers run in separate background thread)
- **IV3: Existing Endpoints:** REST API endpoints remain responsive while workers are polling Camunda

---

### Story 1.10: KYC Status Entity and State Machine

**As a** KYC Officer,  
**I want** to track the KYC compliance state for each client (Pending, InProgress, Completed, EDD_Required, Rejected),  
**so that** I can see which clients need KYC review and what stage they're in.

**Acceptance Criteria:**

1. `KycStatus` entity created in `Domain/Entities` with fields: Id, ClientId (unique), CurrentState, KycStartedAt, KycCompletedAt, KycCompletedBy, CamundaProcessInstanceId, HasNrc, HasProofOfAddress, HasPayslip, IsDocumentComplete, AmlScreeningComplete, RequiresEdd, EddReason
2. `KycStatusConfiguration` EF Core configuration with unique index on ClientId
3. `KycWorkflowService` created with methods: InitiateKycAsync, UpdateKycStateAsync, GetKycStatusAsync
4. API endpoints created:
   - POST /api/clients/{id}/kyc/initiate (start KYC workflow)
   - GET /api/clients/{id}/kyc-status (get current state)
   - PUT /api/clients/{id}/kyc/state (update state - called by Camunda workers)
5. State machine validation: Only valid state transitions allowed (e.g., Pending → InProgress → Completed, not Pending → Completed)
6. When KYC initiated: Create KycStatus record with CurrentState=Pending, KycStartedAt=NOW()
7. Unit tests validate state machine transitions

**Integration Verification:**

- **IV1: Client CRUD:** Creating/updating clients does NOT automatically create KycStatus (explicit initiation required)
- **IV2: State Validation:** Invalid state transitions throw exception with clear error message
- **IV3: Idempotency:** Calling InitiateKycAsync multiple times for same client does not create duplicate records

---

### Story 1.11: KYC Verification Workflow Implementation (client_kyc_v1.bpmn)

**As a** KYC Officer,  
**I want** an automated KYC workflow that checks document completeness, performs AML screening, and assigns human review tasks,  
**so that** KYC verification follows a consistent, auditable process.

**Acceptance Criteria:**

1. `client_kyc_v1.bpmn` BPMN process created in `Workflows/BPMN/` with:
   - Start event: Triggered by ClientCreatedEvent
   - Service task: Check document completeness
   - Exclusive gateway: Complete vs Incomplete
   - Service task: AML screening (if complete)
   - Exclusive gateway: AML result (Clear vs Hit)
   - Service task: Risk assessment (if AML clear)
   - Human task: KYC officer review
   - End events: Approved, Rejected, EDD_Required
2. Camunda workers created:
   - `KycDocumentCheckWorker` for topic `client.kyc.check-documents`
   - `AmlScreeningWorker` for topic `client.kyc.aml-screening`
   - `RiskAssessmentWorker` for topic `client.kyc.risk-assessment`
3. `KycDocumentCheckWorker` implementation:
   - Query ClientDocument table for client
   - Set workflow variables: hasNrc, hasProofOfAddress, hasPayslip, documentComplete
   - Update KycStatus.IsDocumentComplete
4. Human task form created in Camunda for KYC officer review with fields: Approve (boolean), Comments (string)
5. Domain events published:
   - `KycCompletedEvent` when approved
   - `KycRejectedEvent` when rejected
   - `EddEscalatedEvent` when escalated
6. Integration tests with Camunda Test SDK validate full workflow execution

**Integration Verification:**

- **IV1: BPMN Deployment:** BPMN process deploys successfully to Camunda and appears in Operate UI
- **IV2: Worker Execution:** Workers process tasks without errors, complete jobs successfully
- **IV3: Event Publishing:** Domain events published to RabbitMQ exchange `client.events` with correct routing keys

---

### Story 1.12: AML Screening and Enhanced Due Diligence (EDD) Workflow

**As a** Compliance Officer,  
**I want** to screen clients against sanctions lists and PEP databases, and escalate high-risk clients to Enhanced Due Diligence,  
**so that** we comply with BoZ AML requirements and prevent onboarding sanctioned individuals.

**Acceptance Criteria:**

1. `AmlScreening` entity created in `Domain/Entities` with fields: Id, KycStatusId, ScreeningType, ScreeningProvider, ScreenedAt, ScreenedBy, IsMatch, MatchDetails, RiskLevel, Notes
2. `AmlScreeningService` created with methods: PerformSanctionsScreeningAsync, PerformPepScreeningAsync, RecordScreeningResultAsync
3. `AmlScreeningWorker` implementation for topic `client.kyc.aml-screening`:
   - Call AmlScreeningService to perform sanctions and PEP checks (manual list initially)
   - Create AmlScreening records
   - Set workflow variables: amlRiskLevel (Clear, Low, Medium, High), sanctionsHit (bool), pepMatch (bool)
   - Update KycStatus.AmlScreeningComplete = true
   - If High risk: Set workflow variable escalateToEdd = true
4. `client_edd_v1.bpmn` BPMN process created with:
   - Start event: Triggered by EddEscalatedEvent
   - Service task: Generate EDD report PDF
   - Human task: Compliance officer review
   - Human task: CEO approval (if compliance approves)
   - End events: Approved, Rejected
5. `EddReportGenerationWorker` created for topic `client.edd.generate-report`:
   - Create PDF with client profile, AML screening results, document verification status, risk factors
   - Store PDF in MinIO
   - Set ClientDocument.EddReportObjectKey
6. Integration tests validate EDD escalation path

**Integration Verification:**

- **IV1: KYC Workflow Integration:** High AML risk in KYC workflow correctly triggers EDD workflow start
- **IV2: EDD Report Storage:** EDD report PDF stored in MinIO with correct retention policy
- **IV3: CEO Approval:** EDD human task assignable to role:ceo with MFA step-up requirement (validated in Camunda)

---

### Story 1.13: Vault Integration and Risk Scoring Engine

**As a** Risk Manager,  
**I want** to compute customer risk scores using Vault-managed business rules that can be updated without code deployments,  
**so that** we can adapt risk assessment criteria as regulations and business conditions change.

**Acceptance Criteria:**

1. VaultSharp NuGet package added (version 1.15+)
2. `RiskProfile` entity created in `Domain/Entities` with fields: Id, ClientId (unique), RiskRating, RiskScore, ComputedAt, ComputedBy, RiskRulesVersion, RiskRulesChecksum, RuleExecutionLog, InputFactorsJson, IsCurrent
3. `VaultRiskConfigProvider` created in `Infrastructure/VaultClient/` implementing IRiskConfigProvider with:
   - GetCurrentConfigAsync() method
   - Polling mechanism (every 60 seconds)
   - Cache with version/checksum comparison
   - RegisterConfigChangeCallback() for hot-reload notifications
4. `RiskScoringService` created with ComputeRiskAsync method:
   - Retrieve current config from Vault
   - Build input factors JSON (kycComplete, amlRiskLevel, isPep, hasSanctionsHit)
   - Execute JSONLogic/CEL rules
   - Calculate risk score (0-100)
   - Map score to rating (Low: 0-25, Medium: 26-50, High: 51-100)
   - Store RiskProfile with rules version/checksum
5. `RiskAssessmentWorker` implementation for topic `client.kyc.risk-assessment`:
   - Call RiskScoringService.ComputeRiskAsync
   - Update Client.RiskRating and Client.RiskLastAssessedAt
   - Set workflow variable: riskRating
6. API endpoint: GET /api/clients/{id}/risk-profile
7. Integration tests with local Vault instance

**Integration Verification:**

- **IV1: Vault Connectivity:** Health check `/health/vault` verifies connection to Vault
- **IV2: Hot-Reload:** Risk config version change detected within 60 seconds without service restart
- **IV3: Rule Traceability:** RiskProfile.RiskRulesVersion matches Vault config version at time of computation

---

### Story 1.14: Event-Driven Notification Triggers for KYC Status Changes

**As a** Customer,  
**I want** to receive SMS notifications when my KYC status changes (Approved, Rejected, EDD Required),  
**so that** I know the status of my loan application without having to call the branch.

**Acceptance Criteria:**

1. Domain event handlers created for:
   - `KycCompletedEvent` → Trigger notification with template `kyc_approved`
   - `KycRejectedEvent` → Trigger notification with template `kyc_rejected`
   - `EddEscalatedEvent` → Trigger notification with template `kyc_edd_required`
2. `NotificationService` created with SendKycStatusNotificationAsync method:
   - Retrieve client and consent preferences
   - Check ConsentType=Operational and SmsEnabled=true
   - Call CommunicationsClient with appropriate template and personalization data (ClientName, KycStatus)
3. MassTransit consumers created in `Consumers/` folder for each event
4. RabbitMQ configuration added: exchange `client.events`, routing keys `client.kyc.*`
5. Integration tests with MassTransit In-Memory test harness validate event publishing and consumption
6. Notification retry logic: 3 retries with exponential backoff, DLQ after failures

**Integration Verification:**

- **IV1: Consent Enforcement:** No notification sent if customer has SmsEnabled=false (verified with test)
- **IV2: CommunicationsService Load:** Notifications queued asynchronously, do not block KYC workflow completion
- **IV3: Event Ordering:** Events published in correct order (KycCompletedEvent always after AML screening completes)

---

### Story 1.15: Document Expiry Monitoring and Reminder Notifications

**As a** Branch Manager,  
**I want** to receive automated alerts when client KYC documents are approaching expiry,  
**so that** I can proactively request updated documents and maintain compliance.

**Acceptance Criteria:**

1. `DocumentExpiryMonitoringService` created as BackgroundService running daily at 2 AM
2. Query logic: SELECT documents WHERE ExpiryDate IS NOT NULL AND ExpiryDate BETWEEN NOW() AND NOW() + 30 days AND IsArchived=false
3. For each expiring document:
   - Call NotificationService to send notification with template `document_expiring_soon`
   - Personalization data: ClientName, DocumentType, ExpiryDate, DaysRemaining
   - Check consent before sending
4. Audit event logged: DocumentExpiryReminderSent
5. Configuration setting: `DocumentExpiryReminderDays` (default 30) to customize warning period
6. Unit tests with mocked current date validate reminder logic

**Integration Verification:**

- **IV1: Service Performance:** Daily monitoring job completes in < 5 minutes for up to 10,000 documents
- **IV2: No Duplicate Reminders:** Reminders sent only once per document per day (idempotency check)
- **IV3: Existing Jobs:** Monitoring service runs independently without interfering with API request processing

---

### Story 1.16: Integration Testing Suite with TestContainers

**As a** QA Engineer,  
**I want** comprehensive integration tests using TestContainers for SQL Server, MinIO, and mocked external services,  
**so that** we can validate end-to-end workflows in an isolated test environment.

**Acceptance Criteria:**

1. Test project `IntelliFin.ClientManagement.IntegrationTests` created with xUnit
2. TestContainers NuGet packages added: Testcontainers.MsSql, Testcontainers.Minio
3. `ClientManagementTestFixture` class created implementing IAsyncLifetime:
   - Start SQL Server container
   - Start MinIO container
   - Apply EF Core migrations
   - Seed test data (sample clients, documents, KYC statuses)
4. WireMock configured for external service mocks: AdminService, CommunicationsService, KycDocumentService
5. Integration test scenarios created:
   - Client CRUD with versioning (create, update, query historical versions)
   - Document upload → dual-control verification → MinIO storage
   - KYC workflow end-to-end (initiate → document check → AML screening → risk assessment → approval)
   - EDD escalation path (high risk → EDD report generation → compliance review → CEO approval)
   - Consent-based notification (consent granted → notification sent, consent revoked → no notification)
6. Test coverage report generated: 85%+ integration test coverage
7. CI/CD pipeline configured to run integration tests on every pull request

**Integration Verification:**

- **IV1: Test Isolation:** Tests run in parallel without interfering with each other (separate test databases)
- **IV2: Cleanup:** TestContainers automatically clean up after tests (no leaked containers)
- **IV3: CI/CD Performance:** Integration test suite completes in < 10 minutes in CI pipeline

---

### Story 1.17: KycDocumentService Migration - Phase 2 (Optional/Future)

**As a** System Architect,  
**I want** to consolidate document management into Client Management by migrating MinIO integration from KycDocumentService,  
**so that** we eliminate service duplication and simplify the architecture.

**Acceptance Criteria:**

1. `MinioDocumentStorageService` class created in `Infrastructure/Storage/` (copied from KycDocumentService pattern)
2. Minio.NET NuGet package added (version 6.0+)
3. MinIO configuration added: Endpoint, AccessKey, SecretKey, BucketName, UseSSL
4. DocumentLifecycleService refactored with feature flag `UseKycDocumentService`:
   - If true: Use KycDocumentServiceClient (Phase 1 behavior)
   - If false: Use MinioDocumentStorageService (Phase 2 behavior)
5. Migration script created to update ClientDocument.ObjectKey paths if bucket structure changes
6. Parallel run testing: Both Phase 1 and Phase 2 paths tested in staging for 2 weeks
7. Rollback plan documented: Feature flag toggled back to Phase 1 if issues arise
8. KycDocumentService marked as deprecated with sunset date

**Integration Verification:**

- **IV1: Feature Flag Toggle:** Service can switch between Phase 1 and Phase 2 with config change (no code deployment)
- **IV2: Document Retrieval:** Documents uploaded via Phase 1 are readable via Phase 2 (same bucket/paths)
- **IV3: KycDocumentService Deprecation:** No new features added to KycDocumentService after Phase 2 deployment

---

## Story Dependency Summary

**Story Sequencing Rationale:**

This story sequence is designed to minimize risk to the existing system by:

1. **Building foundation first** (Stories 1.1-1.2: database, DI, basic infrastructure) before adding complexity
2. **Establishing data model** (Stories 1.3-1.4: Client entity and versioning) before integrations
3. **Integrating with existing services early** (Stories 1.5-1.7: AdminService, KycDocumentService, CommunicationsService) to catch contract issues
4. **Adding dual-control verification** (Story 1.8) before workflow orchestration to ensure security baseline
5. **Deferring workflow orchestration** (Stories 1.9-1.12: Camunda, KYC/EDD workflows) until data model and integrations are stable
6. **Implementing risk scoring** (Story 1.13: Vault integration) after workflows are operational
7. **Adding event-driven notifications** (Story 1.14) after workflows and communications are integrated
8. **Implementing monitoring** (Story 1.15: document expiry) as operational enhancement
9. **Validating with integration tests** (Story 1.16) once all features are implemented
10. **Making migration optional** (Story 1.17) with feature flags for gradual rollout

**Dependencies:**
- Stories 1.1-1.2 are prerequisites for all others (foundation)
- Stories 1.3-1.8 can be worked in parallel after foundation (data model + integrations)
- Stories 1.9-1.12 require 1.3-1.8 complete (workflows depend on data + integrations)
- Stories 1.13-1.15 require 1.9-1.12 complete (enhancements depend on workflows)
- Story 1.16 requires all features complete (integration testing)
- Story 1.17 is optional and can be executed independently (migration)

---

**Document Status:** ✅ COMPLETE  
**Author:** PM John  
**Date:** 2025-10-16  
**Next Steps:** PO validation → Architecture review (if needed) → Story creation by SM Agent
