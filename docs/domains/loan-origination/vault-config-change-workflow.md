# Vault Product Configuration Change Workflow

**Version**: 1.0  
**Last Updated**: 2025-10-27  
**Owner**: Product Management & Compliance Team

## Overview

This document defines the dual-control approval workflow for updating loan product configurations in HashiCorp Vault. All changes require approval from both Product Owner and Compliance Officer to ensure regulatory compliance with Bank of Zambia Money Lenders Act.

## Approval Workflow

```
┌─────────────────┐
│ Product Owner   │
│ Proposes Change │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ EAR Calculation │
│ & Validation    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Compliance      │
│ Officer Review  │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
 Approve   Reject
    │         │
    ▼         ▼
┌────────┐ ┌──────┐
│ Apply  │ │ End  │
│ Change │ └──────┘
└───┬────┘
    │
    ▼
┌─────────────────┐
│ Vault Audit Log │
│ Version History │
└─────────────────┘
```

## Roles and Responsibilities

### Product Owner
- **Responsibilities**:
  - Propose product configuration changes
  - Provide business justification
  - Calculate EAR using approved methodology
  - Coordinate with Compliance Officer
- **Required Skills**: Product knowledge, financial calculations
- **Access Level**: Read-only Vault access

### Compliance Officer
- **Responsibilities**:
  - Review EAR calculations for accuracy
  - Verify compliance with 48% EAR cap
  - Approve or reject changes
  - Document approval decision
- **Required Skills**: Regulatory compliance, financial analysis
- **Access Level**: Read-only Vault access

### System Administrator
- **Responsibilities**:
  - Apply approved changes to Vault
  - Verify configuration propagation
  - Monitor for errors
  - Maintain audit trail
- **Required Skills**: Vault administration, technical operations
- **Access Level**: Write access to `kv/intellifin/loan-products/*`

## Change Request Process

### Step 1: Initiate Change Request

**Actor**: Product Owner

**Actions**:
1. Create Change Request ticket in tracking system (Jira/ServiceNow)
2. Include the following information:
   - Product Code (e.g., GEPL-001)
   - Current configuration values
   - Proposed configuration values
   - Business justification
   - Expected EAR calculation
   - Implementation date/time

**Template**:
```
Change Request: GEPL-001 Interest Rate Reduction
Ticket: CR-2025-0127
Requested By: John Doe (Product Owner)
Date: 2025-10-27

Current Configuration:
- baseInterestRate: 0.12 (12%)
- adminFee: 0.02 (2%)
- managementFee: 0.01 (1%)
- calculatedEAR: 0.152 (15.2%)

Proposed Configuration:
- baseInterestRate: 0.10 (10%)  ← Changed
- adminFee: 0.02 (2%)
- managementFee: 0.01 (1%)
- calculatedEAR: 0.132 (13.2%)  ← Recalculated

Business Justification:
Market competitive positioning requires lower rates for government employees.
Target: Increase loan origination by 20% in Q4 2025.

EAR Compliance: 13.2% < 48% ✓
```

### Step 2: Calculate EAR

**Actor**: Product Owner

**Requirements**:
- Use approved EAR calculation tool/spreadsheet
- Include all recurring fees (management fee)
- Document calculation methodology
- Verify result is ≤ 48%

**EAR Calculation Worksheet**:
```
Product: GEPL-001
Base Interest Rate: 10% (0.10)
Admin Fee (one-time): 2% (0.02)
Management Fee (monthly): 1% (0.01)

Monthly Rate = (0.10 + 0.01) / 12 = 0.009167
EAR = (1 + 0.009167)^12 - 1 + 0.02/avgLoanAmount
EAR = 0.1155 + 0.02/25000
EAR ≈ 0.132 (13.2%)

Compliance Check: 13.2% < 48% ✓ COMPLIANT
```

### Step 3: Compliance Review

**Actor**: Compliance Officer

