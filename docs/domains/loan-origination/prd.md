# Loan Origination Brownfield Enhancement PRD

## Document Information

- **Product**: IntelliFin Loan Management System
- **Module**: Loan Origination
- **Document Type**: Product Requirements Document (Brownfield Enhancement)
- **Version**: 1.0
- **Last Updated**: 2025-10-17
- **Owner**: Product Management Team
- **Status**: Draft

---

## 1. Intro Project Analysis and Context

### 1.1 Analysis Source

✅ **Document-project output available at**: `docs/domains/loan-origination/brownfield-architecture.md`

This comprehensive brownfield analysis was completed by the Architect agent and provides:
- Current state analysis of existing LoanOriginationService implementation
- Gap analysis identifying missing components
- Integration architecture with Client Management and other services
- Complete file manifest and implementation roadmap

### 1.2 Current Project State

**Extracted from Brownfield Architecture Document**:

The Loan Origination module currently exists as a **basic CRUD-based loan application scaffolding** with:

**What EXISTS**:
- ✅ ASP.NET Core service with Zeebe client configured
- ✅ Domain models supporting workflow integration (LoanApplication, CreditAssessment)
- ✅ Credit assessment engine with BoZ-aligned risk scoring (A-F grades)
- ✅ Compliance service with KYC validation and BoZ compliance checks
- ✅ Controllers and repositories for basic operations
- ✅ MassTransit/RabbitMQ messaging infrastructure configured

**What is MISSING** (Critical Gaps):
- ❌ No KYC/AML gating from Client Management service
- ❌ Product rules hardcoded (not Vault-based)
- ❌ No workflow orchestration (BPMN not deployed)
- ❌ No dual control enforcement
- ❌ No loan versioning
- ❌ No agreement generation (JasperReports not integrated)
- ❌ No audit event publishing to AdminService

**Target State**: Process-aware, compliance-embedded origination engine orchestrated by Camunda with deep integration to Vault, Client Management, AdminService, and JasperReports.

### 1.3 Enhancement Scope Definition

**Enhancement Type**: ✅ **Major Feature Modification + Integration with New Systems**

**Enhancement Description**:

Transform the existing Loan Origination module from a transactional CRUD system into a fully automated, workflow-driven origination engine. This enhancement adds Camunda-orchestrated workflows (Application → Assessment → Approval → Disbursement), enforces KYC/AML compliance gates integrated with Client Management service, externalizes product rules to Vault configuration with EAR enforcement, implements dual control for approvals, enables loan versioning for audit trails, automates agreement generation via JasperReports with MinIO storage, and streams comprehensive audit events to AdminService.

**Impact Assessment**: ✅ **Major Impact (architectural changes required)**

The enhancement requires:
- New workflow orchestration layer (Camunda BPMN with human tasks and service tasks)
- Service-to-service integration patterns (HTTP + Events with Client Management, Credit Assessment)
- Configuration-driven product rules (Vault KV store)
- Event-driven audit architecture (RabbitMQ → AdminService)
- Database schema changes (versioning, loan numbers, agreement tracking)

### 1.4 Goals and Background Context

**Goals**:
- Transform loan origination from manual, transactional to fully automated workflow-driven process
- Enforce KYC/AML compliance gates to prevent loans for non-verified clients
- Externalize product configurations (interest rates, EAR caps, eligibility rules) to Vault for dynamic updates without redeployment
- Implement dual control at workflow and service layers to prevent self-approval and ensure segregation of duties
- Enable loan versioning to maintain immutable audit history for regulatory compliance
- Automate agreement generation with JasperReports, MinIO storage, and cryptographic hashing for document integrity
- Stream comprehensive audit events to AdminService for centralized compliance reporting and traceability

**Background Context**:

The current Loan Origination implementation was created as a foundation service but lacks the workflow orchestration, compliance integration, and audit capabilities required for production use in a regulated microfinance environment. The Client Management module has now been completed with full KYC/AML verification capabilities, creating the prerequisite for proper loan origination gating. Similarly, the infrastructure components (Camunda 8, Vault, MinIO, JasperReports, AdminService, RabbitMQ) are all operational and ready for integration.

This enhancement is critical to meeting Bank of Zambia regulatory requirements, particularly around EAR compliance (48% cap enforced via Vault), dual control/segregation of duties, complete audit trails, and KYC verification before lending. The transformation aligns with the broader IntelliFin architecture vision of configuration-driven, workflow-orchestrated services that maintain compliance at every layer.

### 1.5 Change Log

| Date       | Version | Description                          | Author    |
|------------|---------|--------------------------------------|-----------|
| 2025-10-17 | 1.0     | Initial PRD creation                 | PM (John) |

---

## 2. Requirements

### 2.1 Functional Requirements

**FR1**: The system shall integrate with Client Management Service to verify KYC/AML status before allowing loan application initiation, blocking applications for clients whose KYC status is not "Approved" or whose AML clearance is pending.

**FR2**: The system shall implement a Camunda BPMN workflow (loan-origination-process) orchestrating the complete loan lifecycle from Application → Assessment → Approval → Disbursement → Active, with automatic routing based on loan amount and risk grade.

**FR3**: The system shall load all loan product configurations (interest rates, EAR caps, term limits, eligibility rules) dynamically from Vault key-value store at `kv/intellifin/loan-products/{productCode}/rules`, with 5-minute cache TTL.

**FR4**: The system shall enforce EAR compliance by validating that the calculated Effective Annual Rate (including all recurring fees) does not exceed the Money Lenders Act limit of 48%, rejecting product configurations that violate this constraint.

**FR5**: The system shall implement dual control enforcement at two layers: (1) Camunda workflow routing that prevents task assignment to the loan creator, and (2) API-level validation that rejects approval attempts where approver equals creator or assessor.

**FR6**: The system shall implement loan versioning by assigning a unique loan number (format: `{BranchCode}-{Year}-{Sequence}`) and maintaining version history, creating new version records for all state changes while preserving immutable snapshots of previous states.

**FR7**: The system shall route approval tasks to appropriate user groups based on loan amount and risk grade: Credit Analysts (≤50K, A/B grade), Head of Credit (>50K or C grade), dual control Head of Credit + CEO (>250K or D/F grade).

**FR8**: The system shall generate loan agreements automatically via JasperReports API upon approval, store the PDF in MinIO at `loan-agreements/{clientId}/{loanNumber}_v{version}.pdf`, compute SHA256 hash, and record both hash and MinIO path in the loan record.

