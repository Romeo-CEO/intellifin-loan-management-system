# Credit Assessment Microservice - Product Requirements Document

## Document Information

**Project**: IntelliFin Loan Management System - Credit Assessment Microservice  
**Document Type**: Brownfield Enhancement PRD  
**Version**: 1.0  
**Date**: 2025-10-17  
**Author**: Product Manager  
**Status**: Draft - Ready for Review

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2025-10-17 | 1.0 | Initial PRD for Credit Assessment microservice extraction and enhancement | PM |

---

## 1. Introduction: Project Analysis and Context

### 1.1 Analysis Source

**Analysis Source**: Fresh IDE-based analysis combined with existing documentation

**Reference Documents**:
- Brownfield Architecture Document: `docs/domains/credit-assessment/brownfield-architecture.md`
- Credit Scoring Methodology: `docs/domains/credit-assessment/credit-scoring-methodology.md`
- Risk Assessment Framework: `docs/domains/credit-assessment/risk-assessment-framework.md`
- Collateral Management: `docs/domains/credit-assessment/collateral-management.md`
- Credit Assessment Process: `docs/domains/loan-origination/credit-assessment-process.md`

### 1.2 Current Project State

**Existing System Overview**:

IntelliFin is a comprehensive Loan Management System for Zambian microfinance operations, currently serving two core loan products:
1. **Government Employee Payroll Loans** - PMEC-integrated salary deduction loans
2. **SME Asset-Backed Loans** - Collateral-based business lending

**Current Credit Assessment State**:
- Credit assessment logic is **embedded within the Loan Origination Service**
- Basic scoring functionality exists in `CreditAssessmentService.cs`
- Risk rules are hard-coded in `RiskCalculationEngine.cs`
- DMN decision table provides basic risk grading (A-F)
- TransUnion integration is mocked (not production-ready)
- No Vault-based configuration
- Limited audit trail capabilities
- No event-driven KYC monitoring
- Basic affordability analysis without deep PMEC integration

**Technology Stack**:
- .NET 8.0 / ASP.NET Core
- PostgreSQL 15 (shared database)
- Camunda 8.x (workflow orchestration)
- Redis 7.x (caching)
- RabbitMQ (event bus - to be utilized)
- Vault (to be integrated for configuration)
- Serilog (structured logging)

### 1.3 Available Documentation Analysis

