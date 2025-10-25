# Camunda BPMN Workflows

## Credit Assessment Workflow v1

**File**: `credit_assessment_v1.bpmn`  
**Story**: 1.15 - Camunda Workflow Definition

### Process Overview

This workflow orchestrates the credit assessment process with automatic fallback to manual review when needed.

### Workflow Steps

1. **Start Event**: Assessment Requested
   - Triggered when a loan application needs credit assessment
   - Input variables: `loanApplicationId`, `clientId`, `requestedAmount`, `termMonths`, `productType`

2. **Service Task**: Perform Credit Assessment
   - External task type: `credit-assessment`
   - Timeout: 30 seconds
   - Calls Credit Assessment Service via external task worker
   - Returns: `decision`, `riskGrade`, `score`, `assessmentId`

3. **Exclusive Gateway**: Check Decision
   - Routes based on assessment decision
   - Three paths: Approved, Manual Review, Rejected

4. **Approved Path**: 
   - Direct to approval process
   - End event

5. **Manual Review Path**:
   - User task for credit officer review
   - Decision: Approved/Conditional/ManualReview
   - Allows human judgment on edge cases

6. **Rejected Path**:
   - Automatic rejection
   - End event

### Process Variables

**Input**:
- `loanApplicationId` (String) - Loan application UUID
- `clientId` (String) - Client UUID
- `requestedAmount` (Number) - Loan amount requested
- `termMonths` (Number) - Loan term
- `productType` (String) - PAYROLL or BUSINESS

**Output from Assessment**:
- `decision` (String) - Approved/ManualReview/Rejected
- `riskGrade` (String) - A, B, C, D, or F
- `score` (Number) - Composite risk score
- `assessmentId` (String) - Assessment record UUID
- `explanation` (String) - Human-readable explanation

### Error Handling

**Service Unavailable**:
- If assessment service times out or fails
- BPMN error event: `SERVICE_UNAVAILABLE`
- Routes to manual credit officer review task

**Boundary Events**:
- Timeout: 30 seconds
- Error: SERVICE_UNAVAILABLE → Manual Review

### Deployment

```bash
# Deploy to Camunda
zbctl deploy credit_assessment_v1.bpmn

# Start instance
zbctl create instance credit_assessment_v1 \
  --variables '{"loanApplicationId":"uuid","clientId":"uuid","requestedAmount":50000}'
```

### Integration with Worker

The external task worker (`CreditAssessmentWorker`) polls for tasks of type `credit-assessment` and:
1. Extracts process variables
2. Calls `ICreditAssessmentService.PerformAssessmentAsync()`
3. Completes task with assessment results
4. Or throws BPMN error if service unavailable

---

**Created**: Story 1.15 - Camunda Workflow Definition  
**Status**: Ready for deployment to Camunda Zeebe