**FR9**: The system shall publish comprehensive audit events to AdminService via RabbitMQ for all state changes including: LoanApplicationCreated, LoanApplicationSubmitted, LoanCreditAssessmentCompleted, LoanApprovalGranted, LoanApprovalRejected, LoanAgreementGenerated, LoanDisbursed, LoanVersionCreated.

**FR10**: The system shall subscribe to Client Management events (ClientKycApproved, ClientKycRevoked, ClientProfileUpdated) and automatically pause active loan workflows when KYC status is revoked or expired (>12 months old).

**FR11**: The system shall integrate with Credit Assessment Service via HTTP API for risk scoring, with fallback to manual credit review task in Camunda workflow if the service is unavailable.

**FR12**: The system shall maintain complete loan workflow state tracking including current state, workflow instance key, and workflow variables, exposing this state via API for frontend query.

**FR13**: The system shall auto-populate loan application forms by fetching client profile data (name, NRC, address, employment details, salary) from Client Management Service when loan officer selects a verified client, presenting pre-filled data for human validation rather than manual entry.

**FR14**: The system shall automatically calculate and display Effective Annual Rate (EAR), monthly payment amount, and debt-to-income ratio in real-time as loan parameters are adjusted, presenting these as read-only calculated fields for user review.

**FR15**: The system shall automatically trigger Credit Assessment Service integration upon application submission (without human initiation), displaying multi-stage progress indicator: "Fetching Credit Bureau Data → Calculating Risk Score → Assessing Affordability → Generating Recommendation" with estimated time remaining.

**FR16**: The system shall automatically route approved loans to agreement generation (without human initiation), displaying progress stages: "Fetching Loan Terms → Applying Template → Generating PDF → Storing in MinIO → Computing Hash" with real-time status updates.

**FR17**: The system shall present credit assessment results (risk grade A-F, credit score, affordability analysis) as read-only recommendations with explanation for human approval decision, not as editable fields requiring manual scoring.

**FR18**: The system shall automatically validate all entered data against product rules from Vault (amount within limits, term allowed, client meets eligibility) in real-time, displaying validation errors immediately to prevent submission of invalid applications.

**FR19**: The system shall auto-select the appropriate approval routing path based on calculated risk grade and loan amount, displaying to the user "This loan will be routed to [Credit Analyst / Head of Credit / CEO dual control]" for transparency, without requiring manual routing selection.

### 2.2 Non-Functional Requirements

**NFR1**: All API endpoints (excluding external service calls to Credit Assessment, JasperReports, Client Management) shall respond within 500ms at p95 for up to 100 concurrent loan application requests.

**NFR2**: Vault product configurations shall be cached in-memory with 5-minute TTL to reduce latency, with automatic cache invalidation on configuration update events.

**NFR3**: The system shall handle Camunda workflow execution for 100 concurrent loan applications without degradation, with worker threads scaled to match load.

**NFR4**: Audit events published to AdminService shall be delivered with at-least-once guarantee using MassTransit/RabbitMQ durable queues with dead-letter exchange for failed deliveries.

**NFR5**: All loan state changes, agreement generation, and approval actions shall be traceable via correlation IDs propagated across all service calls and event publications for distributed tracing.

**NFR6**: The system shall maintain 99.9% uptime during business hours (08:00-18:00 UTC) with graceful degradation if external services (Credit Assessment, JasperReports) are unavailable.

**NFR7**: Database queries for loan retrieval shall complete within 100ms for current version lookup and 200ms for version history retrieval (up to 50 versions).

**NFR8**: Agreement PDF generation via JasperReports shall complete within 3 seconds for standard templates, with timeout after 10 seconds triggering workflow incident for manual intervention.

### 2.3 Compatibility Requirements

**CR1: Existing API Compatibility** - All existing REST API endpoints (`/api/loanapplication`) must remain functional with backward-compatible responses; new workflow-related fields (workflowInstanceKey, currentState) shall be added as optional fields, not replacements.

**CR2: Database Schema Compatibility** - New versioning columns (LoanNumber, Version, ParentVersionId, IsCurrentVersion, AgreementFileHash, AgreementMinioPath) shall be added to existing LoanApplications table via ALTER TABLE; existing queries selecting by Id must continue to work with performance impact <10%.

**CR3: UI/UX Consistency** - Loan status enumeration shall be extended (add "PendingKycVerification", "PendingCreditAssessment", "PendingApproval") but existing statuses (Draft, Submitted, Approved, Rejected) must remain unchanged to avoid breaking frontend state machines.

**CR4: Integration Compatibility** - Existing RabbitMQ exchange and queue configurations for other services must not be modified; new loan audit events shall be published to dedicated exchange (`intellifin.loan.events`) to avoid impacting existing message routing.

**CR5: Shared Library Compatibility** - Changes to `Shared.DomainModels.Entities.LoanApplication` must maintain binary compatibility with other services (AdminService, ReportingService) that reference this assembly; use optional properties for new versioning fields.

---

## 3. User Interface Enhancement Goals

### Design Philosophy: Hide Complexity, Maximize Speed

**Core Principle**: The backend workflow orchestration (Camunda BPMN, Vault configs, dual control, versioning, audit events) operates as a complex compliance engine, but the frontend must **abstract this complexity entirely**. Users should experience a fast, intuitive flow where the system does the heavy lifting invisibly.

**Speed as Competitive Advantage**:
- **Target**: Loan officer completes application in <15 minutes (vs. competitor average of 45+ minutes)
- **Target**: Credit analyst approves/rejects from queue in <2 minutes
- **Target**: Zero unnecessary clicks - every action must directly advance the loan lifecycle

**Automation-First Approach**:
- ✅ **Backend does the heavy lifting** - System fetches data, calculates values, makes recommendations
- ✅ **Human validates and approves** - User confirms "Yes, this is correct" and advances the workflow
- ✅ **Transparent waiting** - When backend processes take time (Credit Assessment, Agreement Generation), show progress stages so user understands what's happening
- ✅ **Error prevention** - Minimize manual data entry; system-calculated values (EAR, DTI, risk grade, recommended terms) are presented as read-only for human validation

### 3.1 Integration with Existing UI - Speed-First Approach

**Existing UI Patterns to Enhance (Not Replace)**:
- **Smart Wizard**: Extend existing loan application wizard to pre-fill data from client profile, auto-fetch product configs, calculate EAR in real-time as user types - no manual lookups required
- **Action-Oriented Dashboards**: Transform existing loan list into action-focused views: "Needs Your Action" (top priority tasks), "In Progress" (waiting on others), "Completed" (archive)
- **Inline Actions**: All approval/rejection actions directly in the list view (no navigation to detail pages unless user explicitly chooses to review)
- **Background Processing**: All workflow state transitions, KYC checks, credit assessments happen automatically - UI shows progress indicators, not blocking spinners

