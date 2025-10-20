# Credit Assessment Service - Brownfield Architecture Document

## Introduction

This document captures the CURRENT STATE of the Credit Assessment implementation within IntelliFin Loan Management System and defines the path forward to create a standalone, production-ready Credit Assessment microservice. It includes existing implementations, technical debt, integration points, and comprehensive requirements for the new service.

### Document Scope

**Enhancement Focus**: Transform embedded credit assessment functionality into a standalone microservice with:
- Vault-based rule engine configuration
- Automated and auditable scoring
- Camunda workflow integration
- Event-driven KYC status monitoring
- Client Management API integration
- Comprehensive audit trail via AdminService

### Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2025-10-17 | 1.0 | Initial brownfield analysis for Credit Assessment microservice | BMad Architect |

## Quick Reference - Key Files and Entry Points

### Existing Implementation (Loan Origination Service)

**Current Location**: Credit assessment logic currently embedded in `IntelliFin.LoanOriginationService`

**Critical Files**:
- **Service**: `apps/IntelliFin.LoanOriginationService/Services/CreditAssessmentService.cs`
- **Entity**: `libs/IntelliFin.Shared.DomainModels/Entities/CreditAssessment.cs`
- **Risk Engine**: `apps/IntelliFin.LoanOriginationService/Services/RiskCalculationEngine.cs`
- **DMN Rules**: `apps/IntelliFin.LoanOriginationService/BPMN/risk-grade-decision.dmn`
- **Models**: `apps/IntelliFin.LoanOriginationService/Models/LoanApplicationModels.cs`

**Existing Documentation**:
- `docs/domains/credit-assessment/credit-scoring-methodology.md` - Business rules and scoring framework
- `docs/domains/credit-assessment/risk-assessment-framework.md` - Risk management approach
- `docs/domains/credit-assessment/collateral-management.md` - SME collateral processes
- `docs/domains/loan-origination/credit-assessment-process.md` - Process workflow

### Enhancement Impact Areas

**New Microservice Structure** (to be created):
```
apps/IntelliFin.CreditAssessmentService/
├── Controllers/
│   └── CreditAssessmentController.cs       # New API endpoints
├── Services/
│   ├── CreditAssessmentService.cs          # Enhanced service (migrated)
│   ├── RiskScoringEngine.cs                # Vault-based rule engine
│   ├── AffordabilityAnalysisService.cs     # Affordability calculations
│   ├── CreditDecisionService.cs            # Decision orchestration
│   └── VaultConfigService.cs               # Vault integration
├── Integration/
│   ├── ClientManagementClient.cs           # Client Management API client
│   ├── TransUnionClient.cs                 # TransUnion integration
│   └── PMECClient.cs                       # PMEC verification
├── EventHandlers/
│   └── KYCStatusEventHandler.cs            # KYC expiry monitoring
├── Workers/
│   └── CreditAssessmentWorker.cs           # Camunda worker
├── Models/
│   ├── AssessmentRequest.cs
│   ├── AssessmentResponse.cs
│   └── RuleDtos.cs
└── BPMN/
    └── credit_assessment_v1.bpmn           # Camunda workflow
```

**Files to Modify**:
- `apps/IntelliFin.LoanOriginationService/Services/LoanApplicationService.cs` - Update to call new service
- `apps/IntelliFin.LoanOriginationService/BPMN/loan-approval-process.bpmn` - Add credit assessment service task
- `libs/IntelliFin.Shared.DomainModels/Entities/CreditAssessment.cs` - Enhance audit fields

## High Level Architecture

### Technical Summary

**Current State**: Credit assessment is tightly coupled within Loan Origination Service
- Logic embedded in `CreditAssessmentService.cs`
- Hard-coded rules in `RiskCalculationEngine.cs`
- DMN decision table in Camunda
- No external service interface
- Mock bureau integration
- No Vault integration

**Target State**: Standalone microservice with:
- Independent deployment
- Vault-based configuration
- Event-driven architecture
- Complete API interface
- Production TransUnion integration
- Comprehensive audit trail

### Actual Tech Stack

| Category | Technology | Version | Notes |
|----------|------------|---------|-------|
| Runtime | .NET | 8.0 | ASP.NET Core Web API |
| Framework | ASP.NET Core | 8.0 | Minimal APIs |
| Database | PostgreSQL | 15 | Shared LmsDbContext |
| Workflow | Camunda | 8.x | External task workers |
| Config | Vault | Latest | Rule engine configuration |
| Messaging | RabbitMQ | Latest | Event bus for KYC events |
| Cache | Redis | 7.x | Assessment result caching |
| Logging | Serilog | Latest | Structured logging |

### Repository Structure

- **Type**: Monorepo
- **Location**: `D:\Projects\Intellifin Loan Management System\apps\IntelliFin.CreditAssessmentService`
- **Shared Models**: `libs/IntelliFin.Shared.DomainModels/`
- **Package Manager**: NuGet

