# EDD Human Task Forms

This directory contains Camunda Form JSON schemas for the EDD (Enhanced Due Diligence) workflow human tasks.

## Forms

### 1. `compliance-officer-edd-review-form.json`

**Purpose:** First-level EDD review by Compliance Officer

**User Task ID:** `UserTask_ComplianceReview` in `client_edd_v1.bpmn`

**Form Key:** `compliance-officer-edd-review`

**Assignee:** `role:compliance-officer`

**Sections:**
- Client information (read-only)
- EDD report download
- AML screening results (read-only)
- Document verification status (read-only)
- Compliance decision (editable)

**Variables:**
- **Inputs:**
  - `clientName` - Client full name
  - `eddReason` - EDD escalation reason
  - `overallRiskLevel` - Overall risk level
  - `reportObjectKey` - MinIO object key for report
  - `sanctionsHit` - Boolean sanctions match
  - `pepMatch` - Boolean PEP match
  - `documentComplete` - Boolean document completeness

- **Outputs:**
  - `complianceApproved` - Boolean decision
  - `complianceRecommendation` - String (Approve/Reject/RequestInfo)
  - `complianceComments` - String (required, 10-2000 chars)
  - `rejectionReason` - String (if rejecting)

**Validation:**
- `complianceComments` is required (minimum 10 characters)
- `rejectionReason` is required if `complianceApproved = false`

---

### 2. `ceo-edd-approval-form.json`

**Purpose:** Final approval by CEO after compliance review

**User Task ID:** `UserTask_CeoApproval` in `client_edd_v1.bpmn`

**Form Key:** `ceo-edd-approval`

**Assignee:** `role:ceo`

**Sections:**
- Client information (read-only)
- Compliance officer recommendation (read-only)
- Compliance review notes (read-only)
- EDD report download
- AML screening results (read-only)
- CEO final decision (editable)

**Variables:**
- **Inputs:**
  - All from compliance form (read-only)
  - `complianceRecommendation` - Compliance recommendation
  - `complianceComments` - Compliance notes

- **Outputs:**
  - `ceoApproved` - Boolean final decision
  - `riskAcceptanceLevel` - String (Standard/EnhancedMonitoring/RestrictedServices)
  - `ceoComments` - String (required, 20-2000 chars)
  - `rejectionReason` - String (if rejecting)

**Validation:**
- `ceoComments` is required (minimum 20 characters)
- `riskAcceptanceLevel` is required if `ceoApproved = true`
- `rejectionReason` is required if `ceoApproved = false`

**Risk Acceptance Levels:**
- **Standard:** Normal monitoring procedures
- **Enhanced Monitoring:** Increased transaction monitoring, lower thresholds
- **Restricted Services:** Limited product offerings, manual approvals

---

## Deployment

These forms are referenced in the BPMN workflow via the `formKey` attribute:

```xml
<bpmn:userTask id="UserTask_ComplianceReview" name="Compliance Officer Review">
  <bpmn:extensionElements>
    <zeebe:assignmentDefinition assignee="role:compliance-officer" />
    <zeebe:formDefinition formKey="compliance-officer-edd-review" />
  </bpmn:extensionElements>
</bpmn:userTask>
```

### Deployment to Camunda

1. **Via Camunda Modeler:**
   - Open Camunda Modeler
   - Go to Forms tab
   - Import JSON file
   - Deploy to Camunda cluster

2. **Via Camunda Tasklist:**
   - Forms are automatically loaded when referenced in BPMN
   - Form key must match JSON `id` field

3. **Via API:**
   ```bash
   curl -X POST \
     http://camunda:8080/v1/forms \
     -H 'Content-Type: application/json' \
     -d @compliance-officer-edd-review-form.json
   ```

---

## Development & Testing

### Local Testing (Without Camunda UI)

For integration testing without Camunda Tasklist UI:

```csharp
// Simulate compliance approval
var complianceDecision = new Dictionary<string, object>
{
    ["complianceApproved"] = true,
    ["complianceRecommendation"] = "Approve",
    ["complianceComments"] = "Reviewed and approved with conditions"
};

// Simulate CEO approval
var ceoDecision = new Dictionary<string, object>
{
    ["ceoApproved"] = true,
    ["riskAcceptanceLevel"] = "EnhancedMonitoring",
    ["ceoComments"] = "Approved subject to enhanced monitoring requirements"
};
```

### Form Validation

The forms include JSON Schema validation:
- Required fields are enforced
- Minimum/maximum length constraints
- Conditional field visibility (e.g., rejection reason only shown if rejecting)
- Dropdown options are restricted to valid values

---

## Workflow Integration

### Workflow Variables Flow

```
client_kyc_v1.bpmn (Main KYC Workflow)
  ↓
  Sets: clientId, kycStatusId, escalationReason
  ↓
client_edd_v1.bpmn (EDD Workflow)
  ↓
  Generate Report Worker
    → Sets: reportObjectKey, overallRiskLevel, clientName, eddReason
  ↓
  Compliance Review (Human Task)
    → User provides: complianceApproved, complianceComments
  ↓
  CEO Approval (Human Task)
    → User provides: ceoApproved, riskAcceptanceLevel, ceoComments
  ↓
  Status Update Worker
    → Uses all above variables to update KycStatus
```

---

## Audit & Compliance

All form submissions are logged via:
1. **Camunda Audit Log:** Built-in task completion events
2. **Application Audit Log:** `AdminService.LogEventAsync` calls
3. **Database Records:** `KycStatus.ComplianceComments`, `KycStatus.CeoComments`

Audit fields captured:
- User ID (from Camunda claims)
- Timestamp (UTC)
- Decision (approve/reject)
- Comments/rationale
- Risk acceptance level
- Correlation ID

---

## Security Considerations

### Role-Based Access Control

- **Compliance Officer:** Can only access compliance review tasks
- **CEO:** Can only access CEO approval tasks
- **Separation of Duties:** Different users must handle compliance vs CEO tasks

### Data Protection

- Reports contain sensitive PII and are access-controlled via MinIO pre-signed URLs
- Form data is encrypted in transit (TLS)
- Sensitive fields (e.g., rejection reasons) are logged to secure audit system

### MFA Requirements

High-risk EDD approvals may require step-up authentication:
- CEO approvals for sanctions hits
- Risk acceptance level "RestrictedServices"
- Override of compliance recommendation

---

## Troubleshooting

### Form Not Displayed in Tasklist

1. Verify `formKey` in BPMN matches JSON `id`
2. Check form is deployed to same Camunda cluster
3. Verify user has access to task (role assignment)

### Validation Errors

1. Check required fields are populated
2. Verify field lengths meet min/max constraints
3. Ensure conditional fields have correct visibility rules

### Variables Not Passing Between Tasks

1. Verify variable names match exactly (case-sensitive)
2. Check BPMN output mappings
3. Review Camunda Operate for variable values

---

## Future Enhancements

**Planned for Story 1.14:**
- Real-time notifications when tasks assigned
- Email alerts for pending EDD reviews
- Auto-escalation if task not completed within SLA
- Attachment support for additional evidence
- Inline document preview (instead of download)
- Multi-language support (English, Nyanja, Bemba)

**Planned for Story 1.15:**
- Mobile-optimized forms for tablet access
- Offline capability with sync
- Digital signature capture
- Voice notes/comments
- AI-assisted risk assessment recommendations