**Integration Points with Existing UI**:
1. **Loan Application Form** - Auto-check KYC in background as user types client ID; if not verified, show inline "Complete KYC First" button that opens KYC wizard in modal (no page navigation)
2. **Loan Dashboard** - Single unified view with smart filters: "My Tasks", "My Team's Tasks", "All Loans" - default to "My Tasks"
3. **Approval Queue** - NOT a separate page - integrated as primary section of dashboard with approve/reject buttons directly on each row
4. **Document Management** - Auto-generate agreements on approval (background); show "Agreement Ready" notification, allow instant download with single click

### 3.2 Modified/New Screens and Views

**Modified Screens**:

1. **Loan Application Wizard** (Existing: `pages/loans/new.tsx`) - **AUTOMATION-FIRST**
   - **Auto-fetch client data**: When loan officer enters client ID, system fetches all KYC-verified data from Client Management and pre-fills form (name, NRC, address, employment, salary) → User reviews and confirms accuracy with single "Confirm Client Details" button
   - **Auto-calculate loan parameters**: As user adjusts amount/term sliders, system calculates EAR, monthly payment, DTI ratio in real-time → Displayed as read-only badges with tooltips explaining calculation
   - **Auto-select product rules**: System fetches eligible products from Vault based on client type → User selects from dropdown, system shows interest rate, fees, term limits as read-only (not editable)
   - **Real-time validation**: System validates amount against product limits, DTI against affordability → Shows green checkmarks for valid, red errors for invalid before submission
   - **Auto-save**: System saves draft every 10 seconds (no manual save button) → "Last saved 5 seconds ago" indicator
   - **Submit triggers automation**: "Submit" button starts multi-stage backend process with progress indicator

2. **Credit Assessment Progress Indicator** (New: Inline component on Loan Detail)
   - **Triggered automatically** after application submission
   - **Multi-stage progress bar** with real-time updates:
     ```
     ✓ Application Submitted
     ⟳ Fetching Credit Bureau Data (TransUnion API call) - 15 seconds
     ⏱ Calculating Risk Score (Evaluating payment history, DTI, credit utilization) - 5 seconds
     ⏱ Assessing Affordability (Analyzing income vs. debt obligations) - 3 seconds
     ⏱ Generating Recommendation (Determining approval recommendation) - 2 seconds
     ```
   - **Purpose**: User understands WHY they're waiting and approximately HOW LONG
   - **Fallback**: If Credit Assessment Service is down → Show "System automatically routing to Manual Review" with explanation

3. **Loan Dashboard** (Existing: `pages/loans/index.tsx`) - **ACTION-FIRST REDESIGN**
   - **Primary View: "Needs Your Action"**: Loans assigned to current user at top (bold, color-coded by urgency)
   - **Inline Approval Actions**: For Credit Analysts/Head of Credit - Approve/Reject buttons directly on each row with single-click confirmation
   - **Workflow State**: Show as simple icon + tooltip (hover to see full state path), NOT as verbose text
   - **Quick Actions Menu**: Three-dot menu on each row for secondary actions (View Details, Download Agreement, View History)
   - **Smart Filtering**: Default filter = "Needs My Action", with quick toggle to "Team Queue" and "All Loans"
   - **No Detail Page Navigation Required**: 90% of actions doable from list view; detail page only for investigation/audit
   - **System-calculated summary** presented for each loan:
     - **Risk Grade** (A-F) with explanation: "Grade B based on: Good payment history, DTI 32%, 3 years credit history"
     - **Recommended Decision**: "System recommends APPROVAL based on risk grade and affordability"
     - **Calculated Values**: EAR, monthly payment, DTI ratio (all read-only, system-calculated)
   - **Human validation action**: Approver reviews system recommendation and clicks:
     - "Approve (Agree with Recommendation)" - Most common action
     - "Reject (Override Recommendation)" - Requires reason selection from dropdown
     - "Request More Information" - Adds comment and sends back to loan officer
   - **Dual Control Transparency**: If second approval needed, show "Your approval will send this to [CEO] for final authorization"

4. **Loan Detail View** (Existing: `pages/loans/[id].tsx`) - **INVESTIGATION MODE ONLY**
   - **Use case**: Loan officer/approver needs to investigate before decision - NOT for routine approvals
   - **Lazy load workflow timeline**: Hidden by default behind "Show Timeline" button to avoid clutter
   - **Version history**: Collapsed accordion, only for audit/compliance review
   - **Agreement preview**: Inline PDF viewer (not modal) for quick review

5. **Agreement Generation Progress Indicator** (New: Modal overlay)
   - **Triggered automatically** when loan is approved
   - **Multi-stage progress**:
     ```
     ✓ Approval Confirmed
     ⟳ Fetching Final Loan Terms from Vault - 2 seconds
     ⏱ Generating Agreement PDF (JasperReports) - 5 seconds
     ⏱ Storing Document in MinIO - 1 second
     ⏱ Computing Document Hash (SHA256) - 1 second
     ⏱ Recording Audit Trail in AdminService - 1 second
     ```
   - **Completion**: "Agreement Ready for Disbursement" with "Download Agreement" and "Proceed to Disbursement" buttons
   - **Error Handling**: If JasperReports times out → "Agreement generation taking longer than expected. System will notify you when complete. You can continue working."

6. **Data Validation Feedback** (Inline throughout forms)
   - **Real-time validation** as user types/changes values:
     - Amount exceeds product limit → Red border + "Maximum amount for this product is ZMW 50,000"
     - DTI exceeds 40% → Yellow warning + "Client's debt-to-income ratio is 45%. Loan may require additional review."
     - KYC expired → Red block + "Client's KYC verification expired 2 months ago. Complete KYC renewal before proceeding."
   - **Auto-correction suggestions**: If user enters invalid term → System suggests nearest valid term: "12 months is not available for this product. Suggested terms: 6 months or 18 months"

7. **Background Status Notifications** (New: `components/ui/WorkflowNotifications.tsx`) - **NON-INTRUSIVE**
   - **Toast notifications**: "Credit assessment completed - Loan ready for approval", "Agreement generated - Ready to disburse"
   - **No modals**: Never block user with "Processing..." dialogs - everything happens in background with progress indicators
   - **Action buttons in toasts**: "Review Now" button in notification for immediate action if user wants

### 3.3 UI Consistency Requirements

**UCR1**: All workflow state displays shall use consistent status badge components from `components/ui/StatusBadge.tsx` with predefined color mappings: Draft (gray), In Assessment (blue), Pending Approval (yellow), Approved (green), Rejected (red), Disbursed (purple).

