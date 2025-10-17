# Loan Origination Module - Brownfield Architecture Document

## Document Information

- **Document Type**: Brownfield Architecture Analysis
- **Module**: Loan Origination
- **Version**: 1.0
- **Last Updated**: 2025-10-17
- **Owner**: Architecture Team
- **Purpose**: Document CURRENT state and transformation requirements for workflow-driven, compliance-embedded origination engine

## Executive Summary

This document captures the **ACTUAL CURRENT STATE** of the Loan Origination module and defines the architectural transformation required to evolve it from a basic CRUD-based loan system into a fully automated, workflow-driven origination engine with deep compliance integration.

### Enhancement Scope

**Current State**: Basic loan application scaffolding with hardcoded business logic  
**Target State**: Process-aware, compliance-embedded origination engine orchestrated by Camunda

**Key Transformation Goals**:
- Move from transactional to workflow-driven processing
- Enforce KYC/AML gating from Client Management service
- Externalize product rules to Vault configuration
- Implement dual control at workflow and service layers
- Enable loan versioning for audit compliance
- Automate agreement generation with JasperReports
- Integrate comprehensive audit event streaming to AdminService

### Change Log

| Date       | Version | Description                      | Author     |
|------------|---------|----------------------------------|------------|
| 2025-10-17 | 1.0     | Initial brownfield analysis      | Architect  |

---

## Quick Reference - Critical Files and Entry Points

### Existing Implementation Files

**Service Entry Point**:
- `apps/IntelliFin.LoanOriginationService/Program.cs` - Service bootstrapping, DI configuration, Zeebe client setup

**Core Service Layer**:
- `Services/LoanApplicationService.cs` - Basic CRUD operations, product validation
- `Services/CreditAssessmentService.cs` - Risk scoring with mock Bureau data
- `Services/ComplianceService.cs` - BoZ compliance validation, hardcoded rules
- `Services/RiskCalculationEngine.cs` - Scoring algorithm implementation
- `Services/WorkflowService.cs` - Minimal Camunda integration (process start only)

**Controllers**:
- `Controllers/LoanApplicationController.cs` - REST API endpoints for loan applications
- `Controllers/LoanProductController.cs` - Product catalog endpoints

**Domain Models**:
- `Models/LoanApplicationModels.cs` - Application, CreditAssessment, WorkflowStep entities
- `Models/BusinessRuleModels.cs` - Product rules, validation logic
- `Models/ValidationModels.cs` - Validation result structures

**Camunda Integration**:
- `Workers/InitialValidationWorker.cs` - Zeebe external task worker

### Domain Documentation (Business Process Definitions)

**Existing Business Process Documents** (in `docs/domains/loan-origination/`):
- `loan-application-workflow.md` - Current intake and underwriter review process
- `loan-approval-workflow.md` - Camunda-based approval routing rules (dual control defined)
- `loan-product-catalog.md` - GEPL and SMEABL product definitions with EAR compliance
- `credit-assessment-process.md` - Risk grading methodology (A-F grades)
- `loan-disbursement-process.md` - Disbursement workflow with Tingg/PMEC integration
- `loan-documentation.md` - Document checklist and verification requirements

### Infrastructure Dependencies

**External Services**:
- Vault - Control Plane (existing infrastructure, needs product config extension)
- Camunda 8 (Zeebe) - Workflow engine (client configured, workflows not deployed)
- MinIO - Document storage (existing infrastructure)
- JasperReports - Agreement generation (existing infrastructure)
- AdminService - Audit event bus (existing service)
- ClientManagementService - KYC/AML verification (recently completed module)
- RabbitMQ - MassTransit messaging infrastructure (configured)

---

## High-Level Architecture

### Current State - Basic CRUD Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Next.js Frontend                            │
│         (Loan officer enters application data manually)         │
└───────────────────────────┬─────────────────────────────────────┘
                            │ HTTP REST
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│           LoanApplicationController (REST API)                  │
│  - POST /api/loanapplication (create draft)                    │
│  - POST /{id}/submit (submit for review)                       │
│  - POST /{id}/approve (direct approval - NO WORKFLOW)          │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│              LoanApplicationService (Business Logic)            │
│  ❌ Product rules HARDCODED in code                            │
│  ❌ No KYC verification check                                  │
│  ❌ Approval logic in service layer (not workflow)             │
│  ❌ No versioning                                              │
│  ❌ No audit events                                            │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                     SQL Server Database                         │
│            (LoanApplication table - single version)             │
└─────────────────────────────────────────────────────────────────┘
```

**Zeebe Client Configured But Underutilized**:
- `WorkflowService.StartApprovalWorkflowAsync()` starts a process but NO BPMN deployed
- No human tasks, no approval routing, no compliance gates

### Target State - Workflow-Driven Compliance Engine

```
┌─────────────────────────────────────────────────────────────────┐
│                     Next.js Frontend                            │
│         (Loan application wizard with real-time validation)     │
└───────────────────────────┬─────────────────────────────────────┘
                            │ HTTP REST
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│           LoanApplicationController (Thin API Layer)            │
│  - POST /api/loanapplication → Start Camunda Workflow          │
│  - GET /{id} → Query loan state + workflow status              │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Camunda 8 (Zeebe Engine)                     │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ BPMN: loan-origination-process                            │  │
│  │                                                           │  │
│  │  Start → KYC Gate → Application → Assessment →           │  │
│  │          Approval (dual control) → Agreement →           │  │
│  │          Disbursement → Active                           │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                 │
│  Human Tasks:                                                   │
│  - Credit Assessment Review (assigned to Credit Analyst group)  │
│  - Approval (routed by amount + risk grade)                    │
│  - Disbursement Authorization (Finance group)                   │
└──────────┬──────────────────┬───────────────┬──────────────┬────┘
           │                  │               │              │
           │ External Task    │ Service Task  │ Event        │ Event
           ▼                  ▼               ▼              ▼
┌──────────────────┐  ┌──────────────┐  ┌─────────────┐  ┌──────────────┐
│ Credit           │  │ Vault        │  │ Client Mgmt │  │ JasperReports│
│ Assessment       │  │ Product      │  │ Service     │  │ Agreement    │
│ Service          │  │ Config       │  │ KYC API     │  │ Generation   │
│ (Separate MS)    │  │ (Dynamic)    │  │ + Events    │  │              │
└──────────────────┘  └──────────────┘  └─────────────┘  └──────────────┘
           │                  │               │              │
           │ Risk Score       │ EAR Rules     │ Verified?    │ PDF
           ▼                  ▼               ▼              ▼
┌─────────────────────────────────────────────────────────────────┐
│              LoanOriginationService (Orchestrator)              │
│  ✅ Publishes audit events to AdminService                     │
│  ✅ Enforces dual control at API layer (defense in depth)      │
│  ✅ Stores loan versions (immutable history)                   │
│  ✅ Validates against Vault product rules                      │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                SQL Server + MinIO + AdminService                │
│  - LoanApplication (versioned records)                          │
│  - LoanAgreements (MinIO with hash in AdminService)            │
│  - AuditEvents stream (state changes, approvals, rejections)    │
└─────────────────────────────────────────────────────────────────┘
```

---

## Current State Analysis

### What EXISTS Today

#### ✅ Service Scaffolding and Infrastructure

**Program.cs Configuration** (Lines 1-103):
```csharp
// Database Context
builder.Services.AddDbContext<LmsDbContext>(...);

// Repositories registered
ILoanApplicationRepository, ILoanProductRepository, 
ICreditAssessmentRepository, IDocumentVerificationRepository