## Source Tree and Module Organization

### Existing Structure (Loan Origination Service)

```text
apps/IntelliFin.LoanOriginationService/
├── Services/
│   ├── CreditAssessmentService.cs          # Basic assessment logic
│   ├── RiskCalculationEngine.cs            # Hard-coded risk rules
│   ├── ComplianceService.cs                # BoZ compliance checks
│   └── ILoanApplicationService.cs
├── Models/
│   ├── LoanApplicationModels.cs            # Credit assessment DTOs
│   └── BusinessRuleModels.cs
├── BPMN/
│   ├── loan-approval-process.bpmn          # Main loan workflow
│   └── risk-grade-decision.dmn             # DMN decision table
└── Controllers/
    └── LoanApplicationController.cs
```

### New Microservice Structure

```text
apps/IntelliFin.CreditAssessmentService/
├── Controllers/
│   └── CreditAssessmentController.cs       # REST API endpoints
├── Services/
│   ├── Core/
│   │   ├── CreditAssessmentService.cs      # Main orchestration
│   │   ├── RiskScoringEngine.cs            # Vault-driven scoring
│   │   ├── AffordabilityAnalysisService.cs # DTI/affordability
│   │   └── CreditDecisionService.cs        # Decision logic
│   ├── Integration/
│   │   ├── ClientManagementClient.cs       # Get KYC/employment
│   │   ├── TransUnionClient.cs             # Credit bureau
│   │   ├── PMECClient.cs                   # Gov employee verification
│   │   └── AdminServiceClient.cs           # Audit trail
│   ├── Configuration/
│   │   ├── VaultConfigService.cs           # Load rules from Vault
│   │   └── RuleEngineService.cs            # Rule evaluation
│   └── Events/
│       └── KYCStatusEventHandler.cs        # KYC expiry handling
├── Workers/
│   └── CreditAssessmentWorker.cs           # Camunda external task
├── Models/
│   ├── Requests/
│   │   ├── AssessmentRequest.cs
│   │   └── ManualOverrideRequest.cs
│   ├── Responses/
│   │   ├── AssessmentResponse.cs
│   │   ├── DecisionPayload.cs
│   │   └── ScoreBreakdown.cs
│   └── Configuration/
│       ├── ScoringRules.cs
│       └── ThresholdConfig.cs
├── Data/
│   └── Repositories/
│       └── CreditAssessmentRepository.cs
├── BPMN/
│   └── credit_assessment_v1.bpmn           # Assessment workflow
└── Program.cs                              # Service startup
```

## Data Models and APIs

### Data Models

**Existing Entity**: `libs/IntelliFin.Shared.DomainModels/Entities/CreditAssessment.cs`
```csharp
public class CreditAssessment
{
    public Guid Id { get; set; }
    public Guid LoanApplicationId { get; set; }
    public string RiskGrade { get; set; }
    public decimal CreditScore { get; set; }
    public decimal DebtToIncomeRatio { get; set; }
    public decimal PaymentCapacity { get; set; }
    public bool HasCreditBureauData { get; set; }
    public string ScoreExplanation { get; set; }
    public DateTime AssessedAt { get; set; }
    public string AssessedBy { get; set; }
    public ICollection<CreditFactor> CreditFactors { get; set; }
    public ICollection<RiskIndicator> RiskIndicators { get; set; }
}
```

**Enhancements Needed**:
```csharp
// Add audit and decision fields
public Guid? AssessedByUserId { get; set; }           // User who triggered
public string DecisionCategory { get; set; }          // Approved/Conditional/ManualReview
public List<string> TriggeredRules { get; set; }      // Rule IDs that fired
public Guid? ManualOverrideByUserId { get; set; }     // If manually overridden
public string? ManualOverrideReason { get; set; }
public DateTime? ManualOverrideAt { get; set; }
public bool IsValid { get; set; }                     // False if KYC expired
public string? InvalidReason { get; set; }
public string VaultConfigVersion { get; set; }        // Track config version used
```

### New Models

**Credit Assessment Request**:
```csharp
public class CreditAssessmentRequest
{
    public Guid LoanApplicationId { get; set; }
    public Guid ClientId { get; set; }
    public Guid EmployerId { get; set; }              // For PMEC verification
    public decimal RequestedAmount { get; set; }
    public int TermMonths { get; set; }
    public string ProductType { get; set; }           // PAYROLL | BUSINESS
    public Dictionary<string, object> AdditionalData { get; set; }
}
```

