# BPMN Workflow Guide - Loan Origination Process

## Overview

This document describes the Camunda BPMN workflow that orchestrates the loan origination process from application submission through to disbursement.

**Story**: 1.5 - Camunda BPMN Workflow Design and Deployment  
**Version**: 1.0  
**Last Updated**: 2025-10-27

## Workflow Structure

### Process ID
`loanOriginationProcess`

### Process Name
Loan Origination Process

### Workflow Sequence

1. **Start Event** - Loan Application Initiated
2. **Service Task** - KYC Gate (verify-kyc)
3. **User Task** - Application Review (loan-officers)
4. **Service Task** - Credit Assessment (request-credit-assessment)
5. **Exclusive Gateway** - Approval Routing
   - Route 1: Credit Analyst Approval (credit-analysts)
   - Route 2: Head of Credit Approval (head-of-credit)
   - Route 3: Dual Control (head-of-credit → ceo)
6. **Merge Gateway** - Approval Complete
7. **Service Task** - Generate Agreement (generate-agreement)
8. **User Task** - Disbursement Authorization (finance-officers)
9. **Service Task** - Execute Disbursement (execute-disbursement)
10. **End Event** - Loan Active

## Approval Routing Logic

The exclusive gateway routes loan applications based on **loan amount** and **risk grade**.

### Route 1: Credit Analyst (Low Risk & Low Amount)

**Condition**: `loanAmount <= 50000 and riskGrade in ["A", "B"]`

- **Target**: Single approval from Credit Analyst
- **Candidate Group**: `credit-analysts`
- **Use Case**: Low-risk, low-amount loans that can be handled by junior credit staff

### Route 2: Head of Credit (Medium Risk or Amount)

**Condition**: `(loanAmount > 50000 and loanAmount <= 250000) or riskGrade = "C"`

- **Target**: Single approval from Head of Credit
- **Candidate Group**: `head-of-credit`
- **Use Case**: Medium-risk loans or larger amounts requiring senior credit officer review

### Route 3: Dual Control (High Risk or High Amount)

**Condition**: `loanAmount > 250000 or riskGrade in ["D", "F"]`

- **Target**: Sequential dual approval
  1. Head of Credit approval
  2. CEO approval
- **Candidate Groups**: `head-of-credit`, `ceo`
- **Use Case**: High-risk or high-value loans requiring segregation of duties

## Service Tasks

Service tasks are executed by Zeebe workers that poll for jobs with specific types.

### 1. KYC Gate

- **Task ID**: `Activity_KycGate`
- **Job Type**: `verify-kyc`
- **Purpose**: Verify client KYC status before proceeding
- **Worker**: KycVerificationWorker (Story 1.6)
- **Error Handling**: Throws BPMN error `KYC_NOT_VERIFIED` if verification fails

### 2. Credit Assessment

- **Task ID**: `Activity_RequestCreditAssessment`
- **Job Type**: `request-credit-assessment`
- **Purpose**: Trigger external Credit Assessment Service for risk scoring
- **Worker**: CreditAssessmentWorker (Story 1.6)
- **Fallback**: Manual credit review task if service unavailable

### 3. Generate Agreement

- **Task ID**: `Activity_GenerateAgreement`
- **Job Type**: `generate-agreement`
- **Purpose**: Generate loan agreement PDF via JasperReports, store in MinIO
- **Worker**: GenerateAgreementWorker (Story 1.8)
- **Timeout**: 10 seconds

### 4. Execute Disbursement

- **Task ID**: `Activity_ExecuteDisbursement`
- **Job Type**: `execute-disbursement`
- **Purpose**: Execute payment via Tingg/PMEC integration
- **Worker**: DisbursementWorker (Future story)

## User Tasks

User tasks are completed via the Camunda Tasklist UI or API by human actors.

### 1. Application Review