// Services registered
ILoanApplicationService, ICreditAssessmentService, IWorkflowService, 
IRiskCalculationEngine, IComplianceService

// Zeebe Client configured
builder.Services.AddSingleton<IZeebeClient>(provider => {
    return ZeebeClient.Builder()
        .UseGatewayAddress(configuration["Zeebe:GatewayAddress"])
        .UseOAuthCredentials(...)
        .Build();
});

// MassTransit for RabbitMQ messaging
builder.Services.AddMassTransit(x => {
    x.UsingRabbitMq((context, cfg) => { ... });
});

// InitialValidationWorker registered as HostedService
builder.Services.AddHostedService<InitialValidationWorker>();
```

**Status**: ✅ Foundation is solid, ready for enhancement

#### ✅ Domain Models

**LoanApplication Model** (`Models/LoanApplicationModels.cs`):
```csharp
public class LoanApplication
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string ProductCode { get; set; }
    public decimal RequestedAmount { get; set; }
    public int TermMonths { get; set; }
    public decimal InterestRate { get; set; }
    public LoanApplicationStatus Status { get; set; }
    public Dictionary<string, object> ApplicationData { get; set; }
    public CreditAssessment? CreditAssessment { get; set; }
    public string WorkflowInstanceId { get; set; }  // ✅ Camunda tracking field exists
    public List<WorkflowStep> WorkflowSteps { get; set; }
}
```

**Status**: ✅ Model supports workflow integration, needs versioning fields

#### ✅ Credit Assessment Engine

**CreditAssessmentService** implements full risk scoring:
- TransUnion integration point (mock data currently)
- Affordability calculation (DTI ratio, payment capacity)
- Risk grading (A-F based on credit score bands)
- Explanation generation

**RiskCalculationEngine** implements BoZ-aligned scoring model:
- Payment history (35% weight)
- DTI ratio (25% weight)
- Credit utilization (20% weight)
- Credit history length (10% weight)
- Account diversity (10% weight)
- Business rules for auto-decline scenarios

**Status**: ✅ Solid foundation, needs Vault-based rule configuration

#### ✅ Compliance Service

**ComplianceService** (`Services/ComplianceService.cs`):
- KYC validation logic (calls DocumentVerificationRepository)
- BoZ compliance checks (8 requirements tracked)
- EAR validation placeholder
- Document compliance validation

**Current Limitations**:
- Hardcoded compliance rules (not Vault-based)
- No real-time KYC gating from Client Management service
- EAR validation is placeholder only

**Status**: ✅ Framework exists, needs enhancement

#### ⚠️ Minimal Workflow Integration

**WorkflowService** (`Services/WorkflowService.cs`):
```csharp
public async Task<string> StartApprovalWorkflowAsync(Guid applicationId, ...)
{
    var processInstance = await _zeebeClient.NewCreateProcessInstanceCommand()
        .BpmnProcessId("loanOriginationProcess")
        .LatestVersion()
        .Variables(JsonSerializer.Serialize(new { applicationId = ... }))
        .WithResult()
        .Send(cancellationToken);
    
    return processInstance.ProcessInstanceKey.ToString();
}
```

**Problem**: 
- Only starts processes, no BPMN definition exists
- All other methods marked `[Obsolete]`
- No human task completion, no service task handlers
- No integration with approval authority matrix from docs

**Status**: ⚠️ Client configured but no workflows deployed

---

## What is MISSING for Enhancement

### ❌ 1. KYC/AML Gating

**Gap**: No verification before loan origination starts

**Required**:
```csharp
// NEW: KYC Verification Gate (Workflow Service Task)
public class KycVerificationWorker : IBackgroundJobHandler
{
    public async Task Handle(IJob job, CancellationToken cancellationToken)
    {
        var clientId = job.Variables["clientId"];
        
        // Real-time API call to Client Management Service
        var client = await _clientManagementClient.GetClientAsync(clientId);
        
        if (!client.KycStatus.IsApproved || !client.AmlStatus.IsCleared)
        {
            await _zeebeClient.NewThrowErrorCommand(job.Key)
                .ErrorCode("KYC_NOT_VERIFIED")
                .ErrorMessage($"Client KYC/AML not verified. Status: {client.KycStatus}")
                .Send(cancellationToken);
            return;
        }
        
        // Also check for expiration
        if (client.KycStatus.ApprovedAt < DateTime.UtcNow.AddMonths(-12))
        {
            await _zeebeClient.NewThrowErrorCommand(job.Key)
                .ErrorCode("KYC_EXPIRED")
                .ErrorMessage("KYC verification expired, renewal required")
                .Send(cancellationToken);
            return;
        }
        
        await _zeebeClient.NewCompleteJobCommand(job.Key)
            .Variables(JsonSerializer.Serialize(new { kycVerified = true }))
            .Send(cancellationToken);
    }
}

// NEW: Event Subscriber for KYC Status Changes
public class KycStatusEventConsumer : IConsumer<ClientKycStatusChanged>
{
    public async Task Consume(ConsumeContext<ClientKycStatusChanged> context)
    {
        // If KYC revoked, pause all active loans for this client
        if (context.Message.NewStatus == KycStatus.Revoked)
        {
            await _workflowService.PauseLoansByClientAsync(context.Message.ClientId);
            // Publish LoanApplicationPaused event to AdminService
        }
    }
}
```

**Impact**: 
- New Zeebe worker: `KycVerificationWorker.cs`
- New HTTP client: `IClientManagementClient`
- New MassTransit consumers: `ClientKycStatusChanged`, `ClientKycRevoked` events
- BPMN: Add service task after Start event

---

### ❌ 2. Vault-Based Product Configuration

**Gap**: Product rules are hardcoded in `LoanProductService`

**Current** (`LoanProductService.cs`):
```csharp
// Hardcoded product definitions
private readonly List<LoanProduct> _products = new()
{
    new() {
        Code = "GEPL-001",
        MaxAmount = 50000m,
        BaseInterestRate = 0.15m,  // ❌ HARDCODED
        // ...
    }
};
```

**Required Vault Schema**:
```
kv/intellifin/loan-products/
├── GEPL-001/
│   ├── rules.json
│   │   {
│   │     "productName": "Government Employee Payroll Loan",
│   │     "minAmount": 1000,
│   │     "maxAmount": 50000,
│   │     "minTermMonths": 1,
│   │     "maxTermMonths": 60,
│   │     "baseInterestRate": 0.12,
│   │     "adminFee": 0.02,
│   │     "managementFee": 0.01,
│   │     "calculatedEAR": 0.152,
│   │     "earCapCompliance": true,
│   │     "earLimit": 0.48,
│   │     "eligibilityRules": {
│   │       "requiredKycStatus": "Approved",
│   │       "minMonthlyIncome": 5000,
│   │       "maxDtiRatio": 0.40,
│   │       "pmecRegistrationRequired": true
│   │     }
│   │   }
│   ├── version-history.json  // Track config changes
│   └── last-updated: "2025-10-17T10:00:00Z"
│
└── SMEABL-001/
    └── rules.json
```

**New Service**: `VaultProductConfigService.cs`
```csharp
public class VaultProductConfigService : IProductConfigService
{
    private readonly IVaultClient _vaultClient;
    private readonly IMemoryCache _cache;
    