**UCR2**: Approval actions (Approve, Reject, Approve with Conditions) shall use the same confirmation dialog pattern as Client Management KYC approval actions, requiring comment/reason text for reject actions.

**UCR3**: Version history display shall follow the same accordion pattern used in Client Profile versioning (recently implemented in Client Management module) to maintain cross-module UX consistency.

**UCR4**: Real-time KYC status indicators shall use the same icon set and color scheme as Client Management module: Verified (green checkmark), Pending (yellow clock), Expired (red warning), Revoked (red X).

**UCR5**: All new API loading states shall use the existing skeleton loader components from `components/ui/SkeletonLoader.tsx` to maintain loading UX consistency.

**UCR6**: Error states for workflow failures (e.g., Credit Assessment service down, JasperReports timeout) shall use the existing error boundary pattern with user-friendly messages and retry actions.

**UCR7**: All workflow state transitions shall happen in the background with optimistic UI updates (show new state immediately, rollback if backend fails) to avoid blocking user flow.

**UCR8**: All calculated fields (EAR, monthly payment, DTI ratio, risk grade, credit score) shall be displayed as read-only with clear "System Calculated" indicators and tooltips explaining calculation methodology to prevent manual override attempts.

**UCR9**: All long-running backend processes (>3 seconds) shall display multi-stage progress indicators with descriptive stage names and estimated time remaining, updated in real-time via WebSocket or polling to provide transparency during waits.

**UCR10**: All auto-populated data from backend services (client profile, employment details, product configs) shall be displayed with visual indicators ("Auto-filled from Client Profile") and "Edit" buttons that require explicit confirmation to override system-provided data.

**UCR11**: All human validation actions (Approve, Reject, Confirm) shall require single-click confirmation with pre-populated system recommendations, minimizing required user input to binary choices or dropdown selections (no free-text fields except for rejection reasons).

**UCR12**: All validation errors shall be shown inline in real-time as user interacts with form fields, preventing submission of invalid data rather than showing errors after submission attempt.

---

## 4. Technical Constraints and Integration Requirements

### 4.1 Existing Technology Stack

**Extracted from Brownfield Architecture Document - Actual Tech Stack**:

| Category | Technology | Version | Constraints/Notes |
|----------|-----------|---------|-------------------|
| Backend Language | C# | 12.0 (.NET 9) | ASP.NET Core Minimal APIs, DI container |
| Backend Framework | ASP.NET Core | 9.0 | Existing service scaffolding in place |
| Database | SQL Server | 2022 | ACID compliance, Always On availability |
| Workflow Engine | Camunda 8 (Zeebe) | 8.4+ | Zeebe client configured but no BPMN deployed yet |
| Configuration Store | HashiCorp Vault | 1.15+ | Control Plane infrastructure exists, needs product config extension |
| Object Storage | MinIO | RELEASE.2024-01-16T16-07-38Z | S3-compatible, WORM storage for agreements |
| Document Generation | JasperReports Server | 8.0+ | Existing infrastructure, needs loan agreement templates |
| Message Queue | RabbitMQ | 3.12+ | MassTransit configured for event-driven messaging |
| Caching | Redis | 7.2+ | Available for Vault config caching |
| Frontend Framework | Next.js | 15+ | TypeScript, Tailwind CSS, React Query + Zustand |
| Audit/Event Bus | AdminService | Custom | Existing microservice consuming audit events via RabbitMQ |

**Critical Constraints**:
- **No new infrastructure** - All components (Camunda, Vault, MinIO, JasperReports, RabbitMQ) are operational; enhancement extends existing usage patterns
- **Shared.DomainModels library** - Changes to LoanApplication entity must maintain binary compatibility with other services (AdminService, ReportingService)
- **Zeebe client SDK limitations** - Workers must be idempotent; no transactions across Zeebe + SQL Server
- **Vault Agent sidecar pattern** - Configuration injection follows Control Plane standard (CSI driver + init containers)

### 4.2 Integration Approach

**Database Integration Strategy**:
- **Versioning schema migration**: ALTER TABLE to add versioning columns (LoanNumber, Version, ParentVersionId, IsCurrentVersion, AgreementFileHash, AgreementMinioPath) to existing LoanApplications table
- **Backward compatibility**: New columns nullable initially; backfill LoanNumber for existing records with format `LUS-2025-{sequence}`
- **Version queries**: Add indexed queries `GetCurrentVersionAsync(loanNumber)` and `GetVersionHistoryAsync(loanNumber)` to repository
- **Transaction boundaries**: Version creation + audit event publishing within single database transaction for consistency

**API Integration Strategy**:
- **Client Management Service**: HTTP GET `/api/clients/{id}/verification` for KYC status check; timeout 3s with circuit breaker (fallback: manual KYC check)
- **Credit Assessment Service**: HTTP POST `/api/assessments` with loan + client data; timeout 30s with fallback to manual credit review task in Camunda
- **JasperReports Server**: HTTP POST `/rest_v2/reports/intellifin/loan-agreements/{template}.pdf` with JSON payload; timeout 10s with retry once
- **Service-to-service auth**: OAuth2 client credentials flow using Keycloak tokens with 1-hour expiration

**Frontend Integration Strategy**:
- **REST API**: Existing `/api/loanapplication` endpoints extended with workflow state fields (workflowInstanceKey, currentState, assignedTo)
- **Real-time updates**: WebSocket connection for workflow state changes (Camunda workflow events → backend publishes via SignalR → frontend updates)
- **Optimistic UI**: Frontend updates state immediately on user action (Approve/Reject), rollback if backend returns error
- **Polling fallback**: If WebSocket unavailable, poll workflow state every 5 seconds for loans in active workflows

**Testing Integration Strategy**:
- **Unit tests**: xUnit for service layer (VaultProductConfigService, DualControlValidator, LoanVersioningService)
- **Integration tests**: Testcontainers for SQL Server, RabbitMQ, Redis; mock Zeebe client for workflow tests
- **E2E tests**: Playwright for critical user journeys (Application → Assessment → Approval → Agreement)
- **Workflow tests**: Camunda Zeebe Process Test library for BPMN validation

### 4.3 Code Organization and Standards