✅ **Available Documentation**:
- [x] Tech Stack Documentation (from architecture analysis)
- [x] Source Tree/Architecture (brownfield architecture doc)
- [x] Coding Standards (partial - C# standards in place)
- [x] API Documentation (partial - existing service interfaces)
- [x] External API Documentation (TransUnion specs available)
- [ ] UX/UI Guidelines (Not applicable - backend service)
- [x] Technical Debt Documentation (comprehensive in brownfield doc)

**Documentation Status**: Comprehensive technical documentation exists. All critical architectural information has been captured in the brownfield architecture document.

### 1.4 Enhancement Scope Definition

**Enhancement Type**: ✅ **New Feature Addition** + **Integration with New Systems** + **Architecture Enhancement**

This is a **significant architectural enhancement** involving:
- Service extraction and standalone deployment
- New external system integrations
- Configuration management system integration
- Event-driven architecture implementation

**Enhancement Description**:

Transform the embedded credit assessment functionality into a production-ready, standalone microservice that serves as IntelliFin's intelligent lending brain. The new service will feature Vault-based rule configuration, automated scoring with explainability, comprehensive audit trails, event-driven KYC monitoring, and seamless integration with existing services while enabling future AI/ML capabilities.

**Impact Assessment**: ✅ **Major Impact** - Architectural changes required

This enhancement requires:
- New microservice creation and deployment
- Refactoring of existing Loan Origination Service
- Multiple new external service integrations
- Database schema enhancements
- Camunda workflow modifications
- Event bus integration
- Comprehensive testing and migration strategy

### 1.5 Goals and Background Context

**Goals**:

1. **Establish Credit Assessment as an independent, reusable microservice** that can serve multiple lending contexts
2. **Enable business-driven rule configuration** through Vault, eliminating code deployments for policy changes
3. **Achieve complete decision traceability** with comprehensive audit trails to AdminService
4. **Automate credit score invalidation** when KYC status changes via event-driven monitoring
5. **Integrate production-ready TransUnion API** with smart routing for cost optimization
6. **Provide deep affordability analysis** through real-time PMEC payroll data integration
7. **Support intelligent, explainable decisions** that credit officers and regulators can understand and trust
8. **Enable graceful degradation** with manual fallback when the service is unavailable
9. **Position IntelliFin for AI/ML scoring** with extensible architecture for future statistical models
10. **Maintain BoZ compliance** with transparent, auditable risk assessment processes

**Background Context**:

With the Loan Origination module successfully deployed and operational for application, approval, and agreement generation workflows, IntelliFin is ready to differentiate itself through intelligent, data-driven credit assessment. The current embedded assessment logic served well for initial deployment but now constrains business agility and regulatory transparency.

The business team requires the ability to adjust risk policies—such as maximum loan-to-income ratios, debt-to-income thresholds, and credit score requirements—without developer intervention. Regulators (Bank of Zambia) demand complete auditability of every credit decision. As IntelliFin scales and adds new loan products, a reusable, configurable assessment engine becomes critical infrastructure.

This enhancement positions the Credit Assessment module as IntelliFin's core intelligence layer, preparing for integration with mobile money transaction history, PMEC payroll analytics, and future AI scoring models while maintaining the transparency and compliance that microfinance in Zambia requires.

---

## 2. Requirements

### 2.1 Functional Requirements

**FR1**: The system shall provide a standalone REST API for credit assessment that can be invoked by Loan Origination Service, Collections Service, or any future service requiring credit evaluation.

**FR2**: The system shall retrieve verified KYC profile data, employment information, and verification status from Client Management Service API before performing assessment.

**FR3**: The system shall integrate with TransUnion Zambia API to retrieve credit bureau scores and history for first-time applicants, with smart caching to minimize API costs.

**FR4**: The system shall verify government employee status and retrieve salary details, existing deductions, and employment tenure from PMEC API for payroll loan assessments.

**FR5**: The system shall load all scoring rules, weights, and thresholds from HashiCorp Vault on service startup and refresh configuration on demand without service restart.

**FR6**: The system shall evaluate credit applications against configurable rule sets, with separate rule configurations for PAYROLL and BUSINESS loan products.

**FR7**: The system shall calculate composite risk scores based on weighted rule evaluations, with each rule contributing a pass/fail score multiplied by its configured weight.

**FR8**: The system shall determine risk grades (A, B, C, D, F) based on composite scores and configurable grade thresholds.

**FR9**: The system shall produce credit decisions (Approved, Conditional, ManualReview, Rejected) based on risk grade and configurable decision matrix.

**FR10**: The system shall calculate debt-to-income (DTI) ratio incorporating existing obligations from TransUnion, proposed loan payment, and verified income.

**FR11**: The system shall calculate affordability analysis showing maximum affordable payment, recommended loan amount, and disposable income after all obligations.

**FR12**: The system shall generate human-readable explanations for every credit decision, listing which rules were triggered, their impact, and the rationale for the final decision.

**FR13**: The system shall log every assessment request, rule evaluation, and decision to AdminService as structured audit events for regulatory compliance.

**FR14**: The system shall include Vault configuration version, assessed-by user ID, and timestamp in every assessment record for complete traceability.

**FR15**: The system shall subscribe to KYC status change events (KYCExpired, KYCRevoked, KYCUpdated) via RabbitMQ and automatically invalidate affected credit assessments.

**FR16**: The system shall support manual override of automated decisions by authorized credit officers, capturing override reason, user ID, and timestamp.

**FR17**: The system shall implement a Camunda external task worker that receives assessment requests from loan approval workflows and returns structured decision payloads.

**FR18**: The system shall gracefully handle service unavailability by allowing Camunda workflows to route to manual credit officer review when the assessment service is down.

**FR19**: The system shall cache assessment results in Redis with configurable TTL to avoid redundant evaluations for the same application.

**FR20**: The system shall provide API endpoints to retrieve assessment history for a client, view individual assessment details, and query assessments by loan application ID.

**FR21**: The system shall validate KYC verification status before performing assessment and reject requests where KYC has expired or is invalid.

**FR22**: The system shall support different rule evaluation contexts for initial application assessment vs. renewal assessment vs. modification assessment.

**FR23**: The system shall track which rules passed/failed for each assessment and store detailed rule evaluation results including input values and calculated outputs.

**FR24**: The system shall provide configuration management APIs (admin-only) to view current rule configuration version and trigger configuration refresh from Vault.

**FR25**: The system shall implement circuit breaker patterns for external service calls (TransUnion, PMEC, Client Management) to prevent cascading failures.

### 2.2 Non-Functional Requirements

**NFR1**: The system shall complete 95% of credit assessments within 5 seconds from API request to decision response, excluding external API latency.

**NFR2**: The system shall achieve 99.9% availability (approximately 8.76 hours downtime per year) with proper monitoring and health checks.

**NFR3**: The system shall support concurrent assessment of 100 loan applications per second with horizontal scaling capability.

**NFR4**: The system shall encrypt all personally identifiable information (PII) at rest in the database and in Redis cache.

**NFR5**: The system shall use TLS 1.3 for all external API communications (TransUnion, PMEC, Client Management, AdminService).

**NFR6**: The system shall implement JWT bearer token authentication and require `credit:assess` permission for assessment API access.

**NFR7**: The system shall require `credit:override` permission for manual override operations, separate from basic assessment permissions.

**NFR8**: The system shall mask sensitive data (credit scores, income, debt amounts) in application logs while maintaining full audit trail in AdminService.

**NFR9**: The system shall implement structured logging with Serilog, using correlation IDs to trace requests across microservices.

**NFR10**: The system shall expose Prometheus-compatible metrics for monitoring assessment rate, latency, decision distribution, and external API health.

**NFR11**: The system shall implement health check endpoints (`/health/live` and `/health/ready`) for Kubernetes liveness and readiness probes.

**NFR12**: The system shall support deployment via Docker containers with configuration via environment variables and Vault integration.

**NFR13**: The system shall maintain backward compatibility with existing `CreditAssessment` database entity structure while adding new audit fields.

**NFR14**: The system shall complete database migrations without downtime using blue-green deployment or rolling update strategy.

**NFR15**: The system shall implement request rate limiting (100 requests/minute per client) to prevent abuse and ensure fair resource allocation.

**NFR16**: The system shall retry failed external API calls with exponential backoff (max 3 retries) before returning service unavailable error.

**NFR17**: The system shall cache Vault configuration for 5 minutes to minimize Vault API calls while allowing timely rule updates.

**NFR18**: The system shall implement comprehensive unit test coverage (minimum 80%) and integration tests for all critical paths.

**NFR19**: The system shall support A/B testing of rule configurations by allowing different rule set versions to be applied to different client segments.

**NFR20**: The system shall maintain API response times within 200ms (p95) for cached assessment retrievals (excluding initial assessment calculation).

### 2.3 Compatibility Requirements

**CR1: API Compatibility** - The new Credit Assessment Service must provide API endpoints that Loan Origination Service can migrate to without breaking changes. During transition, both embedded and service-based assessment methods will coexist, controlled by feature flag.

**CR2: Database Schema Compatibility** - All enhancements to the `credit_assessments` table must be additive (new columns with defaults) to avoid breaking existing queries from Loan Origination Service, Collections Service, and Reporting Service.

**CR3: Event Schema Compatibility** - All audit events published to AdminService must maintain backward-compatible schema structure, adding new fields as optional rather than changing existing field types or removing fields.

**CR4: Integration Compatibility** - The service must work within the existing IntelliFin service mesh, using the same authentication (JWT), authorization (permission-based), and service discovery patterns as other microservices.

**CR5: Workflow Compatibility** - Camunda workflow integration must support both synchronous (direct API call) and asynchronous (external task worker) invocation patterns to accommodate different workflow designs without requiring workflow redeployment.

**CR6: Configuration Compatibility** - Vault rule configuration format must be versioned, and the service must gracefully handle configuration updates without dropping in-flight assessment requests or requiring service restart.

**CR7: Monitoring Compatibility** - The service must expose metrics in the same format (Prometheus) and logging structure (JSON with correlation IDs) as other IntelliFin services for unified observability.

**CR8: Deployment Compatibility** - The service must deploy to the existing Kubernetes cluster using the same CI/CD pipeline, Helm charts structure, and deployment patterns as other microservices.

---

## 3. Technical Constraints and Integration Requirements

### 3.1 Existing Technology Stack

**Languages**: C# 12 (.NET 8.0)  
**Frameworks**: ASP.NET Core 8.0 (Minimal APIs for REST endpoints)  
**Database**: PostgreSQL 15 (shared `LmsDbContext` with Entity Framework Core)  
**Workflow**: Camunda 8.x (Zeebe workflow engine with external task workers)  
**Configuration**: HashiCorp Vault (secrets and dynamic configuration management)  
**Messaging**: RabbitMQ (event bus for KYC status events)  
**Cache**: Redis 7.x (assessment result caching, distributed locking)  
**Logging**: Serilog with structured JSON output  
**Monitoring**: Prometheus metrics + Grafana dashboards  
**Container Platform**: Docker + Kubernetes  
**External Dependencies**: 
- TransUnion Zambia Credit Bureau API
- PMEC (Public Service Management) API
- Client Management Service (internal)
- AdminService (internal - audit trail)
- IdentityService (internal - authentication)

### 3.2 Integration Approach

**Database Integration Strategy**:
- Extend existing `LmsDbContext` with new tables: `credit_assessment_audit`, `rule_evaluations`, `assessment_config_versions`
- Add nullable columns to existing `credit_assessments` table for new audit fields
- Use EF Core migrations for schema updates
- Implement repository pattern for data access
- Share connection pool configuration with other services

**API Integration Strategy**:
- REST API with OpenAPI/Swagger documentation
- Reusable HTTP client library (`IntelliFin.Shared.ApiClients`) for Client Management, AdminService
- Circuit breaker pattern (Polly) for external API resilience
- Standardized error response format matching other IntelliFin services
- API versioning strategy (URL path: `/api/v1/credit-assessment`)

**Event Bus Integration Strategy**:
- RabbitMQ consumer for KYC status change events
- Use `IntelliFin.Shared.Messaging` library for event serialization/deserialization
- Dead letter queue for failed event processing
- Idempotent event handlers to handle duplicate messages
- Event correlation with assessment records

**Vault Integration Strategy**:
- AppRole authentication for service identity
- Periodic configuration refresh (every 5 minutes)
- Configuration caching to minimize Vault API calls
- Graceful fallback to last-known-good configuration if Vault is unavailable
- Version tracking for rule configuration changes

### 3.3 Code Organization and Standards

**File Structure Approach**:
```
apps/IntelliFin.CreditAssessmentService/
├── Controllers/          # REST API endpoints
├── Services/            
│   ├── Core/            # Core business logic
│   ├── Integration/     # External service clients
│   ├── Configuration/   # Vault and config management
│   └── Events/          # Event handlers
├── Workers/             # Camunda external task workers
├── Models/              # DTOs and request/response models
│   ├── Requests/
│   ├── Responses/
│   └── Configuration/
├── Data/
│   └── Repositories/    # Data access layer
├── BPMN/                # Workflow definitions
└── Program.cs           # Service configuration
```

**Naming Conventions**:
- PascalCase for classes, methods, properties
- camelCase for local variables, parameters
- Prefix interfaces with `I` (e.g., `ICreditAssessmentService`)
- Suffix DTOs with `Request`, `Response`, `Dto` (e.g., `AssessmentRequest`)
- Suffix external clients with `Client` (e.g., `TransUnionClient`)

**Coding Standards**:
- Follow existing C# coding conventions in IntelliFin codebase
- Use async/await for all I/O operations
- Implement structured logging with contextual information
- Use dependency injection for all service dependencies
- Write XML documentation comments for public APIs
- Follow SOLID principles and clean architecture patterns

**Documentation Standards**:
- OpenAPI/Swagger annotations for all API endpoints
- README.md in service root with setup instructions
- Architecture decision records (ADRs) for significant technical choices
- Inline code comments for complex business logic
- API integration guides for consumers

### 3.4 Deployment and Operations

**Build Process Integration**:
- Use existing .NET build pipeline in Azure DevOps / GitHub Actions
- Multi-stage Dockerfile (build → test → publish → runtime)
- Automated test execution as part of build (unit + integration tests)
- Docker image tagging with semantic versioning
- Vulnerability scanning with Trivy before deployment

**Deployment Strategy**:
- Blue-green deployment for zero-downtime updates
- Kubernetes deployment with rolling update strategy
- Helm chart for configuration management
- ConfigMap for non-sensitive configuration
- Vault secrets injection for sensitive configuration
- Health checks for liveness and readiness probes

**Monitoring and Logging**:
- Prometheus metrics exported on `/metrics` endpoint
- Grafana dashboards for service health, latency, throughput
- Serilog JSON logs to Elasticsearch via Fluentd
- Correlation IDs for distributed tracing
- Alerts for high error rate, high latency, service unavailability

**Configuration Management**:
- Environment-specific configuration in Vault
- Feature flags for gradual rollout (e.g., enable new service for subset of applications)
- Configuration versioning and audit trail
- Hot reload capability for rule configuration changes
- Secrets rotation support

### 3.5 Risk Assessment and Mitigation

**Technical Risks**:

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Vault configuration errors causing incorrect credit decisions | High | Medium | Configuration validation on load, version tracking, dry-run testing mode |
| External API failures (TransUnion, PMEC) causing assessment delays | High | Medium | Circuit breakers, fallback logic, graceful degradation to manual review |
| Event processing lag causing stale assessments | Medium | Low | Monitor event lag metrics, implement catch-up processing, alert on delays |
| Database migration failures during deployment | High | Low | Comprehensive testing in staging, rollback plan, additive-only schema changes |
| Service unavailability blocking loan origination | High | Low | Camunda workflow fallback to manual review, implement high availability |

**Integration Risks**:

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Client Management API changes breaking assessment data retrieval | Medium | Medium | API versioning, contract testing, backward compatibility guarantees |
| Camunda worker failures causing workflow blockage | High | Low | Worker health monitoring, automatic restart, manual task fallback |
| RabbitMQ message loss causing assessment invalidation failures | Medium | Low | Persistent messages, dead letter queues, reconciliation jobs |
| Vault unavailability preventing service startup | High | Low | Configuration caching, last-known-good fallback, startup resilience |

**Deployment Risks**:

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Feature flag misconfiguration causing dual assessment execution | Medium | Low | Thorough testing, gradual rollout, monitoring for duplicate assessments |
| Network policy misconfiguration blocking service communication | High | Low | Pre-deployment validation, automated smoke tests, quick rollback capability |
| Resource contention with Loan Origination Service | Medium | Medium | Resource quotas, auto-scaling, performance testing under load |

**Mitigation Strategies**:

1. **Comprehensive Testing**: Unit tests (80% coverage), integration tests, end-to-end tests, performance tests
2. **Gradual Rollout**: Feature flag for phased migration (10% → 50% → 100% of loan applications)
3. **Monitoring & Alerting**: Real-time metrics, automated alerts, on-call runbooks
4. **Rollback Plan**: Automated rollback triggers, feature flag kill switch, database migration rollback scripts
5. **Documentation**: Runbooks for incident response, architecture decision records, API integration guides

---

## 4. Epic and Story Structure

### 4.1 Epic Approach

**Epic Structure Decision**: **Single Comprehensive Epic**

**Rationale**: This enhancement represents a cohesive architectural transformation—extracting credit assessment into a microservice with integrated capabilities (Vault, events, audit). While substantial, it delivers a single unified capability (intelligent credit assessment) rather than multiple independent features. Breaking into multiple epics would create artificial boundaries and complicate dependency management.

The epic will be structured into **6 sequential phases** that minimize risk to the existing system:

1. **Phase 1: Foundation** - Service scaffolding, basic API, database
2. **Phase 2: Core Logic Migration** - Move assessment logic with parity
3. **Phase 3: External Integrations** - TransUnion, PMEC, Client Management
4. **Phase 4: Configuration & Rules** - Vault-based rule engine
5. **Phase 5: Events & Audit** - KYC monitoring, AdminService integration
6. **Phase 6: Workflow Integration** - Camunda worker, Loan Origination cutover

Each phase delivers testable, valuable increments while maintaining existing system functionality.

---

## 5. Epic 1: Credit Assessment Microservice - Intelligent Scoring Engine

**Epic Goal**: Transform embedded credit assessment into a production-ready, configurable, auditable microservice that serves as IntelliFin's lending intelligence brain, enabling business-driven risk policies, complete decision traceability, and extensibility for future AI capabilities.

**Integration Requirements**:
- Maintain Loan Origination Service functionality during migration
- Provide backward-compatible API for existing assessment queries
- Support parallel operation (old embedded + new service) during transition
- Ensure zero data loss during database schema migration
- Preserve existing audit trail continuity

---

### Story 1.1: Credit Assessment Service Scaffolding and Infrastructure Setup

**As a** DevOps Engineer  
**I want** to create the Credit Assessment Service project structure with deployment configuration  
**So that** the new microservice has a solid foundation matching IntelliFin's infrastructure standards

#### Acceptance Criteria

1. Create new ASP.NET Core 8.0 project `IntelliFin.CreditAssessmentService` in `apps/` directory
2. Configure shared `LmsDbContext` reference from `IntelliFin.Shared.DomainModels`
3. Set up dependency injection with Serilog, Prometheus metrics, health checks
4. Create Dockerfile with multi-stage build (build → test → runtime)
5. Create Kubernetes deployment manifest and Helm chart with ConfigMap and secrets
6. Configure appsettings.json with environment-specific configuration sections
7. Implement health check endpoints (`/health/live`, `/health/ready`)
8. Add Prometheus metrics endpoint (`/metrics`) with basic HTTP request metrics
9. Create README.md with setup instructions and architecture overview
10. Successfully deploy to development Kubernetes cluster and verify health checks

#### Integration Verification

**IV1**: Existing Loan Origination Service continues operating without any changes  
**IV2**: New service deploys successfully alongside existing services without network conflicts  
**IV3**: Health checks respond correctly and service is discoverable by Kubernetes  

---

### Story 1.2: Database Schema Enhancement for Audit and Configuration Tracking

**As a** Backend Developer  
**I want** to extend the credit assessment database schema with audit and configuration tracking  
**So that** the service can store detailed rule evaluations and track configuration versions

#### Acceptance Criteria

1. Create EF Core migration adding columns to `credit_assessments` table:
   - `assessed_by_user_id` (UUID, nullable)
   - `decision_category` (varchar, nullable)
   - `triggered_rules` (JSONB, nullable)
   - `manual_override_by_user_id` (UUID, nullable)
   - `manual_override_reason` (text, nullable)
   - `manual_override_at` (timestamp, nullable)
   - `is_valid` (boolean, default true)
   - `invalid_reason` (varchar, nullable)
   - `vault_config_version` (varchar, nullable)
2. Create new table `credit_assessment_audit` for detailed audit trail
3. Create new table `rule_evaluations` for individual rule results
4. Create new table `assessment_config_versions` for Vault configuration tracking
5. Add appropriate indexes on foreign keys and query columns
6. Test migration in development environment with existing data
7. Verify migration rollback script works correctly
8. Update `CreditAssessment` entity class with new properties
9. Document schema changes in architecture document
10. Migration applies successfully without downtime using blue-green deployment

#### Integration Verification

**IV1**: Existing queries from Loan Origination, Collections, and Reporting services continue working with new schema (backward compatibility)  
**IV2**: New columns have appropriate default values so existing application code doesn't break  
**IV3**: Database migration completes within 5 seconds on test dataset of 10,000 assessments  

---

### Story 1.3: Core Assessment Service API with Basic Endpoints

**As a** Backend Developer  
**I want** to create the REST API endpoints for credit assessment operations  
**So that** Loan Origination Service can invoke assessments via HTTP

#### Acceptance Criteria

1. Create `CreditAssessmentController` with minimal API endpoints
2. Implement `POST /api/v1/credit-assessment/assess` endpoint
3. Implement `GET /api/v1/credit-assessment/{assessmentId}` endpoint
4. Implement `GET /api/v1/credit-assessment/client/{clientId}/latest` endpoint
5. Define request/response DTOs (`AssessmentRequest`, `AssessmentResponse`, `DecisionPayload`)
6. Add OpenAPI/Swagger annotations for API documentation
7. Implement JWT bearer token authentication
8. Implement permission check (`credit:assess`) via IdentityService integration
9. Add request validation with FluentValidation
10. Return structured error responses matching IntelliFin error format

#### Integration Verification

**IV1**: API endpoints accessible with valid JWT token from IdentityService  
**IV2**: API documentation renders correctly in Swagger UI  
**IV3**: Unauthorized requests (no token or insufficient permissions) return 401/403 as expected  

---

### Story 1.4: Migrate Core Credit Assessment Logic with Parity

**As a** Backend Developer  
**I want** to migrate the existing `CreditAssessmentService` logic to the new microservice  
**So that** the new service has functional parity with the embedded implementation

#### Acceptance Criteria

1. Copy and refactor `CreditAssessmentService.cs` from Loan Origination Service
2. Copy and adapt `RiskCalculationEngine.cs` (keeping hard-coded rules temporarily)
3. Implement `AffordabilityAnalysisService` with DTI calculation logic
4. Create repository pattern for `CreditAssessment` entity data access
5. Implement basic credit bureau data retrieval (mocked initially)
6. Implement affordability assessment logic
7. Implement risk calculation and grade determination
8. Generate assessment result with explanation
9. Persist assessment to database
10. Return structured `AssessmentResponse` with decision payload

#### Integration Verification

**IV1**: Assessment results match output from existing embedded service (verified with same input data)  
**IV2**: Risk grades calculated are identical to DMN decision table results for test cases  
**IV3**: Database records written have same structure as existing assessments (backward compatible)  

---

### Story 1.5: Client Management API Integration for KYC and Employment Data

**As a** Backend Developer  
**I want** to integrate with Client Management Service to retrieve verified KYC and employment data  
**So that** assessments use real client verification status and income information

#### Acceptance Criteria

1. Create `ClientManagementClient` with HTTP client configuration
2. Implement `GetKycProfileAsync(clientId)` method calling Client Management API
3. Implement `GetEmploymentDetailsAsync(clientId)` method
4. Implement `GetVerificationStatusAsync(clientId)` method
5. Add circuit breaker pattern (Polly) with 3 retries and 30-second timeout
6. Handle API failures gracefully with fallback to manual review flag
7. Map Client Management response DTOs to internal assessment data models
8. Add logging for integration calls with correlation IDs
9. Validate KYC verification status before allowing assessment
10. Reject assessment requests when KYC is expired or invalid

#### Integration Verification

**IV1**: Client Management API calls succeed with valid client IDs from test data  
**IV2**: Circuit breaker activates correctly when Client Management Service is unavailable (simulated)  
**IV3**: Assessment fails gracefully with clear error message when KYC data is unavailable  

---

### Story 1.6: TransUnion Credit Bureau API Integration with Smart Routing

**As a** Backend Developer  
**I want** to integrate with TransUnion Zambia API for credit bureau score retrieval  
**So that** first-time applicants get real credit bureau data in assessments

#### Acceptance Criteria

1. Create `TransUnionClient` implementing TransUnion API specification
2. Implement smart routing logic: check if client is first-time applicant
3. For first-time applicants, call TransUnion API to retrieve credit score and history
4. For existing clients, skip bureau call and use historical data
5. Cache bureau results in Redis with 90-day TTL
6. Implement API authentication using configured API key from Vault
7. Handle API errors (timeout, unavailable) with retry logic
8. Parse TransUnion response and map to internal `CreditBureauData` model
9. Log API call metrics (success rate, latency, cost tracking)
10. Return null for bureau data if API unavailable (triggers manual review)

#### Integration Verification

**IV1**: TransUnion API calls succeed for test NRCs in sandbox environment  
**IV2**: Cached bureau data retrieved from Redis on subsequent assessment for same client (no duplicate API calls)  
**IV3**: Assessment continues with "no bureau data" flag when TransUnion API is unavailable  

---

### Story 1.7: PMEC Government Employee Verification and Payroll Integration

**As a** Backend Developer  
**I want** to integrate with PMEC API to verify government employee status and retrieve payroll data  
**So that** payroll loan assessments use accurate employment and deduction information

#### Acceptance Criteria

1. Create `PMECClient` for PMEC API integration
2. Implement `VerifyEmploymentAsync(nrc)` method to confirm government employee status
3. Implement `GetSalaryDetailsAsync(nrc)` to retrieve gross salary, net salary, and payment history
4. Implement `GetExistingDeductionsAsync(nrc)` to retrieve all active payroll deductions
5. Calculate actual disposable income after PMEC deductions
6. Use PMEC data for affordability analysis instead of declared income
7. Handle PMEC API unavailability with fallback to declared income (with warning flag)
8. Validate employment tenure from PMEC data
9. Cache PMEC responses for 24 hours (data refreshes daily)
10. Log discrepancies between declared income and PMEC salary

#### Integration Verification

**IV1**: PMEC API calls succeed for test government employee NRCs  
**IV2**: Affordability calculation uses PMEC salary data when available, falls back to declared income when PMEC unavailable  
**IV3**: Assessment captures which income source was used (PMEC vs declared) for audit purposes  

---

### Story 1.8: Vault Integration for Rule Configuration Management

**As a** Backend Developer  
**I want** to integrate with HashiCorp Vault to load credit scoring rules and thresholds  
**So that** business teams can update risk policies without code deployment

#### Acceptance Criteria

1. Create `VaultConfigService` with AppRole authentication
2. Implement configuration loading from Vault path `secret/intellifin/credit-assessment/config`
3. Parse JSON rule configuration into strongly-typed C# models (`ScoringRuleConfig`, `ThresholdConfig`)
4. Validate configuration schema on load (required fields, data types, value ranges)
5. Implement configuration caching with 5-minute refresh interval
6. Support manual configuration refresh via admin API endpoint `POST /api/v1/admin/config/refresh`
7. Track configuration version in assessment records
8. Log configuration changes with before/after comparison
9. Implement graceful fallback to last-known-good configuration if Vault unavailable
10. Provide configuration health check showing current version and last refresh time

#### Integration Verification

**IV1**: Service starts successfully with valid Vault configuration  
**IV2**: Configuration refresh happens automatically every 5 minutes without service disruption  
**IV3**: Service continues operating with cached configuration when Vault is temporarily unavailable  

---

### Story 1.9: Vault-Based Rule Engine with Dynamic Rule Evaluation

**As a** Backend Developer  
**I want** to replace hard-coded risk rules with a Vault-driven rule evaluation engine  
**So that** business can configure risk policies dynamically for different products

#### Acceptance Criteria

1. Create `VaultRuleEngine` service that evaluates rules from Vault configuration
2. Implement rule evaluation context builder combining client data, bureau data, affordability data
3. Support comparison operators: `<=`, `>=`, `<`, `>`, `==` in rule conditions
4. Evaluate mathematical expressions in rule conditions (e.g., `requestedAmount / monthlyIncome`)
5. Calculate pass/fail score for each rule based on evaluation result
6. Apply rule weight to calculate weighted score contribution
7. Sum all weighted scores to produce composite risk score
8. Determine risk grade from composite score using grade thresholds from Vault
9. Determine decision (Approved/Conditional/ManualReview/Rejected) using decision matrix from Vault
10. Generate detailed rule evaluation results showing each rule's input, output, and contribution

#### Integration Verification

**IV1**: Rule engine produces same risk grades as existing DMN decision table for test cases  
**IV2**: Composite scoring calculation is accurate for payroll and business product rules  
**IV3**: Vault configuration changes (e.g., increasing DTI threshold from 40% to 45%) reflected in assessments after configuration refresh  

---

### Story 1.10: Decision Explainability and Human-Readable Reasoning

**As a** Backend Developer  
**I want** to generate human-readable explanations for every credit decision  
**So that** credit officers and clients understand why applications were approved or rejected

#### Acceptance Criteria

1. Implement explanation generator service that converts rule evaluation results to natural language
2. List all rules evaluated with pass/fail status and reason
3. Highlight top 3 most impactful rules (by absolute weighted score contribution)
4. Generate decision summary: "Approved because composite score 720 falls in Grade B (650-749)"
5. Include affordability summary: "DTI ratio 35% within 40% threshold, affordable payment ZMW 3,500"
6. List any triggered conditions for conditional approvals
7. Provide clear rejection reasons when decision is "Rejected"
8. Include data sources used (e.g., "TransUnion credit score: 680, PMEC salary: ZMW 12,000")
9. Format explanation in markdown for easy rendering in UI
10. Store full explanation in `score_explanation` field of assessment record

#### Integration Verification

**IV1**: Explanations are comprehensible to non-technical users (validated by credit officers)  
**IV2**: All key decision factors are included in explanation  
**IV3**: Explanation includes both positive factors (what helped) and negative factors (what hurt)  

---

### Story 1.11: AdminService Audit Trail Integration for Decision Traceability

**As a** Backend Developer  
**I want** to log all credit assessment events to AdminService as structured audit records  
**So that** every decision is traceable for regulatory compliance and forensic analysis

#### Acceptance Criteria

1. Create `AdminServiceClient` for audit event publishing
2. Define audit event types: `CreditAssessmentInitiated`, `RuleEvaluated`, `DecisionMade`, `ManualOverrideApplied`, `AssessmentInvalidated`
3. Publish `CreditAssessmentInitiated` event when assessment request received
4. Publish `RuleEvaluated` event for each rule with input/output/result
5. Publish `DecisionMade` event with final risk grade, score, decision, explanation
6. Include user ID, loan application ID, client ID, timestamp in all events
7. Include Vault configuration version in decision events
8. Include correlation ID linking all events for single assessment
9. Implement async fire-and-forget pattern for audit publishing (don't block assessment)
10. Implement dead letter queue for failed audit events with retry logic

#### Integration Verification

**IV1**: All assessment events appear in AdminService audit log with correct correlation IDs  
**IV2**: Assessment continues successfully even if AdminService is temporarily unavailable (events queued)  
**IV3**: Event payloads contain sufficient detail for forensic reconstruction of decision  

---

### Story 1.12: KYC Status Event Subscription and Assessment Invalidation

**As a** Backend Developer  
**I want** to subscribe to KYC status change events and automatically invalidate affected assessments  
**So that** credit decisions are always based on current, valid KYC verification

#### Acceptance Criteria

1. Create `KYCStatusEventHandler` consuming RabbitMQ events from Client Management Service
2. Subscribe to event types: `KYCExpired`, `KYCRevoked`, `KYCUpdated`
3. For `KYCExpired` and `KYCRevoked` events, find all active assessments for affected client
4. Update assessments: set `is_valid = false`, set `invalid_reason` with event details
5. Publish audit event to AdminService for each invalidated assessment
6. For `KYCUpdated` events, check if verification status improved and log for potential reassessment
7. Implement idempotent event handling (duplicate events don't cause duplicate invalidations)
8. Log event processing metrics (latency, success rate)
9. Implement dead letter queue for failed event processing
10. Provide admin API to manually invalidate/revalidate assessments

#### Integration Verification

**IV1**: When KYC expires in Client Management Service, assessment is invalidated within 1 minute  
**IV2**: Loan Origination Service attempting to use invalidated assessment receives clear error  
**IV3**: Event handler processes duplicate events idempotently (no duplicate invalidations)  

---

### Story 1.13: Manual Override Workflow for Credit Officers

**As a** Credit Officer  
**I want** to manually override automated credit decisions with documented justification  
**So that** exceptional cases can be approved or rejected based on human judgment

#### Acceptance Criteria

1. Implement `POST /api/v1/credit-assessment/{assessmentId}/manual-override` endpoint
2. Require `credit:override` permission (separate from basic assess permission)
3. Accept override request with: decision (Approved/Rejected), reason (required, min 20 chars), user ID
4. Validate that assessment exists and is not already overridden
5. Update assessment record with override details and timestamp
6. Preserve original automated decision for comparison
7. Publish `ManualOverrideApplied` audit event to AdminService
8. Log override metrics (override rate, override by decision type)
9. Return updated assessment with override details clearly indicated
10. Support override reversal (undo) within 24 hours for error correction

#### Integration Verification

**IV1**: Credit officers with `credit:override` permission can successfully override decisions  
**IV2**: Users without override permission receive 403 Forbidden error  
**IV3**: Override audit trail includes both automated decision and override decision for comparison  

---

### Story 1.14: Camunda External Task Worker for Workflow Integration

**As a** Backend Developer  
**I want** to implement a Camunda external task worker that processes assessment requests from workflows  
**So that** credit assessment integrates seamlessly with loan approval workflows

#### Acceptance Criteria

1. Create `CreditAssessmentWorker` implementing `IExternalTaskWorker` interface
2. Subscribe to Camunda external task topic `credit-assessment`
3. Extract loan application ID, client ID, and assessment parameters from task variables
4. Invoke internal `CreditAssessmentService.PerformAssessmentAsync()` method
5. On success, complete task with assessment result variables (decision, risk grade, score)
6. On failure, handle BPMN error `SERVICE_UNAVAILABLE` to route workflow to manual review
7. Implement task timeout handling (30 seconds max processing time)
8. Implement worker health monitoring and automatic restart on failure
9. Configure worker concurrency (5 concurrent tasks)
10. Log worker metrics (tasks processed, success rate, average processing time)

#### Integration Verification

**IV1**: Worker successfully picks up tasks from Camunda and completes them with assessment results  
**IV2**: When service throws exception, workflow routes to manual review task correctly  
**IV3**: Worker processes tasks concurrently without race conditions or duplicate assessments  

---

### Story 1.15: Camunda Workflow Definition for Credit Assessment Process

**As a** Business Analyst  
**I want** a dedicated Camunda workflow for credit assessment with fallback handling  
**So that** the assessment process is visible, auditable, and gracefully handles service unavailability

#### Acceptance Criteria

1. Create `credit_assessment_v1.bpmn` workflow definition
2. Start event: receives loan application ID and client ID as process variables
3. Service task: `credit-assessment` external task with 30-second timeout
4. Boundary error event: catches `SERVICE_UNAVAILABLE` error
5. Exclusive gateway: routes based on service availability
6. Happy path: wait for assessment result → gateway routing by decision (Approved/Conditional/ManualReview)
7. Error path: automatic user task "Manual Credit Officer Review"
8. Record assessment result in process variables for downstream tasks
9. Deploy workflow to Camunda development environment
10. Test workflow execution with both success and failure scenarios

#### Integration Verification

**IV1**: Workflow successfully invokes assessment worker and receives decision  
**IV2**: When worker simulates failure, workflow routes to manual review task as expected  
**IV3**: Workflow variables contain complete assessment result for use in downstream loan approval steps  

---

### Story 1.16: Feature Flag Implementation for Gradual Migration

**As a** DevOps Engineer  
**I want** to implement feature flag in Loan Origination Service to control which assessment method is used  
**So that** we can gradually migrate from embedded assessment to microservice

#### Acceptance Criteria

1. Add configuration setting `UseNewCreditAssessmentService` (boolean, default false)
2. Modify `LoanApplicationService` to check feature flag before assessment
3. When flag is false, use existing embedded `CreditAssessmentService` (current behavior)
4. When flag is true, invoke new Credit Assessment Service API via HTTP
5. Implement HTTP client with circuit breaker for new service calls
6. Log which assessment method was used for each application
7. Implement metric tracking for embedded vs service-based assessments
8. Support gradual rollout: 10% → 50% → 100% traffic to new service
9. Implement emergency kill switch to instantly revert to embedded assessment
10. Document feature flag usage and rollout plan

#### Integration Verification

**IV1**: With flag disabled, loan applications use embedded assessment (existing behavior unchanged)  
**IV2**: With flag enabled, loan applications use new service and receive equivalent decisions  
**IV3**: Metric dashboards show percentage split between embedded and service-based assessments  

---

### Story 1.17: Performance Optimization and Caching Strategy

**As a** Backend Developer  
**I want** to implement caching for assessment results and expensive operations  
**So that** the service achieves < 5 second response time for most assessments

#### Acceptance Criteria

1. Implement Redis caching for assessment results with 24-hour TTL
2. Cache key format: `assessment:{loanApplicationId}` for by-application cache
3. Cache TransUnion bureau data with 90-day TTL
4. Cache PMEC employment data with 24-hour TTL
5. Cache Vault configuration with 5-minute TTL
6. Implement distributed lock pattern for cache updates (prevent duplicate TransUnion calls)
7. Add cache hit/miss metrics to Prometheus
8. Implement cache invalidation on KYC status change
9. Add `X-Cache-Status` response header indicating Hit/Miss/Bypass
10. Performance test: 95% of cached assessments return within 200ms

#### Integration Verification

**IV1**: Repeated assessment requests for same application return cached results (no duplicate external API calls)  
**IV2**: Cache invalidates correctly when KYC expires  
**IV3**: Service performance meets < 5 second SLA for 95th percentile  

---

### Story 1.18: Comprehensive Testing Suite

**As a** QA Engineer  
**I want** a comprehensive test suite covering unit, integration, and performance tests  
**So that** the service quality and reliability are assured before production deployment

#### Acceptance Criteria

1. Achieve 80% unit test coverage for all services and business logic
2. Create integration tests for all external service clients (Client Management, TransUnion, PMEC, Vault)
3. Create integration tests for RabbitMQ event handlers
4. Create integration tests for database repositories
5. Create end-to-end tests for complete assessment flow (mocked external services)
6. Create performance tests: 100 concurrent assessments, measure p95 latency
7. Create load tests: sustained 100 req/sec for 10 minutes
8. All tests run automatically in CI/CD pipeline
9. Test reports generated with coverage metrics
10. All tests pass successfully before deployment

#### Integration Verification

**IV1**: All existing Loan Origination Service tests continue passing (no regressions)  
**IV2**: Integration tests detect breaking changes in external service contracts  
**IV3**: Performance tests verify service meets latency and throughput SLAs  

---

### Story 1.19: Monitoring, Alerting, and Observability

**As a** DevOps Engineer  
**I want** comprehensive monitoring and alerting for the Credit Assessment Service  
**So that** production issues are detected and resolved quickly

#### Acceptance Criteria

1. Create Grafana dashboard showing key metrics:
   - Request rate, latency (p50, p95, p99), error rate
   - Decision distribution (Approved/Conditional/ManualReview/Rejected)
   - External API latency (Client Mgmt, TransUnion, PMEC)
   - Cache hit rate
   - Rule evaluation duration
   - Manual override rate
2. Configure Prometheus alerts:
   - Service availability < 99%
   - p95 latency > 5 seconds
   - Error rate > 5%
   - TransUnion API failure rate > 10%
   - Manual override rate > 20%
   - KYC event processing lag > 5 minutes
3. Implement structured logging with correlation IDs across all operations
4. Add request/response logging for external API calls (with PII masking)
5. Implement distributed tracing with Jaeger
6. Create runbook documentation for common incidents
7. Set up on-call rotation with PagerDuty integration
8. Test alert triggering with simulated failures

#### Integration Verification

**IV1**: Grafana dashboard displays real-time metrics for service and external integrations  
**IV2**: Alerts trigger correctly during simulated outages  
**IV3**: Correlation IDs trace requests across Credit Assessment, Loan Origination, and AdminService  

---

### Story 1.20: Production Deployment and Cutover

**As a** DevOps Engineer  
**I want** to execute the production deployment and cutover to the new Credit Assessment Service  
**So that** IntelliFin uses the microservice for all credit assessments

#### Acceptance Criteria

1. Deploy Credit Assessment Service to production Kubernetes cluster
2. Verify all external service integrations in production (Client Management, TransUnion, PMEC, Vault, AdminService)
3. Load production Vault configuration with approved risk policies
4. Enable feature flag for 10% of loan applications → monitor for 48 hours
5. If metrics healthy, increase to 50% → monitor for 48 hours
6. If metrics healthy, increase to 100% → monitor for 1 week
7. Decommission embedded assessment code from Loan Origination Service
8. Update Loan Origination Service to always use new service (remove feature flag)
9. Archive old `CreditAssessmentService` code with deprecation notice
10. Production cutover complete, all assessments using new microservice

#### Integration Verification

**IV1**: Loan application approval workflow completes successfully end-to-end using new service  
**IV2**: All assessment audit events appear correctly in AdminService  
**IV3**: Zero regression in loan approval processing times or error rates compared to pre-migration baseline  

---

## 6. Success Metrics

**Deployment Success**:
- [ ] Service deployed to production with 99.9% availability
- [ ] 100% of credit assessments using new microservice
- [ ] Zero critical incidents during 30-day post-cutover period

**Performance Success**:
- [ ] 95th percentile assessment latency < 5 seconds
- [ ] 99th percentile assessment latency < 10 seconds
- [ ] Cache hit rate > 70%
- [ ] External API (TransUnion, PMEC) uptime > 95%

**Quality Success**:
- [ ] 100% decision auditability (all assessments logged to AdminService)
- [ ] Zero rule configuration deployment downtime
- [ ] Unit test coverage > 80%
- [ ] Integration test coverage for all critical paths

**Business Success**:
- [ ] Business team able to update risk policies via Vault without developer support
- [ ] Manual override rate < 15% (indicating good automated decision quality)
- [ ] Credit officer satisfaction score > 4/5 for decision explainability
- [ ] Zero BoZ compliance findings related to credit assessment

---

## 7. Out of Scope (Future Enhancements)

The following items are explicitly **out of scope** for this PRD and Epic but identified for future consideration:

1. **AI/ML Scoring Models** - Machine learning based credit scoring using mobile money transaction history, payroll patterns, and historical repayment behavior
2. **Collateral Valuation Service** - Automated collateral assessment for SME loans (currently manual process)
3. **Alternative Data Sources** - Integration with utility bill payments, mobile money providers, e-commerce platforms
4. **Credit Assessment API for External Partners** - Expose assessment capability to third-party lenders
5. **Real-Time Reassessment** - Continuous credit monitoring and dynamic limit adjustments
6. **Multi-Currency Support** - Assessment for cross-border lending or USD-denominated loans
7. **Batch Assessment Processing** - Bulk assessment for portfolio review or marketing campaigns
8. **Predictive Default Modeling** - Probability of default scoring beyond current risk grading

---

## Appendix A: API Specification Summary

### Assessment API

```http
POST /api/v1/credit-assessment/assess
GET /api/v1/credit-assessment/{assessmentId}
GET /api/v1/credit-assessment/client/{clientId}/latest
POST /api/v1/credit-assessment/{assessmentId}/manual-override
```

### Admin API

```http
GET /api/v1/admin/config/version
POST /api/v1/admin/config/refresh
POST /api/v1/admin/assessment/{assessmentId}/invalidate
```

### Health & Metrics

```http
GET /health/live
GET /health/ready
GET /metrics
```

Full OpenAPI specification available at `/swagger/v1/swagger.json` when service is running.

---

## Appendix B: Vault Configuration Schema

See `docs/domains/credit-assessment/brownfield-architecture.md` Section "Vault-Based Rule Engine Design" for complete configuration schema including:
- Rule definition structure
- Threshold configuration
- Grade thresholds
- Decision matrix
- Product-specific rules (payroll vs business)

---

## Appendix C: Database Schema

See `docs/domains/credit-assessment/brownfield-architecture.md` Appendix C for:
- New tables: `credit_assessment_audit`, `rule_evaluations`, `assessment_config_versions`
- Enhancements to existing `credit_assessments` table

---

**Document Status**: Draft - Ready for Review  
**Next Steps**:
1. **Architect Review** - Validate technical approach and integration patterns
2. **PO Validation** - Validate requirements completeness and story sequencing
3. **Stakeholder Approval** - Get sign-off from Credit Manager, Compliance Officer, CTO
4. **Epic & Story Creation** - Export to project management system for sprint planning

**Approval Signatures**:

- **Product Manager**: ___________________ Date: __________
- **Lead Architect**: ___________________ Date: __________
- **Product Owner**: ___________________ Date: __________
- **Credit Manager**: ___________________ Date: __________
- **CTO**: ___________________ Date: __________
