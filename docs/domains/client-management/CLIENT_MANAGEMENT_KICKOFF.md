# Client Management Module - Implementation Kickoff

**Date:** 2025-10-20  
**Branch:** `feature/client-management`  
**Status:** ✅ Ready to Begin  
**Current State:** Minimal scaffold (95 files, basic ASP.NET Core setup)

---

## Executive Summary

You're about to transform the Client Management module from a minimal ASP.NET Core scaffold into a **comprehensive compliance engine** that will serve as the single source of truth for all customer data in the IntelliFin system.

### What You're Building:
- **Unified customer records** with temporal versioning (SCD-2)
- **KYC/AML compliance workflows** orchestrated by Camunda
- **Document lifecycle management** with MinIO + 7-year retention
- **Risk scoring engine** with Vault-based rules
- **Dual-control verification** for regulatory compliance
- **Consent-based communications** management
- **Full audit trails** for Bank of Zambia compliance

---

## Current State Assessment

### ✅ What Exists (Minimal Scaffold)

**Location:** `apps/IntelliFin.ClientManagement/`

**Files Present:**
- ✅ `Program.cs` - Basic web app configuration (21 lines)
- ✅ `IntelliFin.ClientManagement.csproj` - Minimal project file
- ✅ `appsettings.json` + `appsettings.Development.json`
- ✅ OpenTelemetry integration via `IntelliFin.Shared.Observability`
- ✅ Health check endpoint (`/health`)
- ✅ OpenAPI/Swagger in Development mode

**What's Working:**
- Service starts successfully
- Observability configured (logging, tracing)
- Basic health monitoring

### ❌ What's Missing (Everything Else)

**NO business logic, data models, or domain services implemented yet:**
- ❌ No database (EF Core not configured)
- ❌ No entities or domain models
- ❌ No services or repositories
- ❌ No controllers (beyond basic health check)
- ❌ No Camunda integration
- ❌ No Vault integration
- ❌ No MinIO integration
- ❌ No external service clients

**This is a greenfield implementation within the existing scaffold.**

---

## Implementation Overview

### Total Scope: 17 Stories Across 4 Epics

#### **Epic 1: Foundation & Core CRUD (Stories 1.1-1.7)**
Establishes database, entities, basic CRUD, versioning, and service integrations.

#### **Epic 2: KYC/AML Workflows (Stories 1.8-1.12)**
Adds Camunda workflows, dual-control verification, document management.

#### **Epic 3: Risk & Compliance (Stories 1.13-1.16)**
Implements Vault-based risk scoring, monitoring, regulatory reporting.

#### **Epic 4: Observability (Story 1.17)**
Adds performance monitoring and analytics.

---

## Story Priority & Implementation Order

### 🚀 **Phase 1: Foundation (Week 1-2)**

#### **Story 1.1: Database Foundation & EF Core Setup** ⭐ START HERE
**Effort:** 5 SP (8-12 hours)  
**Priority:** Critical - Blocks everything else

**What You'll Build:**
- SQL Server database `IntelliFin.ClientManagement`
- EF Core DbContext with migrations
- Vault integration for connection strings
- Health check for database connectivity
- Integration tests with TestContainers

**Key Files to Create:**
- `Infrastructure/Persistence/ClientManagementDbContext.cs`
- `Extensions/ServiceCollectionExtensions.cs`
- `Infrastructure/Persistence/Migrations/YYYYMMDD_InitialCreate.cs`
- `tests/IntelliFin.ClientManagement.IntegrationTests/` (new project)

**Acceptance Criteria:**
1. Database created with service account
2. EF Core configured and working
3. Vault integration for secrets
4. `/health/db` endpoint returns healthy status
5. Integration tests pass with TestContainers

**Documentation:** `docs/domains/client-management/stories/1.1.database-foundation.story.md`

---

#### **Story 1.2: Shared Libraries & Dependency Injection** ⭐
**Effort:** 3 SP (4-6 hours)  
**Priority:** High - Foundation for all services

**What You'll Build:**
- Shared infrastructure (Result<T>, error handling)
- Correlation ID middleware
- Structured logging with Serilog
- DI registration patterns
- Configuration management

---

#### **Story 1.3: Client CRUD Operations** ⭐
**Effort:** 5 SP (8-12 hours)  
**Priority:** High - Core functionality

**What You'll Build:**
- `Client` entity (7 core entities total)
- `ClientService` with CRUD operations
- `ClientController` REST API (5 endpoints)
- FluentValidation for input
- Integration with AdminService for audit

**API Endpoints:**
- `POST /api/clients` - Create client
- `GET /api/clients/{id}` - Get client
- `PUT /api/clients/{id}` - Update client
- `DELETE /api/clients/{id}` - Soft delete
- `GET /api/clients?nrc=...` - Search by NRC