- **Task ID**: `Activity_ApplicationReview`
- **Candidate Groups**: `loan-officers`
- **Purpose**: Human validation of auto-populated client data before submission
- **Form**: Camunda Form with read-only fields for system-calculated values (EAR, DTI, monthly payment)

### 2. Credit Analyst Approval

- **Task ID**: `Activity_CreditAnalystApproval`
- **Candidate Groups**: `credit-analysts`
- **Form**: loan-approval-form.json (approve/reject with reason)
- **Note**: Cannot be assigned to loan creator (enforced in Story 1.7)

### 3. Head of Credit Approval

- **Task ID**: `Activity_HeadOfCreditApproval`
- **Candidate Groups**: `head-of-credit`
- **Form**: loan-approval-form.json

### 4. Head of Credit Approval (Dual Control - First)

- **Task ID**: `Activity_HeadOfCreditApproval_DualControl`
- **Candidate Groups**: `head-of-credit`
- **Form**: loan-approval-form.json
- **Note**: First approval in dual control sequence

### 5. CEO Approval (Dual Control - Second)

- **Task ID**: `Activity_CeoApproval`
- **Candidate Groups**: `ceo`
- **Form**: loan-approval-form.json
- **Note**: Second approval in dual control sequence

### 6. Disbursement Authorization

- **Task ID**: `Activity_DisbursementAuthorization`
- **Candidate Groups**: `finance-officers`
- **Purpose**: Final authorization before payment execution

## Process Variables

When starting a workflow instance, the following variables must be provided:

```json
{
  "applicationId": "guid",
  "clientId": "guid",
  "loanAmount": 50000.00,
  "riskGrade": "B",
  "productCode": "GEPL-001",
  "termMonths": 24,
  "createdBy": "userId",
  "loanNumber": "LUS-2025-00123"
}
```

### Variable Descriptions

- **applicationId**: Unique identifier for the loan application (GUID)
- **clientId**: Unique identifier for the client (GUID)
- **loanAmount**: Requested loan amount (decimal)
- **riskGrade**: Risk grade assigned to the application (A, B, C, D, F)
- **productCode**: Loan product code (e.g., GEPL-001)
- **termMonths**: Loan term in months (integer)
- **createdBy**: User ID who created the application (string)
- **loanNumber**: Generated loan number (e.g., LUS-2025-00123)

## Starting a Workflow

### Via WorkflowService

```csharp
var workflowInstanceKey = await _workflowService.StartLoanOriginationWorkflowAsync(
    applicationId: application.Id,
    clientId: application.ClientId,
    loanAmount: application.LoanAmount,
    riskGrade: creditAssessment.RiskGrade.ToString(),
    productCode: application.ProductCode,
    termMonths: application.TermMonths,
    createdBy: "user-id",
    loanNumber: application.LoanNumber,
    cancellationToken: cancellationToken
);

// Store workflow instance key in application
application.WorkflowInstanceId = workflowInstanceKey;
await _repository.UpdateAsync(application);
```

### Feature Flag Control

The `EnableWorkflowOrchestration` feature flag controls whether workflows are started:

```json
{
  "FeatureFlags": {
    "EnableWorkflowOrchestration": false
  }
}
```

- **false** (default): Existing CRUD logic is used for backward compatibility
- **true**: Loan applications start Camunda workflow instances

**Important**: Set to `true` only after BPMN is deployed and tested.

## Deployment

### Automatic Deployment

The `BpmnDeploymentService` automatically deploys the BPMN workflow on application startup:

1. Reads `Workflows/loan-origination-process.bpmn` from application directory
2. Deploys to Zeebe using the configured Zeebe client
3. Logs deployment success with ProcessKey and Version
4. **Fails fast** if deployment fails (application will not start)

### Manual Deployment (via Camunda Modeler)

If you need to manually deploy or update the workflow:

1. Open `loan-origination-process.bpmn` in Camunda Modeler
2. Click **Deploy** button in the toolbar
3. Configure deployment to your Zeebe cluster
4. Click **Deploy** to push the updated workflow

### Verification