**Decision Payload**:
```csharp
public class CreditDecisionPayload
{
    public Guid AssessmentId { get; set; }
    public string Decision { get; set; }              // Approved | Conditional | ManualReview | Rejected
    public string RiskGrade { get; set; }             // A, B, C, D, F
    public decimal CompositeScore { get; set; }
    public decimal DebtToIncomeRatio { get; set; }
    public decimal AffordableAmount { get; set; }
    public List<RuleFired> RulesFired { get; set; }
    public List<string> Conditions { get; set; }      // If conditional approval
    public string Explanation { get; set; }
    public DateTime AssessedAt { get; set; }
}
```

**Vault Rule Configuration**:
```csharp
public class ScoringRuleConfig
{
    public string RuleId { get; set; }
    public string RuleName { get; set; }
    public string Description { get; set; }
    public decimal Weight { get; set; }
    public string Condition { get; set; }             // Expression
    public string ProductType { get; set; }           // PAYROLL | BUSINESS | ALL
    public bool IsActive { get; set; }
}

public class ThresholdConfig
{
    public decimal MaxLoanToIncomeRatio { get; set; }
    public decimal MaxDebtToIncomeRatio { get; set; }
    public decimal MaxExposurePerClient { get; set; }
    public Dictionary<string, decimal> CreditScoreThresholds { get; set; }
}
```

### API Specifications

**New Credit Assessment API**:

```http
POST /api/v1/credit-assessment/assess
Authorization: Bearer {token}
Content-Type: application/json

Request Body:
{
  "loanApplicationId": "uuid",
  "clientId": "uuid",
  "employerId": "uuid",
  "requestedAmount": 50000,
  "termMonths": 24,
  "productType": "PAYROLL"
}

Response:
{
  "assessmentId": "uuid",
  "decision": "Approved",
  "riskGrade": "B",
  "compositeScore": 720,
  "debtToIncomeRatio": 0.35,
  "affordableAmount": 50000,
  "rulesFired": [
    {
      "ruleId": "R001",
      "ruleName": "Max LTI Ratio",
      "result": "Pass",
      "weight": 0.25
    }
  ],
  "explanation": "Application meets all criteria...",
  "assessedAt": "2025-10-17T13:30:00Z"
}
```

```http
GET /api/v1/credit-assessment/{assessmentId}
```

```http
POST /api/v1/credit-assessment/{assessmentId}/manual-override
```

```http
GET /api/v1/credit-assessment/client/{clientId}/latest
```

## Technical Debt and Known Issues

### Critical Technical Debt in Existing Implementation

1. **Embedded Service Architecture**
   - **Issue**: Credit assessment tightly coupled with Loan Origination Service
   - **Impact**: Cannot scale independently, deployment coupling, cannot reuse for other contexts
   - **Location**: `CreditAssessmentService.cs` in LoanOriginationService
   - **Fix**: Extract to standalone microservice

2. **Hard-Coded Rules**
   - **Issue**: Risk rules hard-coded in `RiskCalculationEngine.cs`
   - **Impact**: Code deployment required for business rule changes
   - **Location**: `apps/IntelliFin.LoanOriginationService/Services/RiskCalculationEngine.cs`
   - **Fix**: Move to Vault-based configuration

3. **Mock External Integrations**
   - **Issue**: TransUnion integration is mocked
   - **Impact**: Not production-ready
   - **Location**: `CreditAssessmentService.cs` lines 12-41 (mock bureau data dictionary)
   - **Fix**: Implement real TransUnion API integration

4. **No Event-Driven KYC Monitoring**
   - **Issue**: No subscription to KYC status changes
   - **Impact**: Assessments can become stale if KYC expires
   - **Fix**: Add RabbitMQ event handler for KYC status events

5. **Limited Audit Trail**
   - **Issue**: Basic logging, no structured audit events to AdminService
   - **Impact**: Compliance gaps, cannot trace decision history
   - **Fix**: Integrate with AdminService for comprehensive audit trail

6. **No Affordability Deep Analysis**
   - **Issue**: Basic DTI calculation, no PMEC payroll data integration
   - **Impact**: Inaccurate affordability assessment
   - **Location**: `CreditAssessmentService.cs` lines 131-173
   - **Fix**: Integrate with PMEC for real-time deduction data

7. **DMN Decision Table Limitations**
   - **Issue**: DMN table has fixed rules, not fully configurable
   - **Impact**: Limited flexibility for business rule changes
   - **Location**: `BPMN/risk-grade-decision.dmn`
   - **Fix**: Replace with Vault-driven rule engine

### Workarounds and Gotchas

- **Service Downtime Handling**: User mentioned "If the Credit Assessment service is down, the Camunda workflow automatically sends that loan to a manual Credit Officer Review task" - This needs to be implemented
- **KYC Expiry**: Currently no automated check if KYC expired between application and assessment
- **Configuration Changes**: Any rule change requires code deployment currently

## Integration Points and External Dependencies

### Required External Service Integrations