    public async Task<LoanProductConfig> GetProductConfigAsync(string productCode)
    {
        var cacheKey = $"product-config:{productCode}";
        if (_cache.TryGetValue(cacheKey, out LoanProductConfig config))
            return config;
        
        // Read from Vault
        var secret = await _vaultClient.V1.Secrets.KeyValue.V2
            .ReadSecretAsync($"loan-products/{productCode}/rules");
        
        config = JsonSerializer.Deserialize<LoanProductConfig>(
            secret.Data.Data["rules"].ToString());
        
        // Validate EAR compliance before returning
        if (config.CalculatedEAR > config.EarLimit)
        {
            throw new ComplianceException(
                $"Product {productCode} EAR {config.CalculatedEAR:P} exceeds limit {config.EarLimit:P}");
        }
        
        // Cache for 5 minutes
        _cache.Set(cacheKey, config, TimeSpan.FromMinutes(5));
        return config;
    }
}
```

**Impact**:
- New service: `VaultProductConfigService.cs`
- New models: `LoanProductConfig.cs`, `EligibilityRules.cs`
- Vault Agent sidecar configuration (Helm chart update)
- Dual-control workflow for config updates in Vault

---

### ❌ 3. Dual Control Enforcement

**Gap**: Approval logic is a single API call, no workflow enforcement

**Current Problem** (`LoanApplicationController.cs` Line 203-227):
```csharp
[HttpPost("{applicationId:guid}/approve")]
public async Task<ActionResult> ApproveApplication(
    Guid applicationId,
    [FromBody] ApproveApplicationRequest request,
    CancellationToken cancellationToken = default)
{
    // ❌ PROBLEM: No check if approver == creator
    // ❌ PROBLEM: No workflow routing based on amount + risk grade
    var application = await _loanApplicationService
        .ApproveApplicationAsync(applicationId, request.ApprovedBy, cancellationToken);
    return Ok(application);
}
```

**Required**:

**Layer 1: Camunda Workflow Enforcement** (BPMN)
```xml
<!-- loan-origination-process.bpmn -->
<bpmn:process id="loanOriginationProcess">
  
  <!-- Approval routing based on amount + risk grade -->
  <bpmn:exclusiveGateway id="ApprovalRoutingGateway">
    <bpmn:outgoing>toCreditAnalyst</bpmn:outgoing>
    <bpmn:outgoing>toHeadOfCredit</bpmn:outgoing>
    <bpmn:outgoing>toDualControl</bpmn:outgoing>
  </bpmn:exclusiveGateway>
  
  <!-- Route 1: Low risk, low amount -->
  <bpmn:sequenceFlow id="toCreditAnalyst" 
                     sourceRef="ApprovalRoutingGateway" 
                     targetRef="CreditAnalystApproval">
    <bpmn:conditionExpression>
      #{loanAmount &lt;= 50000 and riskGrade in ['A', 'B']}
    </bpmn:conditionExpression>
  </bpmn:sequenceFlow>
  
  <bpmn:userTask id="CreditAnalystApproval" name="Credit Analyst Approval">
    <bpmn:extensionElements>
      <zeebe:assignmentDefinition 
        candidateGroups="credit-analysts" 
        assignee="#{null}" />
      
      <!-- ✅ SEGREGATION OF DUTIES: Cannot be same as creator -->
      <zeebe:taskDefinition type="approve-loan" />
      <zeebe:ioMapping>
        <zeebe:input source="=createdBy" target="originalCreator" />
        <zeebe:output source="=approvedBy" target="approver" />
      </zeebe:ioMapping>
      
      <!-- Custom form for approval decision -->
      <zeebe:formDefinition formKey="camunda-forms:loan-approval-form.json" />
    </bpmn:extensionElements>
  </bpmn:userTask>
  
  <!-- Route 2: High risk or high amount → Dual Control -->
  <bpmn:sequenceFlow id="toDualControl" 
                     sourceRef="ApprovalRoutingGateway" 
                     targetRef="HeadOfCreditApproval">
    <bpmn:conditionExpression>
      #{loanAmount > 250000 or riskGrade in ['D', 'F']}
    </bpmn:conditionExpression>
  </bpmn:sequenceFlow>
  
  <bpmn:userTask id="HeadOfCreditApproval" name="Head of Credit Approval (First)">
    <bpmn:extensionElements>
      <zeebe:assignmentDefinition candidateGroups="head-of-credit" />
    </bpmn:extensionElements>
  </bpmn:userTask>
  
  <!-- Second approval required -->
  <bpmn:userTask id="CEOApproval" name="CEO Approval (Second)">
    <bpmn:extensionElements>
      <zeebe:assignmentDefinition candidateGroups="ceo" />
    </bpmn:extensionElements>
  </bpmn:userTask>
  
</bpmn:process>
```

**Layer 2: Service Layer Guard** (Defense in Depth)
```csharp
// NEW: DualControlValidator.cs
public class DualControlValidator : IDualControlValidator
{
    private readonly ILoanApplicationRepository _applicationRepo;
    private readonly IUserContext _userContext;
    
    public async Task<bool> ValidateApprovalAsync(
        Guid applicationId, 
        string approver, 
        CancellationToken cancellationToken)
    {
        var application = await _applicationRepo.GetByIdAsync(applicationId, cancellationToken);
        
        // ✅ ENFORCE: Approver cannot be same as creator or assessor
        if (application.CreatedBy == approver)
        {
            throw new DualControlViolationException(
                "Approver cannot approve their own loan application");
        }
        
        if (application.CreditAssessment?.AssessedBy == approver)
        {
            throw new DualControlViolationException(
                "Approver cannot approve a loan they assessed");
        }
        
        // ✅ AUDIT: Log approval attempt
        await _auditService.PublishEventAsync(new LoanApprovalAttempted
        {
            ApplicationId = applicationId,
            ApproverUserId = approver,
            ApproverRole = _userContext.CurrentUserRole,
            Timestamp = DateTime.UtcNow,
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString()
        });
        
        return true;
    }
}
```

**Impact**:
- New BPMN: `loan-origination-process.bpmn` with routing logic
- New service: `DualControlValidator.cs`
- New exceptions: `DualControlViolationException`
- Camunda Forms: `loan-approval-form.json`
- API layer: Update `ApproveApplicationAsync` to call validator first

---

### ❌ 4. Loan Versioning

**Gap**: No immutable history when loan terms change

**Current Schema** (from `Shared.DomainModels`):
```sql
CREATE TABLE LoanApplications (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ClientId UNIQUEIDENTIFIER,
    Amount DECIMAL(18,2),
    Status NVARCHAR(50),
    CreatedAtUtc DATETIME2
    -- ❌ NO VERSION TRACKING
);
```

**Required Schema**:
```sql
CREATE TABLE LoanApplications (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ClientId UNIQUEIDENTIFIER,
    LoanNumber NVARCHAR(50) UNIQUE NOT NULL,  -- NEW: Branch-Year-Seq format
    Version INT NOT NULL DEFAULT 1,           -- NEW: Version number
    ParentVersionId UNIQUEIDENTIFIER NULL,    -- NEW: Link to previous version
    Amount DECIMAL(18,2),
    TermMonths INT,
    InterestRate DECIMAL(5,4),
    Status NVARCHAR(50),
    CreatedAtUtc DATETIME2,
    CreatedByUserId UNIQUEIDENTIFIER,
    ModifiedAtUtc DATETIME2,
    ModifiedReason NVARCHAR(500),             -- NEW: Why version was created
    IsCurrentVersion BIT DEFAULT 1,           -- NEW: Only one current per loan
    SnapshotJson NVARCHAR(MAX),               -- NEW: Complete state snapshot
    AgreementFileHash NVARCHAR(64) NULL,      -- NEW: SHA256 of agreement PDF
    AgreementMinioPath NVARCHAR(500) NULL,    -- NEW: MinIO object path
    
    CONSTRAINT UQ_CurrentVersion UNIQUE (LoanNumber, IsCurrentVersion)
    WHERE IsCurrentVersion = 1
);