**Review Checklist**:
- [ ] EAR calculation methodology correct
- [ ] All recurring fees included in calculation
- [ ] Calculated EAR ≤ 48% (Bank of Zambia cap)
- [ ] `earCapCompliance` flag set correctly
- [ ] Business justification reasonable
- [ ] No conflicts with other products
- [ ] Change documented properly

**Decision Options**:
1. **Approve**: Sign off on change request
2. **Reject**: Provide reason and return to Product Owner
3. **Request Information**: Ask for clarification/additional data

**Approval Format**:
```
Compliance Approval: CR-2025-0127
Reviewed By: Jane Smith (Compliance Officer)
Date: 2025-10-27
Status: APPROVED

Verification:
✓ EAR calculation verified correct
✓ Compliance with 48% cap confirmed (13.2% < 48%)
✓ All fees accounted for
✓ Business justification acceptable

Notes: Approved for implementation. Monitor loan origination metrics
post-implementation to validate business case.

Signature: Jane Smith
Date: 2025-10-27 14:30 UTC
```

### Step 4: Apply Configuration Change

**Actor**: System Administrator

**Prerequisites**:
- Valid approval from Compliance Officer
- Change window scheduled (to minimize impact)
- Rollback plan documented

**Implementation Steps**:

1. **Backup Current Configuration**
```bash
# Read current version
vault kv get -format=json kv/intellifin/loan-products/GEPL-001/rules > gepl-001-backup-$(date +%Y%m%d-%H%M%S).json

# Save version number
vault kv metadata get kv/intellifin/loan-products/GEPL-001/rules
```

2. **Apply New Configuration**
```bash
# Write new configuration
vault kv put kv/intellifin/loan-products/GEPL-001/rules \
  productName="Government Employee Payroll Loan" \
  minAmount=1000 \
  maxAmount=50000 \
  minTermMonths=1 \
  maxTermMonths=60 \
  baseInterestRate=0.10 \
  adminFee=0.02 \
  managementFee=0.01 \
  calculatedEAR=0.132 \
  earCapCompliance=true \
  earLimit=0.48 \
  eligibilityRules='{"requiredKycStatus":"Approved","minMonthlyIncome":5000,"maxDtiRatio":0.40,"pmecRegistrationRequired":true}'
```

3. **Verify Configuration**
```bash
# Read back configuration
vault kv get kv/intellifin/loan-products/GEPL-001/rules

# Verify version incremented
vault kv metadata get kv/intellifin/loan-products/GEPL-001/rules
```

4. **Monitor Application Logs**
```bash
# Check service logs for configuration reload
kubectl logs -f deployment/loan-origination-service -n intellifin

# Look for: "Product config for GEPL-001 loaded successfully"
```

5. **Update Change Request**
```
Implementation Complete: CR-2025-0127
Implemented By: System Admin
Date: 2025-10-27 15:00 UTC
Status: COMPLETED

Actions Taken:
✓ Current configuration backed up (version 3)
✓ New configuration applied (version 4)
✓ Configuration verified in Vault
✓ Service logs show successful reload after 5-minute cache expiration
✓ No errors detected

Vault Version: 4
Previous Version: 3 (available for rollback if needed)
```

### Step 5: Post-Implementation Validation

**Actor**: Product Owner + System Administrator

**Validation Steps**:

1. **Test Product Retrieval** (after 5-minute cache expiration)
```bash
# Call API to verify new config loaded
curl -H "Authorization: Bearer $TOKEN" \
  https://api.intellifin.local/loan-origination/products/GEPL-001
```

2. **Verify EAR in Response**
```json
{
  "code": "GEPL-001",
  "name": "Government Employee Payroll Loan",
  "baseInterestRate": 0.10,
  "minAmount": 1000,
  "maxAmount": 50000
  // Verify calculatedEAR reflected correctly
}
```

3. **Test Loan Application**
```bash
# Submit test loan application with GEPL-001
curl -X POST https://api.intellifin.local/loan-origination/applications \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "productCode": "GEPL-001",
    "requestedAmount": 10000,
    "termMonths": 12
  }'
```

4. **Monitor for 24 Hours**
- Check error rates (should remain stable)
- Monitor loan application volumes
- Review customer feedback