| Service | Purpose | Integration Type | Status | Priority |
|---------|---------|------------------|--------|----------|
| Client Management | Pull KYC data, employment info | REST API | To Implement | Critical |
| TransUnion Zambia | Credit bureau score retrieval | REST API | Mocked | Critical |
| PMEC | Government employee verification | REST API | Partial | Critical |
| Vault | Rule configuration | HashiCorp Vault | To Implement | Critical |
| AdminService | Audit trail logging | REST API / Event | To Implement | Critical |
| Loan Origination | Receive assessment requests | Async / API | Existing | Critical |
| Camunda | Workflow orchestration | External Task | To Enhance | Critical |
| RabbitMQ | KYC status events | Event Bus | To Implement | High |

### Integration Details

#### 1. Client Management Integration

**Purpose**: Retrieve verified KYC profile and employment data

**Endpoints Needed**:
```http
GET /api/v1/clients/{clientId}/kyc-profile
GET /api/v1/clients/{clientId}/employment
GET /api/v1/clients/{clientId}/verification-status
```

**Key Data**:
- KYC verification status and expiry
- Employer information
- Income verification documents
- Identification details

#### 2. TransUnion Integration

**Purpose**: Retrieve credit bureau score and history

**Status**: Currently mocked in `CreditAssessmentService.cs`

**Implementation Needed**:
- See `docs/integrations/transunion-zambia/transunion-api-specifications.md`
- Smart routing: Only query for first-time applicants
- Cache results to minimize costs
- Handle API failures gracefully

#### 3. PMEC Integration

**Purpose**: Verify government employee status and salary

**Endpoints**:
```http
GET /api/v1/pmec/employee/{nrc}/verify
GET /api/v1/pmec/employee/{nrc}/salary-details
GET /api/v1/pmec/employee/{nrc}/deductions
```

**Key Data**:
- Employment verification
- Salary amount and payment history
- Existing deductions
- Employment tenure

#### 4. Vault Integration

**Purpose**: Load scoring rules and thresholds

**Vault Paths**:
```
secret/intellifin/credit-assessment/rules
secret/intellifin/credit-assessment/thresholds
secret/intellifin/credit-assessment/weights
```

**Configuration Structure**:
```json
{
  "rules": [
    {
      "ruleId": "R001",
      "name": "Maximum Loan-to-Income Ratio",
      "weight": 0.25,
      "condition": "requestedAmount / monthlyIncome <= maxLTI",
      "productType": "ALL",
      "isActive": true
    }
  ],
  "thresholds": {
    "maxLoanToIncomeRatio": 10.0,
    "maxDebtToIncomeRatio": 0.40,
    "maxExposurePerClient": 500000,
    "creditScoreThresholds": {
      "A": 750,
      "B": 650,
      "C": 550,
      "D": 450,
      "F": 0
    }
  }
}
```

#### 5. AdminService Audit Integration

**Purpose**: Log all assessment decisions and rule evaluations

**Event Types**:
```
CreditAssessmentInitiated
RuleEvaluated
DecisionMade
ManualOverrideApplied
```

**Audit Payload**:
```csharp
{
    "EventType": "CreditAssessmentDecisionMade",
    "EntityType": "CreditAssessment",
    "EntityId": "assessment-guid",
    "UserId": "user-guid",
    "Action": "AutomatedDecision",
    "Details": {
        "LoanApplicationId": "app-guid",
        "ClientId": "client-guid",
        "Decision": "Approved",
        "RiskGrade": "B",
        "Score": 720,
        "RulesFired": [...],
        "VaultConfigVersion": "v1.2.3"
    },
    "Timestamp": "2025-10-17T13:30:00Z"
}
```

#### 6. Event Bus Integration (KYC Status)

**Purpose**: Subscribe to KYC status change events

**Events to Handle**:
- `KYCExpired`
- `KYCRevoked`
- `KYCUpdated`

**Handler Logic**:
```csharp
public async Task HandleKYCExpiredEvent(KYCExpiredEvent @event)
{
    // Find all active assessments for this client
    var activeAssessments = await _repository.GetActiveAssessmentsForClient(@event.ClientId);
    
    foreach (var assessment in activeAssessments)
    {
        assessment.IsValid = false;
        assessment.InvalidReason = "KYC verification expired";
        await _repository.UpdateAsync(assessment);
        
        // Log to audit trail
        await _auditService.LogEvent(new AuditEvent 
        {
            EventType = "CreditAssessmentInvalidated",
            EntityId = assessment.Id,
            Reason = "KYC Expired"
        });
    }
}
```

### Internal Integration Points

#### Loan Origination Service

**Current**:
- Direct method call: `_creditAssessmentService.PerformAssessmentAsync()`
- Synchronous coupling

**Target**:
- Option A: Async API call via HTTP
- Option B: Camunda service task with external worker
- Option C: Event-driven (publish `AssessmentRequested`, subscribe to `AssessmentCompleted`)

**Recommended**: Camunda service task approach for workflow visibility