-- NEW: Loan Number Sequence Table
CREATE TABLE LoanNumberSequence (
    BranchCode NVARCHAR(10) NOT NULL,
    Year INT NOT NULL,
    NextSequence INT NOT NULL,
    PRIMARY KEY (BranchCode, Year)
);
```

**New Service**: `LoanVersioningService.cs`
```csharp
public class LoanVersioningService : ILoanVersioningService
{
    public async Task<LoanApplication> CreateNewVersionAsync(
        Guid loanId, 
        string modificationReason,
        Dictionary<string, object> changes,
        CancellationToken cancellationToken)
    {
        var currentVersion = await _applicationRepo.GetCurrentVersionAsync(loanId);
        
        // Mark current version as no longer current
        currentVersion.IsCurrentVersion = false;
        await _applicationRepo.UpdateAsync(currentVersion, cancellationToken);
        
        // Create new version
        var newVersion = new LoanApplication
        {
            Id = Guid.NewGuid(),
            LoanNumber = currentVersion.LoanNumber,  // Same loan number
            Version = currentVersion.Version + 1,
            ParentVersionId = currentVersion.Id,
            ClientId = currentVersion.ClientId,
            Amount = changes.ContainsKey("amount") ? (decimal)changes["amount"] : currentVersion.Amount,
            TermMonths = changes.ContainsKey("termMonths") ? (int)changes["termMonths"] : currentVersion.TermMonths,
            // ... copy all fields, applying changes
            ModifiedReason = modificationReason,
            ModifiedAtUtc = DateTime.UtcNow,
            IsCurrentVersion = true,
            SnapshotJson = JsonSerializer.Serialize(currentVersion)  // Snapshot of previous state
        };
        
        await _applicationRepository.CreateAsync(newVersion, cancellationToken);
        
        // Publish versioning event to AdminService for audit trail
        await _auditService.PublishEventAsync(new LoanVersionCreated
        {
            LoanNumber = newVersion.LoanNumber,
            PreviousVersion = currentVersion.Version,
            NewVersion = newVersion.Version,
            ModificationReason = modificationReason,
            ChangedFields = changes.Keys.ToList(),
            ModifiedBy = _userContext.CurrentUserId,
            Timestamp = DateTime.UtcNow
        });
        
        return newVersion;
    }
    
    public async Task<string> GenerateLoanNumberAsync(string branchCode)
    {
        var year = DateTime.UtcNow.Year;
        
        // Get next sequence number (thread-safe)
        var sequence = await _loanNumberRepo.GetNextSequenceAsync(branchCode, year);
        
        // Format: Branch-Year-Sequence (e.g., LUS-2025-00123)
        return $"{branchCode}-{year}-{sequence:D5}";
    }
}
```

**Impact**:
- New service: `LoanVersioningService.cs`
- Database migration: Add versioning columns
- New repository methods: `GetCurrentVersionAsync`, `GetVersionHistoryAsync`
- Update all state-changing operations to create versions

---

### ❌ 5. Agreement Generation with JasperReports

**Gap**: No automated agreement generation

**Required Integration**:

**New Service**: `AgreementGenerationService.cs`
```csharp
public class AgreementGenerationService : IAgreementGenerationService
{
    private readonly HttpClient _jasperClient;
    private readonly IMinioClient _minioClient;
    private readonly IAuditService _auditService;
    
    public async Task<AgreementDocument> GenerateAgreementAsync(
        Guid applicationId, 
        CancellationToken cancellationToken)
    {
        var application = await _applicationRepo.GetCurrentVersionAsync(applicationId);
        var product = await _vaultProductConfigService.GetProductConfigAsync(application.ProductCode);
        
        // Prepare data for JasperReports template
        var jasperPayload = new
        {
            loanNumber = application.LoanNumber,
            clientName = application.ClientName,
            principal = application.Amount,
            termMonths = application.TermMonths,
            interestRate = product.BaseInterestRate,
            adminFee = product.AdminFee,
            calculatedEAR = product.CalculatedEAR,
            repaymentSchedule = application.RepaymentSchedule,
            disbursementDate = DateTime.UtcNow,
            templateVersion = "GEPL-v2.1"  // Template versioning
        };
        
        // Call JasperReports Server API
        var response = await _jasperClient.PostAsJsonAsync(
            "/rest_v2/reports/intellifin/loan-agreements/gepl-agreement.pdf",
            jasperPayload,
            cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new AgreementGenerationException("JasperReports generation failed");
        }
        
        var pdfBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        
        // Calculate SHA256 hash for audit trail
        var hash = ComputeSha256Hash(pdfBytes);
        
        // Store in MinIO under client document folder
        var minioPath = $"loan-agreements/{application.ClientId}/{application.LoanNumber}_v{application.Version}.pdf";
        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket("intellifin-documents")
            .WithObject(minioPath)
            .WithStreamData(new MemoryStream(pdfBytes))
            .WithContentType("application/pdf")
            .WithObjectSize(pdfBytes.Length),
            cancellationToken);
        
        // Update loan record with agreement details
        application.AgreementFileHash = hash;
        application.AgreementMinioPath = minioPath;
        await _applicationRepo.UpdateAsync(application, cancellationToken);
        
        // Publish audit event to AdminService
        await _auditService.PublishEventAsync(new LoanAgreementGenerated
        {
            ApplicationId = applicationId,
            LoanNumber = application.LoanNumber,
            DocumentHash = hash,
            MinioPath = minioPath,
            TemplateVersion = "GEPL-v2.1",
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = _userContext.CurrentUserId
        });
        
        return new AgreementDocument
        {
            LoanNumber = application.LoanNumber,
            FileHash = hash,
            MinioPath = minioPath,
            GeneratedAt = DateTime.UtcNow
        };
    }
    
    private string ComputeSha256Hash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return Convert.ToHexString(hashBytes);
    }
}
```

**Camunda Service Task**:
```xml
<bpmn:serviceTask id="GenerateAgreement" name="Generate Loan Agreement">
  <bpmn:extensionElements>
    <zeebe:taskDefinition type="generate-agreement" />
  </bpmn:extensionElements>
</bpmn:serviceTask>
```

**Worker Implementation**:
```csharp
public class GenerateAgreementWorker : IBackgroundJobHandler
{
    public async Task Handle(IJob job, CancellationToken cancellationToken)
    {
        var applicationId = Guid.Parse(job.Variables["applicationId"]);
        
        try
        {
            var agreement = await _agreementService.GenerateAgreementAsync(
                applicationId, cancellationToken);
            
            await _zeebeClient.NewCompleteJobCommand(job.Key)
                .Variables(JsonSerializer.Serialize(new {
                    agreementGenerated = true,
                    agreementHash = agreement.FileHash,
                    minioPath = agreement.MinioPath
                }))
                .Send(cancellationToken);
        }
        catch (Exception ex)
        {
            await _zeebeClient.NewThrowErrorCommand(job.Key)
                .ErrorCode("AGREEMENT_GENERATION_FAILED")
                .ErrorMessage(ex.Message)
                .Send(cancellationToken);
        }
    }
}
```

**Impact**:
- New service: `AgreementGenerationService.cs`
- New worker: `GenerateAgreementWorker.cs`
- HTTP client: Configure `JasperReportsClient`
- MinIO integration: `IMinioClient` setup in DI
- Database fields: `AgreementFileHash`, `AgreementMinioPath`
- JasperReports templates: Version-controlled `.jrxml` files in separate repo

---

### ❌ 6. Comprehensive Audit Events to AdminService

**Gap**: No audit trail for state changes

**Current**: No event publishing at all

**Required**: Event-driven audit architecture

**New Events** (to be published to RabbitMQ → consumed by AdminService):
```csharp
// NEW: Audit Events Models
public record LoanApplicationCreated(
    Guid ApplicationId,
    string LoanNumber,
    Guid ClientId,
    string ProductCode,
    decimal Amount,
    int TermMonths,
    Guid CreatedBy,
    DateTime Timestamp,
    string CorrelationId);