**File Structure Approach**:
```
apps/IntelliFin.LoanOriginationService/
├── Services/
│   ├── VaultProductConfigService.cs          [NEW]
│   ├── LoanVersioningService.cs              [NEW]
│   ├── AgreementGenerationService.cs         [NEW]
│   ├── ClientManagementClient.cs             [NEW]
│   ├── AuditEventPublisher.cs                [NEW]
│   ├── DualControlValidator.cs               [NEW]
│   ├── LoanApplicationService.cs             [MODIFY]
│   ├── ComplianceService.cs                  [MODIFY]
│   └── WorkflowService.cs                    [MODIFY]
├── Workers/
│   ├── KycVerificationWorker.cs              [NEW]
│   ├── CreditAssessmentWorker.cs             [NEW]
│   ├── GenerateAgreementWorker.cs            [NEW]
│   └── InitialValidationWorker.cs            [EXISTING]
├── Consumers/
│   ├── ClientKycApprovedConsumer.cs          [NEW]
│   ├── ClientKycRevokedConsumer.cs           [NEW]
│   └── CreditAssessmentCompletedConsumer.cs  [NEW]
├── Events/
│   ├── LoanApplicationCreated.cs             [NEW]
│   ├── LoanApprovalGranted.cs                [NEW]
│   ├── LoanAgreementGenerated.cs             [NEW]
│   └── [8 other audit event records]         [NEW]
├── Workflows/
│   └── loan-origination-process.bpmn         [NEW]
└── Models/
    ├── LoanProductConfig.cs                  [NEW]
    └── LoanApplicationModels.cs              [MODIFY]
```

**Naming Conventions**:
- **Workers**: `{ActionName}Worker.cs` (e.g., KycVerificationWorker)
- **Consumers**: `{EventName}Consumer.cs` (e.g., ClientKycApprovedConsumer)
- **Events**: `{Entity}{Action}.cs` record types (e.g., LoanApplicationCreated)
- **Services**: `{Purpose}Service.cs` interface + implementation pattern

**Coding Standards**:
- **Async/await**: All I/O operations async (database, HTTP, Vault, MinIO)
- **Cancellation tokens**: Propagate CancellationToken through all async method signatures
- **Logging**: Structured logging with Serilog; correlation IDs in all log entries
- **Exception handling**: Domain exceptions (KycExpiredException, EarComplianceException) for business rule violations; infrastructure exceptions for service failures

**Documentation Standards**:
- **XML comments**: Required for all public interfaces and service classes
- **Inline comments**: For complex business logic (EAR calculation, dual control validation)
- **README updates**: Document new Vault configuration schema, BPMN deployment process

### 4.4 Deployment and Operations

**Build Process Integration**:
- **No changes to existing CI/CD pipeline** - Standard .NET build, test, Docker image creation
- **BPMN deployment**: Add step to deploy `loan-origination-process.bpmn` to Camunda on service startup using BpmnDeploymentService hosted service
- **Database migrations**: EF Core migrations for versioning schema changes; run via init container in Kubernetes

**Deployment Strategy**:
- **Phased rollout**: Deploy with feature flag `EnableWorkflowOrchestration=false` initially; existing CRUD APIs continue working
- **Toggle to workflow mode**: Set feature flag `EnableWorkflowOrchestration=true` after BPMN deployed and tested
- **Zero downtime**: New versioning columns nullable; backfill loan numbers in background job

**Monitoring and Logging**:
- **Application Insights**: Track workflow completion times, approval latencies, agreement generation duration
- **Custom metrics**: Camunda workflow instance counts by state, KYC block rate, dual control violation attempts
- **Correlation IDs**: Propagate through all service calls (Client Management, Credit Assessment, JasperReports) for distributed tracing
- **Alerts**: Workflow incidents (stuck tasks >1 hour), agreement generation failures, audit event publish failures

**Configuration Management**:
- **Vault**: Product configurations stored in Vault KV v2 engine at `kv/intellifin/loan-products/{productCode}/rules`
- **appsettings.json**: Connection strings, Zeebe gateway address, Vault address (non-secret config)
- **Kubernetes Secrets**: Vault token, SQL connection password, RabbitMQ credentials, Keycloak client secret
- **ConfigMap**: Feature flags (EnableWorkflowOrchestration, UseMockBureauData)

### 4.5 Risk Assessment and Mitigation

**Referenced from Brownfield Architecture Document - Known Technical Debt**:

**Existing Technical Debt That Impacts Enhancement**:
1. **Mock Credit Bureau Data** (`CreditAssessmentService.cs` lines 12-41) - Hardcoded TransUnion data
   - **Impact**: Credit Assessment integration will use mock data until Sprint 4 TransUnion API integration
   - **Mitigation**: Add configuration flag `UseMockBureauData: true`; architecture supports swapping to real API later

2. **Hardcoded Compliance Rules** (`ComplianceService.cs` lines 46-96) - BoZ requirements not in Vault
   - **Impact**: Compliance rule changes require code deployment
   - **Mitigation**: Phase 1 focuses on Vault product configs; Phase 2 moves compliance rules to Vault

3. **No Correlation ID Middleware** - Distributed tracing infrastructure incomplete
   - **Impact**: Troubleshooting workflow failures across services will be harder
   - **Mitigation**: Add CorrelationIdMiddleware as part of this enhancement

**Technical Risks**:
- **Risk**: Zeebe workflow state diverges from SQL database state (eventual consistency issue)
  - **Mitigation**: Idempotent workers; publish events only after database commits; workflow retry on transient failures
  
- **Risk**: Vault configuration cache staleness (5-minute TTL) causes loan rejection on recently updated rules
  - **Mitigation**: Publish `ProductConfigUpdated` event from Vault workflow; listeners invalidate cache immediately

- **Risk**: JasperReports template versioning conflicts (multiple templates deployed simultaneously)
  - **Mitigation**: Template version specified in Vault product config; Jasper API called with explicit template path

**Integration Risks**:
- **Risk**: Client Management Service downtime blocks all loan originations (KYC gate failure)
  - **Mitigation**: Circuit breaker with 3-attempt retry; fallback to manual KYC verification in Camunda

- **Risk**: Credit Assessment Service timeout causes workflow to stall
  - **Mitigation**: 30-second timeout + automatic fallback to manual credit review task

- **Risk**: MinIO storage full prevents agreement generation
  - **Mitigation**: Pre-flight storage capacity check; alert when <10% free space

**Deployment Risks**:
- **Risk**: Database migration fails mid-deployment (versioning columns partially added)
  - **Mitigation**: All schema changes in single transaction; rollback on failure; nullable columns allow gradual rollout

- **Risk**: BPMN deployment to Camunda fails but service starts (no workflows available)
  - **Mitigation**: BpmnDeploymentService validates deployment success on startup; service fails to start if deployment errors

**Mitigation Strategies Summary**:
- **Feature flags**: Toggle workflow orchestration on/off without redeployment
- **Circuit breakers**: Prevent cascade failures from external service downtime
- **Fallback tasks**: Manual alternatives for automated steps (credit review, KYC verification)
- **Idempotent workers**: Safe retry of Zeebe task handlers
- **Comprehensive logging**: Correlation IDs + structured logs for troubleshooting
- **Graceful degradation**: Core CRUD APIs remain functional if workflow engine unavailable