---

### 🔄 **Phase 2: Versioning & Integration (Week 2-3)**

#### **Story 1.4: Client Versioning (SCD-2)**
**Effort:** 8 SP (12-16 hours)

**What You'll Build:**
- `ClientVersion` entity with temporal tracking
- Version snapshot creation on every update
- Point-in-time historical queries
- Audit trail integration

---

#### **Story 1.5: AdminService Audit Integration**
**Effort:** 5 SP (8-12 hours)

**What You'll Build:**
- `AdminServiceClient` HTTP client
- Audit event batching (100 events / 5s)
- Async fire-and-forget with retry
- Correlation ID propagation

---

#### **Story 1.6: KycDocument Integration**
**Effort:** 5 SP (8-12 hours)

**What You'll Build:**
- `ClientDocument` entity
- `KycDocumentServiceClient` HTTP client
- MinIO integration for document storage
- SHA256 hash verification
- 7-year retention enforcement

---

#### **Story 1.7: Communications Integration**
**Effort:** 5 SP (8-12 hours)

**What You'll Build:**
- `CommunicationConsent` entity
- `CommunicationsClient` HTTP client
- Consent checking before notifications
- Multi-channel support (SMS, Email, In-App)

---

### 🔐 **Phase 3: Workflows & Compliance (Week 3-4)**

#### **Story 1.8: Dual-Control Verification**
**Effort:** 8 SP (12-16 hours)

**What You'll Build:**
- Database trigger to prevent self-verification
- Verification workflow
- Status tracking (Pending → Verified)
- Email notifications

---

#### **Story 1.9: Camunda Worker Infrastructure**
**Effort:** 8 SP (12-16 hours)

**What You'll Build:**
- Zeebe client integration
- `CamundaWorkerHostedService`
- Worker registration pattern
- 3 BPMN workflows (KYC, EDD, Document Verification)

---

#### **Story 1.10-1.12: KYC/AML Workflows**
**Effort:** 24 SP (36-48 hours combined)

**What You'll Build:**
- KYC state machine
- AML screening integration
- EDD escalation workflows
- Compliance officer approvals

---

### 📊 **Phase 4: Risk & Analytics (Week 4-5)**

#### **Story 1.13: Vault Risk Scoring**
**Effort:** 8 SP (12-16 hours)

**What You'll Build:**
- Vault client for risk rules
- JSONLogic/CEL rule execution
- Hot-reload configuration (60s polling)
- Risk profile computation

---

#### **Story 1.14-1.17: Monitoring & Compliance**
**Effort:** 20 SP (30-40 hours combined)

**What You'll Build:**
- Event-driven notifications
- Document expiry monitoring
- Regulatory compliance reporting
- Performance analytics dashboard

---

## Key Technical Integrations

### 1. **HashiCorp Vault**
- **Purpose:** Secrets management + dynamic risk scoring rules
- **Paths:** 
  - `intellifin/db-passwords/client-svc` (connection string)
  - `intellifin/client-management/risk-scoring-rules` (hot-reloaded)
- **Package:** VaultSharp 1.15+

### 2. **Camunda (Zeebe)**
- **Purpose:** KYC/AML workflow orchestration
- **Workflows:** 
  - `client_kyc_v1.bpmn`
  - `client_edd_v1.bpmn`
  - `document_verification_v1.bpmn`
- **Package:** Zeebe.Client 8.5+

### 3. **MinIO**
- **Purpose:** Document storage with Object Lock (WORM)
- **Features:** 7-year retention, SHA256 verification
- **Integration:** Via KycDocumentService HTTP API (Phase 1)

### 4. **External Services**
- **AdminService:** Audit logging
- **Communications:** Multi-channel notifications
- **KycDocumentService:** Document storage (to be deprecated)

### 5. **SQL Server 2022**
- **Database:** `IntelliFin.ClientManagement`
- **Entities:** 7 core entities
  - Client, ClientVersion, ClientDocument
  - AmlScreening, RiskProfile
  - CommunicationConsent, ClientEvent

---

## Architecture Patterns

### Clean Architecture / DDD

```
apps/IntelliFin.ClientManagement/
├── Controllers/              # API endpoints (thin layer)
├── Domain/
│   ├── Entities/            # Core domain models
│   ├── Events/              # Domain events
│   └── ValueObjects/        # NRC, PayrollNumber
├── Services/                # Business logic
├── Workflows/
│   └── CamundaWorkers/      # Zeebe job workers
├── Infrastructure/
│   ├── Persistence/         # EF Core DbContext
│   └── VaultClient/         # Vault integration
└── Integration/             # HTTP clients
```

### Key Design Principles
- **Async/await** for all I/O
- **Result<T>** pattern for operation outcomes
- **Correlation IDs** on all operations
- **Structured logging** with Serilog
- **Feature flags** for gradual rollouts
- **TestContainers** for integration tests