## Rollback Procedure

If issues detected post-implementation:

### Emergency Rollback

**Actor**: System Administrator (with Compliance Officer notification)

**Steps**:

1. **Identify Previous Version**
```bash
vault kv metadata get kv/intellifin/loan-products/GEPL-001/rules
# Note previous version number
```

2. **Restore Previous Configuration**
```bash
# Rollback to version 3
vault kv rollback -version=3 kv/intellifin/loan-products/GEPL-001/rules
```

3. **Verify Rollback**
```bash
vault kv get kv/intellifin/loan-products/GEPL-001/rules
```

4. **Notify Stakeholders**
- Product Owner
- Compliance Officer
- Document reason for rollback

5. **Update Change Request**
```
Rollback Executed: CR-2025-0127
Rolled Back By: System Admin
Date: 2025-10-27 16:30 UTC
Reason: [Specific reason - e.g., "Service errors detected", "EAR calculation error"]
Status: ROLLED_BACK

Configuration restored to version 3.
Root cause analysis in progress.
```

## Audit Trail

All configuration changes are automatically logged by Vault:

### Viewing Audit Log

```bash
# View audit log (requires audit device configured)
vault audit list

# Example audit entry
{
  "time": "2025-10-27T15:00:00Z",
  "type": "response",
  "auth": {
    "display_name": "sysadmin",
    "policies": ["admin", "loan-products-write"]
  },
  "request": {
    "operation": "create",
    "path": "kv/data/intellifin/loan-products/GEPL-001/rules"
  },
  "response": {
    "data": {
      "version": 4
    }
  }
}
```

### Version History

```bash
# View all versions
vault kv metadata get kv/intellifin/loan-products/GEPL-001/rules

# Output shows:
# Version 1: Initial configuration (2025-01-15)
# Version 2: Rate adjustment (2025-06-20)
# Version 3: Term extension (2025-09-10)
# Version 4: Rate reduction (2025-10-27) ← Current
```

## Compliance Documentation

### Required Records

For each configuration change, maintain:

1. **Change Request Document** (Jira/ServiceNow ticket)
2. **EAR Calculation Worksheet** (signed by Product Owner)
3. **Compliance Approval** (signed by Compliance Officer)
4. **Implementation Record** (Vault audit log + admin notes)
5. **Validation Results** (test results, monitoring data)

### Retention Period

- **Active Configurations**: Indefinite
- **Historical Versions**: 7 years (regulatory requirement)
- **Audit Logs**: 7 years minimum
- **Approval Documents**: 7 years minimum

## Emergency Procedures

### Critical EAR Violation Detected

If a configuration with EAR > 48% is discovered in production:

1. **Immediate Actions** (within 1 hour):
   - System Admin: Disable product immediately
   - Compliance Officer: Assess regulatory exposure
   - Legal: Review affected loans

2. **Remediation** (within 24 hours):
   - Recalculate correct EAR
   - Update configuration with compliant values
   - Re-enable product after validation

3. **Reporting** (within 48 hours):
   - Report to Bank of Zambia if required
   - Document root cause analysis
   - Implement preventive measures

## Training Requirements

All personnel involved in configuration changes must complete:

- **Product Owners**: EAR calculation training (annually)
- **Compliance Officers**: Regulatory compliance training (annually)
- **System Administrators**: Vault operations training (annually)

## Contact Information

| Role | Contact | Email | Phone |
|------|---------|-------|-------|
| Product Owner | John Doe | john.doe@intellifin.com | +260-XXX-XXXX |
| Compliance Officer | Jane Smith | jane.smith@intellifin.com | +260-XXX-XXXX |
| System Admin (On-Call) | DevOps Team | devops@intellifin.com | +260-XXX-XXXX |

## References

- [Vault Product Configuration Schema](vault-product-config-schema.md)
- Bank of Zambia Money Lenders Act
- Internal Change Management Policy
- Vault Administration Guide

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-10-27 | Dev Agent | Initial workflow documentation |