public record LoanApplicationSubmitted(
    Guid ApplicationId,
    string LoanNumber,
    Guid SubmittedBy,
    DateTime Timestamp,
    string CorrelationId);

public record LoanCreditAssessmentCompleted(
    Guid ApplicationId,
    string LoanNumber,
    RiskGrade RiskGrade,
    decimal CreditScore,
    bool RecommendedForApproval,
    Guid AssessedBy,
    DateTime Timestamp,
    string CorrelationId);

public record LoanApprovalGranted(
    Guid ApplicationId,
    string LoanNumber,
    Guid ApprovedBy,
    string ApproverRole,
    decimal ApprovedAmount,
    int ApprovedTerm,
    string ApprovalLevel,  // "CreditAnalyst", "HeadOfCredit", "CEO"
    bool IsDualControlSecondApproval,
    DateTime Timestamp,
    string CorrelationId);

public record LoanApprovalRejected(
    Guid ApplicationId,
    string LoanNumber,
    Guid RejectedBy,
    string RejectionReason,
    RiskGrade RiskGrade,
    DateTime Timestamp,
    string CorrelationId);

public record LoanAgreementGenerated(
    Guid ApplicationId,
    string LoanNumber,
    string DocumentHash,
    string MinioPath,
    string TemplateVersion,
    Guid GeneratedBy,
    DateTime Timestamp,
    string CorrelationId);

public record LoanDisbursed(
    Guid ApplicationId,
    string LoanNumber,
    decimal DisbursedAmount,
    string PaymentMethod,
    string TransactionReference,
    Guid AuthorizedBy,
    DateTime Timestamp,
    string CorrelationId);

public record LoanVersionCreated(
    string LoanNumber,
    int PreviousVersion,
    int NewVersion,
    string ModificationReason,
    List<string> ChangedFields,
    Guid ModifiedBy,
    DateTime Timestamp,
    string CorrelationId);

public record LoanWorkflowStateChanged(
    Guid ApplicationId,
    string LoanNumber,
    string PreviousState,
    string NewState,
    string WorkflowInstanceKey,
    DateTime Timestamp,
    string CorrelationId);
```

**AuditService Integration**:
```csharp
// NEW: AuditEventPublisher.cs
public class AuditEventPublisher : IAuditEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    
    public async Task PublishEventAsync<T>(T auditEvent, CancellationToken cancellationToken) 
        where T : class
    {
        // Ensure CorrelationId is set (for distributed tracing)
        if (auditEvent is ICorrelatedEvent correlatedEvent && 
            string.IsNullOrEmpty(correlatedEvent.CorrelationId))
        {
            correlatedEvent.CorrelationId = _correlationIdProvider.GetCorrelationId();
        }
        
        // Publish to RabbitMQ exchange → AdminService consumes
        await _publishEndpoint.Publish(auditEvent, cancellationToken);
        
        _logger.LogInformation("Published audit event {EventType} for CorrelationId {CorrelationId}",
            typeof(T).Name, correlatedEvent.CorrelationId);
    }
}

// Usage in LoanApplicationService
public async Task<LoanApplicationResponse> CreateApplicationAsync(...)
{
    var application = // ... create application
    
    await _applicationRepository.CreateAsync(application, cancellationToken);
    
    // ✅ PUBLISH AUDIT EVENT
    await _auditPublisher.PublishEventAsync(new LoanApplicationCreated(
        application.Id,
        application.LoanNumber,
        application.ClientId,
        application.ProductCode,
        application.Amount,
        application.TermMonths,
        _userContext.CurrentUserId,
        DateTime.UtcNow,
        _correlationIdProvider.GetCorrelationId()
    ), cancellationToken);
    
    return MapToResponse(application);
}
```

**Impact**:
- New folder: `Events/` with all audit event records
- New service: `AuditEventPublisher.cs`
- Update all state-changing methods to publish events
- MassTransit configuration: Add exchange routing to AdminService queues

---

## Integration Architecture

### Client Management Service Integration

**Pattern**: Hybrid (API + Events)

**API Integration** (Real-time verification):
```csharp
// NEW: IClientManagementClient.cs
public interface IClientManagementClient
{
    Task<ClientVerificationStatus> GetClientVerificationAsync(Guid clientId);
    Task<ClientProfile> GetClientProfileAsync(Guid clientId);
}

public class ClientManagementClient : IClientManagementClient
{
    private readonly HttpClient _httpClient;
    
    public async Task<ClientVerificationStatus> GetClientVerificationAsync(Guid clientId)
    {
        var response = await _httpClient.GetAsync($"/api/clients/{clientId}/verification");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ClientVerificationStatus>();
    }
}
```

**Event Integration** (Continuous sync):
```csharp
// NEW: ClientKycEventConsumer.cs
public class ClientKycApprovedConsumer : IConsumer<ClientKycApproved>
{
    public async Task Consume(ConsumeContext<ClientKycApproved> context)
    {
        _logger.LogInformation("Client {ClientId} KYC approved, resuming paused loans", 
            context.Message.ClientId);
        
        // Resume any paused loan workflows for this client
        await _workflowService.ResumePausedLoansByClientAsync(context.Message.ClientId);
    }
}

public class ClientKycRevokedConsumer : IConsumer<ClientKycRevoked>
{
    public async Task Consume(ConsumeContext<ClientKycRevoked> context)
    {
        _logger.LogWarning("Client {ClientId} KYC revoked, pausing active loans", 
            context.Message.ClientId);
        
        // Pause all active loan workflows for this client
        await _workflowService.PauseLoansByClientAsync(
            context.Message.ClientId, 
            "KYC verification revoked");
        
        // Publish audit event
        await _auditPublisher.PublishEventAsync(new LoansAutoPausedDueToKycRevocation(
            context.Message.ClientId,
            DateTime.UtcNow
        ));
    }
}
```

**Impact**:
- New HTTP client: `ClientManagementClient.cs`
- New consumers: `ClientKycApprovedConsumer.cs`, `ClientKycRevokedConsumer.cs`
- MassTransit: Subscribe to `ClientKycApproved`, `ClientKycRevoked`, `ClientProfileUpdated`

---

### Credit Assessment Service Integration

**Pattern**: Separate Microservice (HTTP + Events)

**Service Contract**:
```csharp
// NEW: ICreditAssessmentServiceClient.cs
public interface ICreditAssessmentServiceClient
{
    Task<CreditAssessmentResult> RequestAssessmentAsync(
        CreditAssessmentRequest request);
    
    Task<CreditAssessmentResult> GetAssessmentAsync(Guid assessmentId);
}