---

## 5. Epic and Story Structure

### 5.1 Epic Approach

**Epic Structure Decision**: **Single Comprehensive Epic** with rationale:

This brownfield enhancement is architected as a **single unified epic** rather than multiple independent epics because:

1. **Tightly Coupled Components**: All enhancement components (KYC gating, Vault config, workflow orchestration, dual control, versioning, agreement generation, audit events) are architecturally interdependent - they form a cohesive workflow-driven system that cannot be delivered in isolated pieces.

2. **Sequential Dependencies**: Each component builds upon the previous (e.g., versioning must exist before agreement generation can store hashes; Vault config must be operational before workflows can enforce EAR compliance).

3. **Risk Management**: Breaking into multiple epics would create partially-functional states that increase deployment risk (e.g., deploying dual control without workflow orchestration leaves approval logic incomplete).

4. **Brownfield Integration**: The enhancement transforms the *entire* loan origination flow from CRUD to workflow-driven; splitting would require complex feature flag management across multiple deployments.

**Story Sequencing Strategy for Risk Minimization**:
- **Foundation First**: Database schema, versioning, loan number generation (non-breaking additive changes)
- **Infrastructure Integration**: Vault, Client Management HTTP client (read-only integrations initially)
- **Workflow Core**: BPMN deployment, worker implementation (deployed with feature flag disabled)
- **Compliance Gates**: KYC verification, dual control enforcement (highest risk - goes last in core sequence)
- **Document Automation**: Agreement generation (depends on stable workflow)
- **Audit Trail**: Event publishing (non-blocking enhancement, can be added incrementally)
- **Testing & Optimization**: Integration tests, performance validation

**Story Sizing for AI Agent Execution**:
- Each story targets **1-2 focused development sessions** (4-8 hours)
- Stories include **existing functionality verification** steps
- Clear **rollback plan** documented in acceptance criteria
- **Dependencies explicitly mapped** between stories

---

## Epic 1: Transform Loan Origination to Workflow-Driven, Compliance-Embedded Engine

**Epic Goal**: Transform the existing CRUD-based Loan Origination module into a Camunda-orchestrated, configuration-driven workflow engine that enforces KYC/AML compliance gates, implements dual control approvals, maintains loan versioning for audit trails, automates agreement generation, and publishes comprehensive audit events to AdminService - while maintaining backward compatibility with existing functionality and ensuring zero data loss.

**Integration Requirements**:
- Extend Vault Control Plane for dynamic product configuration storage
- Integrate with Client Management Service for real-time KYC verification (HTTP + Events)
- Orchestrate approval workflows via Camunda 8 (Zeebe) with human tasks
- Generate agreements via JasperReports with MinIO storage and cryptographic hashing
- Stream audit events to AdminService via RabbitMQ for centralized compliance reporting
- Maintain existing REST API contracts for backward compatibility

**Success Criteria**:
- All existing loan CRUD operations continue functioning without regression
- New loans automatically route through Camunda workflow when `EnableWorkflowOrchestration=true`
- KYC-unverified clients blocked from loan origination with clear user messaging
- Product configurations loaded dynamically from Vault with EAR validation
- Dual control prevents self-approval at both workflow and API layers
- Complete audit trail maintained for every loan state transition
- Sub-24-hour application-to-decision time target achieved

---

### Story 1.1: Database Schema Enhancement for Loan Versioning

**As a** system architect,  
**I want** to extend the LoanApplications table with versioning columns and loan number sequencing,  
**so that** the system can maintain immutable audit history of all loan state changes and generate predictable, auditable loan numbers.

**Acceptance Criteria**:
1. ALTER TABLE migration adds versioning columns (LoanNumber, Version, ParentVersionId, IsCurrentVersion, ModificationReason, SnapshotJson, AgreementFileHash, AgreementMinioPath) as NULLABLE to LoanApplications table
2. LoanNumberSequence table created with BranchCode, Year, NextSequence columns for thread-safe sequence generation
3. Backfill script generates LoanNumber for existing records using format `LUS-2025-{sequence}` ordered by CreatedAtUtc
4. Repository interface extended with `GetCurrentVersionAsync(loanNumber)` and `GetVersionHistoryAsync(loanNumber)` methods
5. All existing queries using `GetByIdAsync(guid)` continue working with <10% performance impact
6. Unit tests verify sequence generation is thread-safe for concurrent requests
7. Rollback migration script tested to remove columns without data loss

**Integration Verification**:
- **IV1**: Run existing integration tests for LoanApplicationRepository - all must pass without modification
- **IV2**: Query existing loan records using both old `GetByIdAsync` and new `GetCurrentVersionAsync` - results must be consistent
- **IV3**: Execute backfill script on copy of production data - verify all loans receive unique loan numbers

**Dependencies**: None (foundation story)

**Estimated Effort**: 1 development session (4-6 hours)

---

### Story 1.2: Loan Number Generation and Versioning Service

**As a** loan officer,  
**I want** the system to automatically assign unique, auditable loan numbers to new applications,  
**so that** I can reference loans by human-readable identifiers and the system maintains version history for compliance.

**Acceptance Criteria**:
1. `LoanVersioningService` implemented with `GenerateLoanNumberAsync(branchCode)` method producing format `{BranchCode}-{Year}-{Sequence}`
2. Sequence generation is thread-safe using database-level locking (SQL UPDATE with OUTPUT clause)
3. `CreateNewVersionAsync(loanId, reason, changes)` creates new version record, marks previous version IsCurrentVersion=false, stores JSON snapshot of previous state
4. LoanApplicationService.CreateApplicationAsync calls GenerateLoanNumberAsync to assign LoanNumber on new loans
5. API response includes LoanNumber field (backward compatible - new optional field)
6. Unit tests verify concurrent loan creation assigns unique sequential numbers
7. Integration test creates loan, modifies it twice, verifies version history chain (3 versions total)

**Integration Verification**:
- **IV1**: Create loan via existing `/api/loanapplication` endpoint - verify LoanNumber present in response, existing fields unchanged
- **IV2**: Retrieve loan by Id using existing endpoint - verify all existing fields intact, new LoanNumber field added
- **IV3**: Load test with 50 concurrent loan creations - verify all receive unique sequential loan numbers

**Dependencies**: Story 1.1 (database schema must exist)

**Estimated Effort**: 1 development session (6-8 hours)

---

### Story 1.3: Vault Product Configuration Service Integration