After deployment, verify in Camunda Operate:

1. Navigate to Camunda Operate UI
2. Go to **Processes** → **loanOriginationProcess**
3. Verify the process definition is deployed
4. Check the version number

## BPMN File Location

**File Path**: `apps/IntelliFin.LoanOriginationService/Workflows/loan-origination-process.bpmn`

The BPMN file is automatically copied to the output directory during build (configured in `.csproj`).

## Camunda Modeler Usage

### Opening the BPMN File

1. Download and install Camunda Modeler 5.x+ from https://camunda.com/download/modeler/
2. Open Camunda Modeler
3. File → Open → Navigate to `loan-origination-process.bpmn`

### Editing the Workflow

1. Drag and drop tasks from the left palette
2. Configure properties in the right panel:
   - For **Service Tasks**: Set `zeebe:taskDefinition` type
   - For **User Tasks**: Set `zeebe:assignmentDefinition` candidateGroups
   - For **Gateways**: Set FEEL expressions in sequence flows
3. Save the file

### Validation

1. Open the BPMN file in Camunda Modeler
2. Check for red error icons indicating validation issues
3. Hover over icons to see error details
4. Fix all errors before deploying

### Common FEEL Expressions

```feel
// Less than or equal
loanAmount <= 50000

// AND condition
loanAmount <= 50000 and riskGrade in ["A", "B"]

// OR condition
loanAmount > 250000 or riskGrade in ["D", "F"]

// Range check
loanAmount > 50000 and loanAmount <= 250000
```

## Monitoring and Operations

### View Active Instances

1. Navigate to Camunda Operate
2. Go to **Instances** tab
3. Filter by process: `loanOriginationProcess`
4. View running instances, their current tasks, and variables

### View User Tasks

1. Navigate to Camunda Tasklist
2. Tasks are organized by candidate groups
3. Claim and complete tasks assigned to your groups

### View History

1. Navigate to Camunda Operate
2. Select a completed process instance
3. View the flow history and variable changes

## Troubleshooting

### BPMN Deployment Fails on Startup

**Symptoms**: Application fails to start with "Failed to deploy BPMN workflow" error

**Solutions**:
1. Check that `Workflows/loan-origination-process.bpmn` exists in output directory
2. Verify BPMN file is valid XML (open in Camunda Modeler and check for errors)
3. Verify Zeebe client configuration in `appsettings.json`
4. Check Zeebe cluster is accessible

### Workflow Instance Not Starting

**Symptoms**: `StartLoanOriginationWorkflowAsync` throws exception

**Solutions**:
1. Verify Zeebe client is configured and connected
2. Check BPMN process ID matches: `loanOriginationProcess`
3. Verify all required variables are provided
4. Check Zeebe gateway logs for errors

### Gateway Not Routing Correctly

**Symptoms**: Workflow takes wrong approval path

**Solutions**:
1. Verify FEEL expressions are correctly formatted
2. Check that `loanAmount` and `riskGrade` variables are set correctly
3. Test conditions in Camunda Modeler's "FEEL Expression Editor"
4. Add default sequence flow to catch unhandled conditions

### Service Tasks Not Executing

**Symptoms**: Workflow waits at service task indefinitely

**Solutions**:
1. Verify corresponding worker is registered and running
2. Check worker is polling for correct job type
3. Check worker error logs
4. View job failures in Camunda Operate → **Incidents**

## Related Documentation

- **Story 1.5**: BPMN Workflow Design and Deployment
- **Story 1.6**: Zeebe Workers Implementation
- **Story 1.7**: Dual Control Enforcement
- **Story 1.8**: Document Generation Integration

## References

- [Camunda 8 Documentation](https://docs.camunda.io/docs/components/modeler/)
- [BPMN 2.0 Specification](https://www.omg.org/spec/BPMN/2.0/)
- [Zeebe FEEL Expressions](https://docs.camunda.io/docs/components/modeler/feel/what-is-feel/)