#### Camunda Workflow Integration

**New Workflow**: `credit_assessment_v1.bpmn`

**Process Flow**:
```
[Start] 
  → [Service Task: Call Credit Assessment API]
  → [Exclusive Gateway: Check Service Response]
      ├─ [Service Available] 
      │   → [Receive Task: Wait for Assessment Result]
      │   → [Exclusive Gateway: Decision Category]
      │       ├─ [Approved] → [Continue Loan Process]
      │       ├─ [Conditional] → [Conditions Review Task]
      │       └─ [Manual Review] → [Credit Officer Task]
      └─ [Service Unavailable / Timeout]
          → [User Task: Manual Credit Officer Review]
```

**External Task Worker**:
```csharp
public class CreditAssessmentWorker : IExternalTaskWorker
{
    public async Task Execute(ExternalTask task, CancellationToken cancellationToken)
    {
        var loanApplicationId = task.Variables["loanApplicationId"];
        var clientId = task.Variables["clientId"];
        
        try
        {
            var result = await _creditAssessmentService.PerformAssessmentAsync(
                new CreditAssessmentRequest 
                {
                    LoanApplicationId = loanApplicationId,
                    ClientId = clientId,
                    // ... other fields
                });
            
            await _camundaClient.CompleteTask(task.Id, new {
                decision = result.Decision,
                riskGrade = result.RiskGrade,
                score = result.CompositeScore
            });
        }
        catch (Exception ex)
        {
            // Service unavailable - route to manual review
            await _camundaClient.HandleBpmnError(task.Id, "SERVICE_UNAVAILABLE");
        }
    }
}
```

## Existing Credit Assessment Logic

### Current Implementation Analysis

**Location**: `apps/IntelliFin.LoanOriginationService/Services/CreditAssessmentService.cs`

**Current Flow**:
1. Get loan application details (mocked)
2. Get credit bureau data (mocked from dictionary)
3. Assess affordability (basic DTI calculation)
4. Calculate risk via `RiskCalculationEngine`
5. Generate explanation
6. Return `CreditAssessment` entity

**What Works**:
- Basic structure and interfaces
- DTI calculation logic
- Credit factor and risk indicator models
- Explanation generation

**What's Missing**:
- Real external integrations
- Vault configuration
- Event handling
- Comprehensive audit trail
- Production-grade error handling
- Caching strategy
- Rule configurability

### Risk Calculation Engine

**Location**: `apps/IntelliFin.LoanOriginationService/Services/RiskCalculationEngine.cs`

**Current Approach**:
- Hard-coded risk factors
- Fixed weights
- Simple scoring algorithm
- Returns `RiskCalculationResult`

**Needs Enhancement**:
- Replace hard-coded rules with Vault configuration
- Add rule versioning
- Support multiple product types with different rules
- Add explainability for each rule evaluation
- Support A/B testing of rule sets

### DMN Decision Table

**Location**: `apps/IntelliFin.LoanOriginationService/BPMN/risk-grade-decision.dmn`

**Current Logic**:
- Inputs: Loan Amount, Credit Score, DTI Ratio, Product Type
- Output: Risk Grade (A, B, C, D, E, F)
- Hit Policy: FIRST

**Example Rules**:
- Grade A: Credit Score >= 750, DTI <= 30%, Loan <= 100K
- Grade B: Credit Score 650-749, DTI <= 40%, Loan <= 150K
- Grade C: Credit Score 550-649, DTI <= 50%, Loan <= 200K
- Default: Grade F

**Enhancement Path**:
- Keep DMN for simple grade determination
- Add Vault-driven rule engine for complex business rules
- Use DMN as final grade calculator after rule evaluation
- Support rule weights and composite scoring

## Vault-Based Rule Engine Design

### Configuration Structure

**Vault Secret Path**: `secret/intellifin/credit-assessment/config`