// Request Model
public record CreditAssessmentRequest(
    Guid ApplicationId,
    Guid ClientId,
    decimal LoanAmount,
    int TermMonths,
    string ProductCode,
    Dictionary<string, object> ClientFinancials);

// Response Model
public record CreditAssessmentResult(
    Guid AssessmentId,
    RiskGrade RiskGrade,
    decimal CreditScore,
    decimal DebtToIncomeRatio,
    decimal PaymentCapacity,
    bool RecommendedForApproval,
    List<RiskFactor> Factors,
    List<string> Conditions,
    string Explanation);
```

**Workflow Integration**:
```xml
<bpmn:serviceTask id="RequestCreditAssessment" name="Credit Assessment">
  <bpmn:extensionElements>
    <zeebe:taskDefinition type="request-credit-assessment" />
  </bpmn:extensionElements>
</bpmn:serviceTask>
```

**Worker with Fallback**:
```csharp
public class CreditAssessmentWorker : IBackgroundJobHandler
{
    public async Task Handle(IJob job, CancellationToken cancellationToken)
    {
        var applicationId = Guid.Parse(job.Variables["applicationId"]);
        
        try
        {
            // Call Credit Assessment Service
            var result = await _creditAssessmentClient.RequestAssessmentAsync(
                new CreditAssessmentRequest(...));
            
            await _zeebeClient.NewCompleteJobCommand(job.Key)
                .Variables(JsonSerializer.Serialize(new {
                    assessmentCompleted = true,
                    riskGrade = result.RiskGrade.ToString(),
                    creditScore = result.CreditScore,
                    recommendedForApproval = result.RecommendedForApproval
                }))
                .Send(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Credit Assessment Service unavailable, routing to manual review");
            
            // ✅ FALLBACK: Route to manual review task in Camunda
            await _zeebeClient.NewCompleteJobCommand(job.Key)
                .Variables(JsonSerializer.Serialize(new {
                    assessmentCompleted = false,
                    requiresManualReview = true,
                    failureReason = "Service unavailable"
                }))
                .Send(cancellationToken);
        }
    }
}
```

**Event Subscription** (Async notification):
```csharp
public class CreditAssessmentCompletedConsumer : IConsumer<CreditAssessmentCompleted>
{
    public async Task Consume(ConsumeContext<CreditAssessmentCompleted> context)
    {
        // Update local cache of assessment results
        await _cacheService.SetAsync(
            $"assessment:{context.Message.ApplicationId}",
            context.Message,
            TimeSpan.FromHours(24));
    }
}
```

**Impact**:
- New HTTP client: `CreditAssessmentServiceClient.cs`
- New worker: `CreditAssessmentWorker.cs`
- Fallback logic: Route to manual review if service down
- Event consumer: `CreditAssessmentCompletedConsumer.cs`

---

## BPMN Workflow Design

### Loan Origination Process (High-Level)

```
┌─────────────────────────────────────────────────────────────────┐
│                  Loan Origination Workflow                       │
└─────────────────────────────────────────────────────────────────┘

Start Event
    │
    ▼
[Service Task] Validate KYC/AML Status
    │ (Calls ClientManagementService API)
    │
    ├─── KYC Not Verified ──→ [Error End Event] "KYC_NOT_VERIFIED"
    │
    ▼
[Service Task] Validate Product Config from Vault
    │ (Fetches product rules, validates EAR compliance)
    │
    ├─── EAR Exceeds Limit ──→ [Error End Event] "EAR_COMPLIANCE_VIOLATION"
    │
    ▼
[Service Task] Initial Application Validation
    │ (Amount, term, required fields)
    │
    ▼
[User Task] Loan Officer Data Entry
    │ (Complete application details, upload documents)
    │ Assignee: loan-officers group
    │
    ▼
[Service Task] Request Credit Assessment
    │ (Calls CreditAssessmentService)
    │
    ├─── Service Down ──→ [User Task] Manual Credit Review
    │
    ▼
[User Task] Credit Assessment Review
    │ (Underwriter reviews risk grade, affordability)
    │ Assignee: credit-analysts group
    │
    ▼
[Exclusive Gateway] Approval Routing
    │
    ├─── Amount ≤ 50K AND Grade A/B ──→ [User Task] Credit Analyst Approval
    │                                           │
    │                                           ▼
    │                                    [Service Task] Dual Control Check
    │                                           │
    ├─── Amount > 50K OR Grade C ──────→ [User Task] Head of Credit Approval
    │                                           │
    │                                           ▼
    │                                    [Service Task] Dual Control Check
    │                                           │
    ├─── Amount > 250K OR Grade D/F ───→ [User Task] Head of Credit Approval (First)
                                                │
                                                ▼
                                         [Service Task] Dual Control Check
                                                │
                                                ▼
                                         [User Task] CEO Approval (Second)
                                                │
                                                ▼
                                         [Service Task] Dual Control Check
    │
    ▼
[Exclusive Gateway] Approval Decision
    │
    ├─── Approved ──→ [Service Task] Generate Loan Agreement
    │                       │ (JasperReports API call)
    │                       │
    │                       ▼
    │                 [Service Task] Store Agreement in MinIO
    │                       │
    │                       ▼
    │                 [Service Task] Publish AgreementGenerated Event
    │                       │
    │                       ▼
    │                 [User Task] Disbursement Authorization
    │                       │ (Finance Officer reviews and authorizes)
    │                       │ Assignee: finance-officers group
    │                       │
    │                       ▼
    │                 [Service Task] Execute Disbursement
    │                       │ (Calls Treasury/PMEC service)
    │                       │
    │                       ▼
    │                 [Service Task] Update Loan Status to Active
    │                       │
    │                       ▼
    │                 [Service Task] Publish LoanDisbursed Event
    │                       │
    │                       ▼
    │                 [End Event] Loan Active
    │
    └─── Rejected ───→ [Service Task] Publish LoanRejected Event
                            │
                            ▼
                      [End Event] Loan Rejected
```

**BPMN File Location**: 
- To be created: `apps/IntelliFin.LoanOriginationService/Workflows/loan-origination-process.bpmn`

**Deployment**:
```csharp
// NEW: BpmnDeploymentService.cs
public class BpmnDeploymentService : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bpmnPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
            "Workflows", "loan-origination-process.bpmn");
        
        var deployment = await _zeebeClient.NewDeployCommand()
            .AddResourceFile(bpmnPath)
            .Send(cancellationToken);
        
        _logger.LogInformation("Deployed BPMN process. Key: {ProcessDefinitionKey}", 
            deployment.Workflows.First().WorkflowKey);
    }
}
```

---

## Data Architecture Changes

### New Database Entities Required

```csharp
// NEW: LoanVersion.cs (in Shared.DomainModels.Entities)
public class LoanVersion
{
    public Guid Id { get; set; }
    public string LoanNumber { get; set; }  // Shared across versions
    public int Version { get; set; }
    public Guid? ParentVersionId { get; set; }
    public bool IsCurrentVersion { get; set; }
    
    // All loan fields (Amount, Term, Rate, etc.)
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
    public decimal InterestRate { get; set; }
    
    // Versioning metadata
    public DateTime CreatedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string ModificationReason { get; set; }
    public string SnapshotJson { get; set; }  // Complete previous state
    
    // Agreement tracking
    public string AgreementFileHash { get; set; }
    public string AgreementMinioPath { get; set; }
}