**As a** system administrator,  
**I want** loan product configurations (interest rates, EAR caps, eligibility rules) stored in Vault and loaded dynamically,  
**so that** we can update product rules without code deployment and enforce EAR compliance automatically.

**Acceptance Criteria**:
1. `VaultProductConfigService` implemented with `GetProductConfigAsync(productCode)` fetching from `kv/intellifin/loan-products/{productCode}/rules`
2. In-memory caching with 5-minute TTL using IMemoryCache
3. EAR compliance validation on config load - throw `ComplianceException` if calculatedEAR > earLimit (48%)
4. LoanProductService refactored to use VaultProductConfigService instead of hardcoded product definitions
5. Vault schema documented with example configurations for GEPL-001 and SMEABL-001 products
6. Configuration change workflow documented (dual control approval process for Vault updates)
7. Unit tests verify EAR validation rejects non-compliant configs, accepts compliant configs
8. Integration test loads config from actual Vault instance (Testcontainers), verifies caching behavior

**Integration Verification**:
- **IV1**: Create loan application via existing endpoint - verify product validation uses Vault config (update Vault config, wait 5 minutes, verify new limits enforced)
- **IV2**: Attempt to load product with EAR >48% from Vault - verify ComplianceException thrown, loan creation blocked
- **IV3**: Existing loans created before Vault migration remain queryable and valid (backward compatibility)

**Dependencies**: Story 1.2 (versioning service operational for config version tracking)

**Estimated Effort**: 1-2 development sessions (8-10 hours including Vault schema design)

---

### Story 1.4: Client Management Service Integration for KYC Verification

**As a** loan officer,  
**I want** the system to automatically check client KYC status when I select a client for loan application,  
**so that** I'm prevented from initiating loans for unverified clients and maintain compliance.

**Acceptance Criteria**:
1. `ClientManagementClient` HTTP client implemented with `GetClientVerificationAsync(clientId)` calling `/api/clients/{id}/verification`
2. Circuit breaker configured (3 retries, 30-second timeout, fallback to manual verification)
3. LoanApplicationService.CreateApplicationAsync calls ClientManagementClient before creating loan
4. If KYC status != "Approved", throw `KycNotVerifiedException` with client-friendly message
5. If KYC approved date >12 months ago, throw `KycExpiredException` with renewal guidance
6. API endpoint returns 400 Bad Request with error code `KYC_NOT_VERIFIED` or `KYC_EXPIRED`
7. Unit tests verify exception handling for all KYC states (Pending, Expired, Revoked, Not Found)
8. Integration test mocks Client Management API responses, verifies blocking behavior

**Integration Verification**:
- **IV1**: Create loan for KYC-verified client - succeeds as before (no breaking change)
- **IV2**: Attempt to create loan for non-verified client - receives clear error message with KYC completion link
- **IV3**: Simulate Client Management Service downtime - verify circuit breaker activates, fallback message displayed

**Dependencies**: Story 1.3 (Vault config includes KYC expiration policy)

**Estimated Effort**: 1 development session (6-8 hours)

---

### Story 1.5: Camunda BPMN Workflow Design and Deployment

**As a** system architect,  
**I want** a comprehensive BPMN workflow deployed to Camunda that orchestrates the loan lifecycle,  
**so that** approval routing, compliance gates, and agreement generation are process-driven rather than code-driven.

**Acceptance Criteria**:
1. `loan-origination-process.bpmn` designed with sequence: Start → KYC Gate → Application → Assessment → Approval Routing → Agreement → Disbursement → Active
2. Exclusive gateway for approval routing with conditions: Amount ≤50K AND Grade A/B → Credit Analyst; Amount >50K OR Grade C → Head of Credit; Amount >250K OR Grade D/F → Dual Control (Head + CEO)
3. Human tasks defined with candidate groups: `credit-analysts`, `head-of-credit`, `ceo`, `finance-officers`
4. Service tasks defined with job types: `verify-kyc`, `request-credit-assessment`, `generate-agreement`, `execute-disbursement`
5. `BpmnDeploymentService` implemented as IHostedService to deploy BPMN on startup, log deployment key
6. WorkflowService.StartLoanOriginationWorkflowAsync creates process instance with variables: `{applicationId, clientId, loanAmount, riskGrade}`
7. BPMN validated using Camunda Modeler, no validation errors
8. Unit test verifies BpmnDeploymentService reads BPMN file, calls Zeebe deploy API

**Integration Verification**:
- **IV1**: Deploy service with BPMN - verify startup logs show successful deployment with workflow key
- **IV2**: Start workflow instance via WorkflowService - verify Camunda Operate shows active instance
- **IV3**: Existing CRUD APIs continue working (workflow disabled via feature flag initially)

**Dependencies**: Story 1.4 (KYC verification logic must exist for KYC Gate task)

**Estimated Effort**: 2 development sessions (10-12 hours including BPMN design iteration)

---

### Story 1.6: KYC Verification Worker and Event Subscription

**As a** compliance officer,  
**I want** active loan workflows to pause automatically if client KYC status is revoked,  
**so that** we maintain continuous compliance and prevent disbursement to unverified clients.

**Acceptance Criteria**:
1. `KycVerificationWorker` implemented as Zeebe job worker handling `verify-kyc` tasks
2. Worker calls ClientManagementClient.GetClientVerificationAsync, throws workflow error if not verified/expired
3. `ClientKycRevokedConsumer` subscribes to `ClientKycRevoked` events from RabbitMQ
4. Consumer calls WorkflowService.PauseLoansByClientAsync to suspend all active workflows for affected client
5. Consumer publishes `LoansAutoPausedDueToKycRevocation` audit event to AdminService
6. Worker is idempotent - can safely retry on transient failures without duplicate checks
7. Unit tests verify worker error handling for all KYC failure scenarios
8. Integration test publishes ClientKycRevoked event, verifies loans paused, audit event published

**Integration Verification**:
- **IV1**: Start loan workflow for verified client - workflow progresses past KYC gate successfully
- **IV2**: Start loan workflow for unverified client - workflow terminates with KYC_NOT_VERIFIED error
- **IV3**: Revoke client KYC while loan workflow active - verify workflow pauses, user notified

**Dependencies**: Story 1.5 (BPMN with KYC gate task must be deployed)

**Estimated Effort**: 1-2 development sessions (8-10 hours)

---

### Story 1.7: Dual Control Validation Service and Workflow Enforcement

**As a** compliance officer,  
**I want** the system to prevent loan approvers from approving their own loan applications,  
**so that** we maintain segregation of duties and meet Bank of Zambia regulatory requirements.