**Rules Configuration**:
```json
{
  "version": "1.0.0",
  "effectiveDate": "2025-10-17",
  "rules": {
    "payroll": [
      {
        "ruleId": "PR-001",
        "name": "Maximum Loan-to-Income Ratio",
        "description": "Loan amount should not exceed 10x monthly gross income",
        "weight": 0.25,
        "condition": {
          "type": "comparison",
          "expression": "requestedAmount / monthlyIncome",
          "operator": "<=",
          "threshold": 10.0
        },
        "isActive": true,
        "passScore": 100,
        "failScore": 0
      },
      {
        "ruleId": "PR-002",
        "name": "Debt-to-Income Ratio",
        "description": "Total debt payments should not exceed 40% of gross income",
        "weight": 0.30,
        "condition": {
          "type": "comparison",
          "expression": "(existingDebt + proposedPayment) / monthlyIncome",
          "operator": "<=",
          "threshold": 0.40
        },
        "isActive": true,
        "passScore": 100,
        "failScore": -150
      },
      {
        "ruleId": "PR-003",
        "name": "Credit Bureau Score Threshold",
        "description": "TransUnion credit score minimum threshold",
        "weight": 0.25,
        "condition": {
          "type": "comparison",
          "expression": "creditScore",
          "operator": ">=",
          "threshold": 550
        },
        "isActive": true,
        "passScore": 100,
        "failScore": -200
      },
      {
        "ruleId": "PR-004",
        "name": "Employment Tenure",
        "description": "Minimum government employment tenure",
        "weight": 0.10,
        "condition": {
          "type": "comparison",
          "expression": "employmentMonths",
          "operator": ">=",
          "threshold": 12
        },
        "isActive": true,
        "passScore": 50,
        "failScore": -50
      },
      {
        "ruleId": "PR-005",
        "name": "Maximum Client Exposure",
        "description": "Total exposure per client limit",
        "weight": 0.10,
        "condition": {
          "type": "comparison",
          "expression": "existingExposure + requestedAmount",
          "operator": "<=",
          "threshold": 500000
        },
        "isActive": true,
        "passScore": 50,
        "failScore": -100
      }
    ],
    "business": [
      {
        "ruleId": "BU-001",
        "name": "Debt Service Coverage Ratio",
        "description": "Business must have sufficient cash flow to service debt",
        "weight": 0.30,
        "condition": {
          "type": "comparison",
          "expression": "netOperatingIncome / totalDebtService",
          "operator": ">=",
          "threshold": 1.25
        },
        "isActive": true,
        "passScore": 120,
        "failScore": -180
      },
      {
        "ruleId": "BU-002",
        "name": "Loan-to-Value Ratio",
        "description": "Loan amount relative to collateral value",
        "weight": 0.35,
        "condition": {
          "type": "comparison",
          "expression": "requestedAmount / collateralValue",
          "operator": "<=",
          "threshold": 0.70
        },
        "isActive": true,
        "passScore": 150,
        "failScore": -200
      },
      {
        "ruleId": "BU-003",
        "name": "Business Age",
        "description": "Minimum time in business",
        "weight": 0.15,
        "condition": {
          "type": "comparison",
          "expression": "businessAgeMonths",
          "operator": ">=",
          "threshold": 24
        },
        "isActive": true,
        "passScore": 75,
        "failScore": -75
      }
    ]
  },
  "thresholds": {
    "maxLoanToIncomeRatio": 10.0,
    "maxDebtToIncomeRatio": 0.40,
    "maxClientExposure": 500000,
    "minCreditScore": {
      "payroll": 550,
      "business": 600
    },
    "minEmploymentMonths": 12,
    "minBusinessAgeMonths": 24,
    "maxLoanToValueRatio": 0.70,
    "minDebtServiceCoverageRatio": 1.25
  },
  "gradeThresholds": {
    "A": { "minScore": 750, "maxScore": 1000 },
    "B": { "minScore": 650, "maxScore": 749 },
    "C": { "minScore": 550, "maxScore": 649 },
    "D": { "minScore": 450, "maxScore": 549 },
    "F": { "minScore": 0, "maxScore": 449 }
  },
  "decisionMatrix": {
    "A": "Approved",
    "B": "Approved",
    "C": "ManualReview",
    "D": "ManualReview",
    "F": "Rejected"
  }
}
```

### Rule Engine Implementation

```csharp
public class VaultRuleEngine
{
    private readonly IVaultConfigService _vaultConfig;
    private readonly ILogger<VaultRuleEngine> _logger;
    
    public async Task<RuleEvaluationResult> EvaluateRulesAsync(
        CreditAssessmentRequest request,
        ClientData clientData,
        CreditBureauData bureauData)
    {
        var config = await _vaultConfig.GetRulesConfigAsync();
        var productType = request.ProductType.ToLower();
        var rules = config.Rules[productType];
        
        var result = new RuleEvaluationResult
        {
            CompositeScore = 0,
            RulesFired = new List<RuleEvaluation>()
        };
        
        foreach (var rule in rules.Where(r => r.IsActive))
        {
            var evaluation = await EvaluateRuleAsync(rule, request, clientData, bureauData);
            result.RulesFired.Add(evaluation);
            result.CompositeScore += evaluation.WeightedScore;
        }
        
        // Determine risk grade based on composite score
        result.RiskGrade = DetermineRiskGrade(result.CompositeScore, config.GradeThresholds);
        result.Decision = config.DecisionMatrix[result.RiskGrade];
        
        return result;
    }
    
    private async Task<RuleEvaluation> EvaluateRuleAsync(
        ScoringRule rule,
        CreditAssessmentRequest request,
        ClientData clientData,
        CreditBureauData bureauData)
    {
        var context = BuildEvaluationContext(request, clientData, bureauData);
        var passed = EvaluateCondition(rule.Condition, context);
        
        return new RuleEvaluation
        {
            RuleId = rule.RuleId,
            RuleName = rule.Name,
            Passed = passed,
            Score = passed ? rule.PassScore : rule.FailScore,
            Weight = rule.Weight,
            WeightedScore = (passed ? rule.PassScore : rule.FailScore) * rule.Weight,
            Explanation = GenerateExplanation(rule, passed, context)
        };
    }
}
```