// NEW: LoanWorkflowState.cs
public class LoanWorkflowState
{
    public Guid Id { get; set; }
    public Guid LoanApplicationId { get; set; }
    public string WorkflowInstanceKey { get; set; }
    public string CurrentState { get; set; }
    public DateTime LastStateChange { get; set; }
    public Dictionary<string, object> WorkflowVariables { get; set; }
    public List<WorkflowEvent> Events { get; set; }
}

// NEW: LoanAuditTrail.cs
public class LoanAuditTrail
{
    public Guid Id { get; set; }
    public string LoanNumber { get; set; }
    public string EventType { get; set; }  // "Created", "Submitted", "Approved", "Rejected", "Disbursed"
    public Guid ActorUserId { get; set; }
    public string ActorRole { get; set; }
    public DateTime EventTimestamp { get; set; }
    public string EventDataJson { get; set; }  // Full event payload
    public string CorrelationId { get; set; }
}
```

### Migration Strategy

**Phase 1**: Add versioning columns to existing `LoanApplications` table
```sql
ALTER TABLE LoanApplications 
ADD LoanNumber NVARCHAR(50) NULL,
    Version INT DEFAULT 1,
    ParentVersionId UNIQUEIDENTIFIER NULL,
    IsCurrentVersion BIT DEFAULT 1,
    ModificationReason NVARCHAR(500) NULL,
    SnapshotJson NVARCHAR(MAX) NULL,
    AgreementFileHash NVARCHAR(64) NULL,
    AgreementMinioPath NVARCHAR(500) NULL;

-- Backfill loan numbers for existing records
UPDATE LoanApplications 
SET LoanNumber = 'LUS-2025-' + RIGHT('00000' + CAST(ROW_NUMBER() OVER (ORDER BY CreatedAtUtc) AS NVARCHAR), 5)
WHERE LoanNumber IS NULL;