**Acceptance Criteria**:
1. `DualControlValidator` service implemented with `ValidateApprovalAsync(applicationId, approverUserId)` method
2. Validator checks: approver != creator, approver != assessor; throws `DualControlViolationException` if match
3. Validator publishes `LoanApprovalAttempted` audit event with IP address, timestamp, outcome
4. BPMN approval tasks updated with assignee exclusion: `candidateGroups="credit-analysts" excludeUsers="#{createdBy}"`
5. LoanApplicationService.ApproveApplicationAsync calls DualControlValidator before processing approval (API-level defense)
6. API returns 403 Forbidden with error code `DUAL_CONTROL_VIOLATION` if validation fails
7. Unit tests verify all violation scenarios (self-approval, approver-as-assessor)
8. Integration test simulates same user attempting approval via API, via Camunda Tasklist - both blocked

**Integration Verification**:
- **IV1**: Different user approves loan - succeeds as normal (no breaking change to valid approvals)
- **IV2**: Creator attempts to approve via API - blocked with clear error message
- **IV3**: Assessor assigned approval task in Camunda Tasklist - task does not appear in their queue (filtered by excludeUsers)

**Dependencies**: Story 1.6 (workflow orchestration operational for task assignment)

**Estimated Effort**: 1 development session (6-8 hours)

---

### Story 1.8: Agreement Generation Service with JasperReports Integration

**As a** loan officer,  
**I want** loan agreements automatically generated and stored when loans are approved,  
**so that** I can immediately download agreements for client signature without manual document preparation.

**Acceptance Criteria**:
1. `AgreementGenerationService` implemented with `GenerateAgreementAsync(applicationId)` method
2. Service calls JasperReports API `POST /rest_v2/reports/intellifin/loan-agreements/{template}.pdf` with loan data JSON
3. Service computes SHA256 hash of generated PDF for integrity verification
4. Service stores PDF in MinIO at `loan-agreements/{clientId}/{loanNumber}_v{version}.pdf`
5. Service updates LoanApplication record with AgreementFileHash and AgreementMinioPath
6. `GenerateAgreementWorker` implemented handling `generate-agreement` Camunda tasks
7. Worker has 10-second timeout, throws incident if JasperReports times out (manual retry by operations)
8. JasperReports template created for GEPL-001 product (basic layout with loan terms, repayment schedule)

**Integration Verification**:
- **IV1**: Approve loan via workflow - verify agreement generates, MinIO contains PDF, database has hash
- **IV2**: Download agreement via API `/api/loanapplication/{id}/agreement` - verify PDF content matches loan terms
- **IV3**: Simulate JasperReports timeout - verify workflow incident created, approval not rolled back (loan still approved, agreement retry-able)

**Dependencies**: Story 1.7 (dual control approval must succeed before agreement generation)

**Estimated Effort**: 2 development sessions (12-14 hours including template design)

---

### Story 1.9: Comprehensive Audit Event Publishing to AdminService

**As a** compliance auditor,  
**I want** all loan state changes published as audit events to AdminService,  
**so that** I have centralized, immutable audit trails for regulatory reporting.

**Acceptance Criteria**:
1. `AuditEventPublisher` service implemented with `PublishEventAsync<T>(event)` generic method
2. Publisher uses MassTransit IPublishEndpoint to publish to RabbitMQ exchange `intellifin.loan.events`
3. Event records defined for: LoanApplicationCreated, LoanApplicationSubmitted, LoanCreditAssessmentCompleted, LoanApprovalGranted, LoanApprovalRejected, LoanAgreementGenerated, LoanDisbursed, LoanVersionCreated, LoanWorkflowStateChanged
4. All events include CorrelationId (from ICorrelationIdProvider) for distributed tracing
5. LoanApplicationService publishes LoanApplicationCreated on every new loan
6. Approval workers publish LoanApprovalGranted/Rejected events
7. Agreement worker publishes LoanAgreementGenerated event
8. RabbitMQ exchange configured with durable queues, dead-letter exchange for failed deliveries
9. Integration test publishes events, verifies RabbitMQ receives them (Testcontainers)

**Integration Verification**:
- **IV1**: Create loan via API - verify LoanApplicationCreated event in RabbitMQ queue
- **IV2**: Complete loan workflow end-to-end - verify all 6+ events published in sequence
- **IV3**: Simulate AdminService consumer failure - verify events route to dead-letter queue (at-least-once delivery)

**Dependencies**: Story 1.8 (agreement generation triggers final audit events)

**Estimated Effort**: 2 development sessions (10-12 hours)

---

### Story 1.10: End-to-End Workflow Testing and Feature Flag Activation

**As a** product owner,  
**I want** comprehensive E2E tests validating the complete loan workflow,  
**so that** we can confidently activate workflow orchestration in production.

**Acceptance Criteria**:
1. Playwright E2E test: Happy path - KYC verified client → Application → Auto Credit Assessment → Approval (Credit Analyst) → Agreement Generation → Complete
2. Playwright E2E test: KYC block path - Unverified client → Application blocked with clear error message
3. Playwright E2E test: Dual control path - High-value loan → Approval (Head of Credit) → Second Approval (CEO) → Agreement
4. Playwright E2E test: Self-approval block - Loan creator attempts approval → Blocked at both UI and API
5. Load test: 100 concurrent loan applications → All receive unique loan numbers, workflows complete within 24 hours target
6. Rollback test: Disable feature flag `EnableWorkflowOrchestration=false` → CRUD APIs work as before
7. Feature flag activated in staging environment, smoke tests pass
8. Monitoring dashboards configured for workflow metrics (instances by state, approval latencies, agreement generation duration)

**Integration Verification**:
- **IV1**: Run full regression test suite for existing CRUD APIs - 100% pass rate
- **IV2**: Run new E2E workflow tests - 100% pass rate
- **IV3**: Verify monitoring dashboards show real-time workflow state

**Dependencies**: Story 1.9 (all components operational for E2E testing)

**Estimated Effort**: 2-3 development sessions (16-20 hours including test automation)

---

## Epic Summary

- **Total Stories**: 10
- **Estimated Duration**: 8-10 weeks (with parallelization, testing, buffer)
- **Dependencies**: Sequential with clear dependency chain
- **Risk Mitigation**: Each story includes integration verification to ensure existing functionality intact
- **Rollback**: Feature flag allows disabling workflow orchestration without rollback deployment

---

## Document Approval

- **Product Manager**: [Name] - [Date]
- **Technical Lead**: [Name] - [Date]
- **Architect**: [Name] - [Date]
- **Compliance Officer**: [Name] - [Date]

---

**Document Control**: This PRD must be reviewed and approved by all stakeholders before epic implementation begins. Updates to requirements must be documented in the Change Log section.