## Development and Deployment

### Local Development Setup

1. **Clone and Navigate**:
   ```powershell
   cd "D:\Projects\Intellifin Loan Management System"
   ```

2. **Install Dependencies**:
   ```powershell
   dotnet restore
   ```

3. **Configure Vault** (for local dev):
   - Use Vault Dev Server or configure Vault connection in `appsettings.Development.json`
   - Load initial rule configuration

4. **Database Migration**:
   ```powershell
   cd libs/IntelliFin.Shared.DomainModels
   dotnet ef migrations add CreditAssessmentEnhancements --context LmsDbContext
   dotnet ef database update
   ```

5. **Start Dependencies**:
   - PostgreSQL
   - Redis
   - RabbitMQ
   - Camunda
   - Vault

6. **Run Service**:
   ```powershell
   cd apps/IntelliFin.CreditAssessmentService
   dotnet run
   ```

### Environment Configuration

**appsettings.json**:
```json
{
  "ConnectionStrings": {
    "LmsDatabase": "Host=localhost;Database=intellifin_lms;Username=postgres;Password=***"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "VirtualHost": "/",
    "Username": "guest",
    "Password": "***"
  },
  "Vault": {
    "Address": "http://localhost:8200",
    "Token": "***",
    "RulesPath": "secret/intellifin/credit-assessment/config"
  },
  "ExternalServices": {
    "ClientManagement": {
      "BaseUrl": "http://localhost:5001",
      "Timeout": 30
    },
    "AdminService": {
      "BaseUrl": "http://localhost:5002",
      "Timeout": 10
    },
    "TransUnion": {
      "BaseUrl": "https://api.transunion.co.zm",
      "ApiKey": "***",
      "Timeout": 60
    },
    "PMEC": {
      "BaseUrl": "https://pmec-api.gov.zm",
      "Timeout": 45
    }
  },
  "Camunda": {
    "ZeebeGatewayAddress": "localhost:26500",
    "WorkerName": "credit-assessment-worker"
  }
}
```

### Build and Deployment Process

**Build**:
```powershell
dotnet build -c Release
```

**Test**:
```powershell
dotnet test
```

**Docker Build**:
```powershell
docker build -t intellifin-credit-assessment:latest -f apps/IntelliFin.CreditAssessmentService/Dockerfile .
```

**Deploy**:
- Kubernetes manifests in `deploy/k8s/credit-assessment-service.yaml`
- Helm chart for configuration management

## Testing Strategy

### Unit Tests

**Coverage Target**: 80%

**Key Test Areas**:
- Rule evaluation logic
- DTI calculations
- Affordability analysis
- Decision matrix logic
- Vault configuration parsing

**Example**:
```csharp
[Fact]
public async Task RiskScoringEngine_WhenDTIExceedsThreshold_ShouldFailRule()
{
    // Arrange
    var engine = new VaultRuleEngine(_mockVaultConfig.Object, _logger);
    var request = new CreditAssessmentRequest { /* ... */ };
    var clientData = new ClientData { MonthlyIncome = 10000, ExistingDebt = 5000 };
    
    // Act
    var result = await engine.EvaluateRulesAsync(request, clientData, null);
    
    // Assert
    var dtiRule = result.RulesFired.First(r => r.RuleId == "PR-002");
    Assert.False(dtiRule.Passed);
    Assert.Equal("ManualReview", result.Decision);
}
```

### Integration Tests

**Test Scenarios**:
1. End-to-end assessment flow with mocked external services
2. Vault configuration loading
3. Event bus message handling
4. Camunda worker task execution
5. Database persistence

### Performance Tests

**Targets**:
- Assessment completion: < 5 seconds (95th percentile)
- Vault config refresh: < 1 second
- Concurrent assessments: 100/second sustained

## Security Considerations

### Authentication & Authorization

- JWT bearer token authentication
- Permission required: `credit:assess`
- Manual override: `credit:override`

### Data Protection

- PII encryption at rest
- TLS for all external communication
- Credit scores cached with TTL and encryption
- Audit log immutability

### Vault Access

- AppRole authentication for service
- Separate policies for read vs write
- Secret rotation support

## Monitoring and Observability

### Metrics

- Assessment request rate
- Assessment completion time (p50, p95, p99)
- Rule evaluation duration per rule
- External API latency (Client Mgmt, TransUnion, PMEC)
- Cache hit rate
- Assessment decisions by category
- Manual override rate

### Logging