-- Make LoanNumber required after backfill
ALTER TABLE LoanApplications 
ALTER COLUMN LoanNumber NVARCHAR(50) NOT NULL;
```

**Phase 2**: Create new audit and workflow tracking tables
```sql
CREATE TABLE LoanWorkflowStates (...);
CREATE TABLE LoanAuditTrail (...);
CREATE TABLE LoanNumberSequence (...);
```

---

## Technical Debt and Known Constraints

### 1. Mock Data in Services

**Issue**: Credit Assessment Service uses hardcoded mock Bureau data

**File**: `Services/CreditAssessmentService.cs` (Lines 12-41)
```csharp
private readonly Dictionary<Guid, CreditBureauData> _mockBureauData = new() { ... };
```

**Mitigation**: 
- Phase 1: Keep mock data, add configuration flag `UseMockBureauData: true`
- Phase 2 (Sprint 4): Integrate TransUnion Zambia API

### 2. Compliance Rules Hardcoded

**Issue**: `ComplianceService.cs` has hardcoded BoZ requirements

**File**: `Services/ComplianceService.cs` (Lines 46-96)
```csharp
private static readonly List<ComplianceRequirement> _bozRequirements = new() { ... };
```

**Mitigation**: Move to Vault configuration under `kv/intellifin/compliance/boz-requirements.json`

### 3. No BPMN Deployed

**Issue**: Zeebe client configured but no workflows exist

**Mitigation**: Critical blocker - BPMN design and deployment is first priority

### 4. Agreement Templates Not Created

**Issue**: JasperReports templates don't exist yet

**Mitigation**:
- Create `.jrxml` templates in separate repo under version control
- Implement template approval workflow (dual control for template changes)

### 5. No Correlation ID Propagation

**Issue**: Distributed tracing not implemented

**File**: Check if `CorrelationIdProvider` exists in `Shared.Infrastructure`

**Mitigation**: Add middleware to capture/generate correlation IDs for all requests

---

## Security and Compliance Considerations

### 1. Dual Control Enforcement

**Implementation**: Two layers (Camunda + API)
- ✅ Prevents self-approval even if workflow bypassed
- ✅ Audit trail includes both approvers with timestamps

### 2. EAR Compliance

**Enforcement Point**: Vault configuration validation
```csharp
if (config.CalculatedEAR > config.EarLimit)  // 48% hard limit
{
    throw new ComplianceException("EAR exceeds Money Lenders Act limit");
}
```

**Audit Trail**: All EAR validations logged to AdminService

### 3. Document Integrity

**Mechanism**: SHA256 hashing + MinIO WORM storage
- Agreement PDF hashed on generation
- Hash stored in database + AdminService audit event
- MinIO configured for immutability (cannot be modified/deleted)

### 4. KYC Expiration Handling

**Rule**: KYC valid for 12 months
```csharp
if (client.KycStatus.ApprovedAt < DateTime.UtcNow.AddMonths(-12))
{
    throw new KycExpiredException("KYC verification expired, renewal required");
}
```

**Workflow**: Automatic pause of loans if KYC revoked/expired

---

## Implementation Roadmap

### Phase 1: Foundation (Weeks 1-2)

1. **Vault Integration**
   - Extend Vault schema for product configs
   - Implement `VaultProductConfigService`
   - Migrate hardcoded product rules to Vault

2. **KYC Gating**
   - Create `ClientManagementClient` HTTP client
   - Implement KYC verification worker
   - Subscribe to KYC status events

3. **Database Versioning**
   - Run migration to add versioning columns
   - Implement `LoanVersioningService`
   - Implement loan number generation

### Phase 2: Workflow Core (Weeks 3-4)

4. **BPMN Design and Deployment**
   - Design `loan-origination-process.bpmn`
   - Implement all Zeebe workers (KYC, Assessment, Agreement, Disbursement)
   - Deploy and test workflow execution

5. **Dual Control Implementation**
   - Configure approval routing rules in BPMN
   - Implement `DualControlValidator` service
   - Add API-level enforcement

### Phase 3: Agreement Generation (Week 5)

6. **JasperReports Integration**
   - Design agreement templates (.jrxml)
   - Implement `AgreementGenerationService`
   - Configure MinIO storage and hashing
   - Implement template versioning workflow

### Phase 4: Audit and Events (Week 6)

7. **Comprehensive Audit Trail**
   - Define all audit event models
   - Implement `AuditEventPublisher`
   - Update all state-changing methods to publish events
   - Configure MassTransit routing to AdminService

### Phase 5: Credit Assessment Service Split (Week 7-8)

8. **Microservice Extraction**
   - Move `CreditAssessmentService` to separate service
   - Implement HTTP API contract
   - Add fallback logic in workflow
   - Configure service-to-service authentication

### Phase 6: Testing and Refinement (Week 9-10)

9. **Integration Testing**
   - End-to-end workflow tests
   - Dual control enforcement tests
   - KYC gating scenarios
   - Agreement generation validation

10. **Performance Optimization**
    - Vault caching strategy
    - Database query optimization
    - Camunda incident handling

---

## Files That Need Modification

### Existing Files to Update

1. **`Program.cs`**
   - Add `VaultProductConfigService` registration
   - Add `ClientManagementClient` registration
   - Add `AuditEventPublisher` registration
   - Configure additional Zeebe workers
   - Add BpmnDeploymentService as HostedService

2. **`LoanApplicationService.cs`**
   - Add KYC verification call before creating application
   - Replace hardcoded product logic with Vault config
   - Add versioning support
   - Add audit event publishing for all state changes

3. **`ComplianceService.cs`**
   - Replace hardcoded BoZ requirements with Vault config
   - Add real-time KYC API integration
   - Enhance EAR validation

4. **`WorkflowService.cs`**
   - Remove `[Obsolete]` methods
   - Add workflow state query methods
   - Add pause/resume methods for KYC revocation

5. **`LoanApplicationModels.cs`**
   - Add versioning fields to `LoanApplication`
   - Add `LoanNumber` property
   - Add `AgreementFileHash`, `AgreementMinioPath`

### New Files to Create

1. **Services**:
   - `VaultProductConfigService.cs`
   - `LoanVersioningService.cs`
   - `AgreementGenerationService.cs`
   - `ClientManagementClient.cs`
   - `AuditEventPublisher.cs`
   - `DualControlValidator.cs`
   - `BpmnDeploymentService.cs`

2. **Workers**:
   - `KycVerificationWorker.cs`
   - `CreditAssessmentWorker.cs`
   - `GenerateAgreementWorker.cs`
   - `DisbursementWorker.cs`

3. **Event Consumers**:
   - `ClientKycApprovedConsumer.cs`
   - `ClientKycRevokedConsumer.cs`
   - `CreditAssessmentCompletedConsumer.cs`

4. **Events** (folder):
   - `LoanApplicationCreated.cs`
   - `LoanApplicationSubmitted.cs`
   - `LoanCreditAssessmentCompleted.cs`
   - `LoanApprovalGranted.cs`
   - `LoanApprovalRejected.cs`
   - `LoanAgreementGenerated.cs`
   - `LoanDisbursed.cs`
   - `LoanVersionCreated.cs`
   - `LoanWorkflowStateChanged.cs`

5. **Workflows**:
   - `loan-origination-process.bpmn`

6. **Models**:
   - `LoanProductConfig.cs`
   - `EligibilityRules.cs`
   - `AgreementDocument.cs`

7. **Repositories**:
   - `LoanNumberSequenceRepository.cs`

---

## Testing Strategy

### Unit Tests Required

- `VaultProductConfigService` - Config fetch and caching
- `LoanVersioningService` - Version creation and loan number generation
- `DualControlValidator` - Self-approval detection
- `AuditEventPublisher` - Event serialization and correlation ID handling

### Integration Tests Required

- KYC gating workflow - Block application if not verified
- Approval routing - Correct user group assignment based on amount + risk grade
- Agreement generation - JasperReports API call, MinIO storage, hash calculation
- Dual control enforcement - Cannot approve own loan (API + workflow layers)
- Vault config refresh - Cache invalidation and reload

### End-to-End Workflow Tests

1. **Happy Path**: KYC verified → Application → Assessment → Approval → Agreement → Disbursement
2. **KYC Block**: Non-verified client blocked at start
3. **Self-Approval Block**: Same user cannot approve at any level
4. **Dual Control**: High-value loan requires two approvals
5. **KYC Revocation**: Active loan paused when KYC revoked

---

## Appendix - Configuration Examples

### Vault Product Configuration Example

```json
{
  "kv/intellifin/loan-products/GEPL-001/rules": {
    "productCode": "GEPL-001",
    "productName": "Government Employee Payroll Loan",
    "isActive": true,
    "minAmount": 1000,
    "maxAmount": 50000,
    "minTermMonths": 1,
    "maxTermMonths": 60,
    "allowedTerms": [1, 3, 6, 12, 18, 24, 36, 48, 60],
    "interestCalculationMethod": "ReducingBalance",
    "recurringCostComponents": {
      "nominalInterestRate": 0.12,
      "adminFee": 0.02,
      "managementFee": 0.01,
      "insuranceFee": 0.005
    },
    "calculatedEAR": 0.152,
    "earLimit": 0.48,
    "earCapCompliance": true,
    "oneTimeFees": {
      "applicationFee": 50,
      "crbFee": 25,
      "communicationFee": 10
    },
    "eligibilityRules": {
      "requiredKycStatus": "Approved",
      "kycExpirationMonths": 12,
      "minMonthlyIncome": 5000,
      "maxDtiRatio": 0.40,
      "pmecRegistrationRequired": true,
      "minEmploymentMonths": 6
    },
    "approvalAuthorityMatrix": [
      {
        "maxAmount": 50000,
        "allowedRiskGrades": ["A", "B"],
        "requiredApproverRole": "CreditAnalyst",
        "requiresDualControl": false
      },
      {
        "maxAmount": 250000,
        "allowedRiskGrades": ["A", "B", "C"],
        "requiredApproverRole": "HeadOfCredit",
        "requiresDualControl": false
      },
      {
        "maxAmount": null,
        "allowedRiskGrades": ["D", "F"],
        "requiredApproverRole": "HeadOfCredit,CEO",
        "requiresDualControl": true
      }
    ],
    "lastUpdated": "2025-10-17T10:00:00Z",
    "configVersion": "2.1",
    "updatedBy": "config-admin-001"
  }
}
```

### Camunda Task Assignment Configuration

```json
{
  "taskAssignments": {
    "LoanOfficerDataEntry": {
      "candidateGroups": ["loan-officers"],
      "description": "Complete application data entry"
    },
    "CreditAssessmentReview": {
      "candidateGroups": ["credit-analysts"],
      "description": "Review credit assessment results"
    },
    "CreditAnalystApproval": {
      "candidateGroups": ["credit-analysts"],
      "assigneeExclusion": "#{originalCreator}",
      "description": "Approve loan application (≤50K, A/B grade)"
    },
    "HeadOfCreditApproval": {
      "candidateGroups": ["head-of-credit"],
      "assigneeExclusion": "#{originalCreator}",
      "description": "Approve loan application (>50K or C grade)"
    },
    "CEOApproval": {
      "candidateGroups": ["ceo"],
      "assigneeExclusion": "#{originalCreator}",
      "description": "Second approval for dual control (>250K or D/F grade)"
    },
    "DisbursementAuthorization": {
      "candidateGroups": ["finance-officers"],
      "description": "Authorize loan disbursement"
    }
  }
}
```

---

## Success Criteria

### Functional Requirements

- ✅ KYC verification blocks non-verified clients at application start
- ✅ Product configurations loaded dynamically from Vault with EAR enforcement
- ✅ Dual control prevents self-approval at both workflow and service layers
- ✅ Loan versioning creates immutable history for all state changes
- ✅ Agreement generation produces PDF via JasperReports, stores in MinIO with SHA256 hash
- ✅ All state changes publish audit events to AdminService via RabbitMQ
- ✅ Camunda orchestrates entire workflow from Application → Active
- ✅ Approval routing automatically assigns based on amount + risk grade

### Non-Functional Requirements

- ✅ All API endpoints respond within 500ms (excluding external service calls)
- ✅ Vault configurations cached with 5-minute TTL to reduce latency
- ✅ Workflow can handle 100 concurrent loan applications without degradation
- ✅ Audit events delivered to AdminService with at-least-once guarantee
- ✅ Complete traceability via correlation IDs across all services

### Compliance Requirements

- ✅ EAR never exceeds 48% (enforced by Vault validation)
- ✅ Segregation of duties enforced (no self-approval)
- ✅ Complete audit trail for regulatory reporting
- ✅ Document integrity via cryptographic hashing
- ✅ KYC compliance gates prevent unauthorized lending

---

## Document Approval

- **Architect**: Winston - 2025-10-17
- **Technical Lead**: [Name] - [Date]
- **Product Owner**: [Name] - [Date]

---

**Document Control**: This brownfield architecture document reflects the CURRENT state as of 2025-10-17 and the specific transformation requirements for the Loan Origination module. Updates required after each implementation phase.