---

## Implementation Guidelines

### 📋 **Before You Start Each Story:**
1. Read the full story file in `docs/domains/client-management/stories/`
2. Review acceptance criteria
3. Check dependencies on previous stories
4. Review brownfield architecture document for context

### 🔨 **During Implementation:**
1. Follow existing patterns from other services
2. Enable nullable reference types
3. Add XML comments on public APIs
4. Use FluentValidation for input validation
5. Log all operations with correlation IDs
6. Add health checks for external dependencies

### ✅ **After Implementation:**
1. Run `dotnet build` - verify 0 errors
2. Run `dotnet test` - all tests pass
3. Verify integration tests with TestContainers
4. Test health check endpoints
5. Commit with clear message
6. Update story status

---

## Quality Gates

### Code Coverage Targets
- **Services:** 90%
- **Camunda Workers:** 80%
- **Domain Entities:** 100%

### Performance Targets
- **Client CRUD:** < 200ms p95
- **KYC Workflow:** < 5 min end-to-end
- **Document Upload:** < 10s for 10MB files
- **Risk Scoring:** < 100ms per computation
- **Vault Hot-Reload:** < 60s change detection

### Security Requirements
- TLS 1.3 in transit
- SQL Server TDE at rest
- MinIO SSE-S3 encryption
- JWT bearer token authentication
- Claims-based authorization

---

## Testing Strategy

### Unit Tests (xUnit + Moq)
- Service layer logic
- Domain entity behavior
- Validation rules

### Integration Tests (TestContainers)
- SQL Server container
- MinIO container
- Full API workflow tests

### Workflow Tests (Camunda Test SDK)
- BPMN validation
- Worker behavior
- Process completion

---

## Migration from KycDocumentService

**3-Phase Migration Plan:**

1. **Phase 1 (Stories 1.1-1.6):** 
   - Client Management calls KycDocumentService via HTTP
   - No duplication of functionality

2. **Phase 2 (Stories 1.7-1.12):**
   - Integrate DocumentLifecycleService internally
   - Feature flag to toggle between services

3. **Phase 3 (Post-completion):**
   - Deprecate KycDocumentService
   - Archive old service

**Current Phase:** Phase 1 (external HTTP integration)

---

## Next Steps

### ✅ **Immediate Action (Right Now):**

1. **Review Story 1.1 Documentation**
   ```bash
   cat "docs/domains/client-management/stories/1.1.database-foundation.story.md"
   ```

2. **Start Implementation**
   - Add EF Core NuGet packages
   - Create `ClientManagementDbContext`
   - Configure Vault integration
   - Generate initial migration
   - Add database health check
   - Create integration tests

3. **Estimated Time:** 8-12 hours for Story 1.1

---

## Reference Documentation

### Essential Reading (In Order)
1. ✅ **This Kickoff Document** - You're here ✓
2. 📖 **Story 1.1:** `docs/domains/client-management/stories/1.1.database-foundation.story.md`
3. 📖 **PRD:** `docs/domains/client-management/prd.md`
4. 📖 **Brownfield Architecture:** `docs/domains/client-management/brownfield-architecture.md`

### Supporting Documentation
- Customer Profile Management: `docs/domains/client-management/customer-profile-management.md`
- KYC/AML Compliance: `docs/domains/client-management/kyc-aml-compliance.md`
- Communications: `docs/domains/client-management/customer-communication-management.md`

---

## Success Criteria

### ✅ **Phase 1 Complete When:**
- Story 1.1-1.7 implemented (7 stories)
- Database operational with migrations
- Core CRUD APIs working
- External service integrations functional
- All integration tests passing
- Build: 0 errors

### ✅ **Module Complete When:**
- All 17 stories implemented
- 90% test coverage achieved
- KYC/AML workflows operational
- Risk scoring engine functional
- Document retention compliant (7 years)
- Performance targets met
- Bank of Zambia compliance requirements satisfied

---

## Branch Information

**Current Branch:** `feature/client-management`  
**Based On:** `master`  
**Status:** Clean working tree, ready for implementation

**Merge Target:** `master` (when complete)

---

## Support & Escalation

**Documentation Issues:** Review brownfield architecture document  
**Technical Blockers:** Check existing service implementations (IdentityService, AdminService)  
**Design Questions:** Refer to PRD and story acceptance criteria

---

**Ready to Begin!** 🚀

Start with **Story 1.1: Database Foundation** and work through the stories sequentially. Each story builds on the previous one.

**Timeline Estimate:** 5-7 weeks for complete module (all 17 stories)

**First Milestone:** Story 1.1 complete with database operational and tests passing.

---

**Created:** 2025-10-20  
**Branch:** feature/client-management  
**Status:** ✅ Ready to implement