- Structured logging with Serilog
- Log levels: Debug, Info, Warning, Error, Critical
- Correlation IDs for request tracing
- Sensitive data masking

### Alerts

- Service availability < 99%
- Assessment completion time > 10 seconds
- External API failure rate > 5%
- Manual override rate > 20%
- KYC expiry events not processed

## Migration Strategy

### Phase 1: Service Extraction

1. Create new `IntelliFin.CreditAssessmentService` project
2. Copy existing credit assessment code
3. Add basic API endpoints
4. Deploy alongside Loan Origination Service

### Phase 2: Integration Setup

1. Implement Client Management client
2. Implement TransUnion client (production)
3. Add PMEC integration
4. Set up RabbitMQ event handlers

### Phase 3: Vault Integration

1. Design rule schema
2. Implement Vault config service
3. Build rule engine
4. Migrate hard-coded rules to Vault
5. Add rule versioning

### Phase 4: Audit & Compliance

1. Integrate with AdminService
2. Add comprehensive event logging
3. Implement manual override workflow
4. Add explainability features

### Phase 5: Camunda Workflow

1. Create `credit_assessment_v1.bpmn`
2. Implement external task worker
3. Add error handling (service unavailable)
4. Update Loan Origination workflow

### Phase 6: Cutover

1. Feature flag in Loan Origination to use new service
2. Parallel run (old + new) for validation
3. Gradual traffic shift
4. Decommission old embedded logic

## Success Criteria

- [ ] Standalone microservice deployed independently
- [ ] Vault-based rule configuration operational
- [ ] All external integrations production-ready
- [ ] KYC status event handling active
- [ ] Comprehensive audit trail to AdminService
- [ ] Camunda workflow with fallback to manual review
- [ ] < 5 second assessment completion time
- [ ] 99.9% availability
- [ ] 100% decision auditability
- [ ] Zero rule deployment downtime

## Appendix A - Risk Grade Decision Rules

See existing DMN file: `apps/IntelliFin.LoanOriginationService/BPMN/risk-grade-decision.dmn`

**Current DMN Logic**:
- A: Credit Score >= 750, DTI <= 30%
- B: Credit Score 650-749, DTI <= 40%
- C: Credit Score 550-649, DTI <= 50%
- D: Credit Score 450-549, DTI <= 40%
- F: Credit Score < 350 OR DTI > 60%

**Enhancement**: Move to composite scoring with Vault-driven weights

## Appendix B - Existing Documentation References

- Credit Scoring Methodology: `docs/domains/credit-assessment/credit-scoring-methodology.md`
- Risk Assessment Framework: `docs/domains/credit-assessment/risk-assessment-framework.md`
- Collateral Management: `docs/domains/credit-assessment/collateral-management.md`
- TransUnion Integration: `docs/integrations/transunion-zambia/transunion-api-specifications.md`
- Loan Origination Process: `docs/domains/loan-origination/credit-assessment-process.md`

## Appendix C - Database Schema Changes

**New Tables**:
```sql
-- Assessment audit log
CREATE TABLE credit_assessment_audit (
    id UUID PRIMARY KEY,
    assessment_id UUID NOT NULL REFERENCES credit_assessments(id),
    event_type VARCHAR(100) NOT NULL,
    event_data JSONB NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by VARCHAR(255)
);

-- Rule evaluations
CREATE TABLE rule_evaluations (
    id UUID PRIMARY KEY,
    assessment_id UUID NOT NULL REFERENCES credit_assessments(id),
    rule_id VARCHAR(50) NOT NULL,
    rule_name VARCHAR(255) NOT NULL,
    passed BOOLEAN NOT NULL,
    score DECIMAL(10,2) NOT NULL,
    weight DECIMAL(5,4) NOT NULL,
    weighted_score DECIMAL(10,2) NOT NULL,
    explanation TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Configuration version tracking
CREATE TABLE assessment_config_versions (
    id UUID PRIMARY KEY,
    version VARCHAR(50) NOT NULL,
    config_data JSONB NOT NULL,
    effective_from TIMESTAMP NOT NULL,
    effective_to TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);
```

**Existing Table Enhancements**:
```sql
ALTER TABLE credit_assessments
ADD COLUMN assessed_by_user_id UUID,
ADD COLUMN decision_category VARCHAR(50),
ADD COLUMN triggered_rules JSONB,
ADD COLUMN manual_override_by_user_id UUID,
ADD COLUMN manual_override_reason TEXT,
ADD COLUMN manual_override_at TIMESTAMP,
ADD COLUMN is_valid BOOLEAN DEFAULT TRUE,
ADD COLUMN invalid_reason VARCHAR(500),
ADD COLUMN vault_config_version VARCHAR(50);
```

---

**Document Version**: 1.0  
**Date**: 2025-10-17  
**Author**: BMad Architect  
**Status**: Ready for PM Review
