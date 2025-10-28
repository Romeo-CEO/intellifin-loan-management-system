# Vault Product Configuration Schema

**Version**: 1.0  
**Last Updated**: 2025-10-27  
**Vault Path**: `kv/intellifin/loan-products/{productCode}/rules`

## Overview

This document defines the JSON schema for loan product configurations stored in HashiCorp Vault. Product configurations control lending parameters, fees, interest rates, and eligibility rules dynamically without requiring code deployment.

## Vault Path Structure

```
kv/intellifin/loan-products/
├── GEPL-001/rules          # Government Employee Payroll Loan
├── SMEABL-001/rules        # SME Asset-Backed Loan
├── BL-STANDARD/rules       # Standard Business Loan
└── {productCode}/rules     # Product-specific configuration
```

## Schema Definition

### Root Configuration Object

```json
{
  "productName": "string",           // Human-readable product name
  "minAmount": number,               // Minimum loan amount (ZMW)
  "maxAmount": number,               // Maximum loan amount (ZMW)
  "minTermMonths": number,           // Minimum loan term (months)
  "maxTermMonths": number,           // Maximum loan term (months)
  "baseInterestRate": number,        // Base annual interest rate (decimal, e.g., 0.12 = 12%)
  "adminFee": number,                // One-time admin fee (decimal, e.g., 0.02 = 2%)
  "managementFee": number,           // Recurring management fee (decimal, e.g., 0.01 = 1%)
  "calculatedEAR": number,           // Effective Annual Rate including all fees (decimal)
  "earCapCompliance": boolean,       // Whether EAR meets regulatory cap
  "earLimit": number,                // Maximum allowed EAR (0.48 = 48% Bank of Zambia cap)
  "eligibilityRules": {              // Applicant qualification criteria
    "requiredKycStatus": "string",   // Required KYC status (e.g., "Approved")
    "minMonthlyIncome": number,      // Minimum monthly income (ZMW)
    "maxDtiRatio": number,           // Maximum debt-to-income ratio (0.0-1.0)
    "pmecRegistrationRequired": boolean  // PMEC registration requirement
  }
}
```

## Field Specifications

### Financial Parameters

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `productName` | string | Yes | Human-readable product name | "Government Employee Payroll Loan" |
| `minAmount` | decimal | Yes | Minimum loan amount in ZMW | 1000 |
| `maxAmount` | decimal | Yes | Maximum loan amount in ZMW | 50000 |
| `minTermMonths` | integer | Yes | Minimum loan term in months | 1 |
| `maxTermMonths` | integer | Yes | Maximum loan term in months | 60 |

### Interest and Fees

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `baseInterestRate` | decimal | Yes | Base annual interest rate (0.0-1.0) | 0.12 (12%) |
| `adminFee` | decimal | Yes | One-time administrative fee (0.0-1.0) | 0.02 (2%) |
| `managementFee` | decimal | Yes | Recurring management fee (0.0-1.0) | 0.01 (1%) |

### EAR Compliance (Bank of Zambia Regulation)

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `calculatedEAR` | decimal | Yes | Effective Annual Rate including ALL recurring fees | 0.152 (15.2%) |
| `earCapCompliance` | boolean | Yes | Whether EAR meets 48% regulatory cap | true |
| `earLimit` | decimal | Yes | Maximum allowed EAR per BoZ Money Lenders Act | 0.48 (48%) |

**IMPORTANT**: The system validates `calculatedEAR <= earLimit` before loading the configuration. Non-compliant configurations are rejected with `ComplianceException`.

### Eligibility Rules

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `requiredKycStatus` | string | Yes | Required KYC verification status | "Approved" |
| `minMonthlyIncome` | decimal | Yes | Minimum monthly income in ZMW | 5000 |
| `maxDtiRatio` | decimal | Yes | Maximum debt-to-income ratio (0.0-1.0) | 0.40 (40%) |
| `pmecRegistrationRequired` | boolean | Yes | PMEC (Pension) registration requirement | true |

## Example Configurations

### GEPL-001: Government Employee Payroll Loan

**Vault Path**: `kv/intellifin/loan-products/GEPL-001/rules`

```json
{
  "productName": "Government Employee Payroll Loan",
  "minAmount": 1000,
  "maxAmount": 50000,
  "minTermMonths": 1,
  "maxTermMonths": 60,
  "baseInterestRate": 0.12,
  "adminFee": 0.02,
  "managementFee": 0.01,
  "calculatedEAR": 0.152,
  "earCapCompliance": true,
  "earLimit": 0.48,
  "eligibilityRules": {
    "requiredKycStatus": "Approved",
    "minMonthlyIncome": 5000,
    "maxDtiRatio": 0.40,
    "pmecRegistrationRequired": true
  }
}
```

**Key Features**:
- Target: Government employees with PMEC payroll deductions
- EAR: 15.2% (well below 48% cap)
- Income requirement: K5,000/month minimum
- DTI limit: 40% maximum

### SMEABL-001: SME Asset-Backed Loan

**Vault Path**: `kv/intellifin/loan-products/SMEABL-001/rules`

```json
{
  "productName": "SME Asset-Backed Loan",
  "minAmount": 10000,
  "maxAmount": 500000,
  "minTermMonths": 12,
  "maxTermMonths": 84,
  "baseInterestRate": 0.18,
  "adminFee": 0.025,
  "managementFee": 0.015,
  "calculatedEAR": 0.224,
  "earCapCompliance": true,
  "earLimit": 0.48,
  "eligibilityRules": {
    "requiredKycStatus": "Approved",
    "minMonthlyIncome": 15000,
    "maxDtiRatio": 0.35,
    "pmecRegistrationRequired": false
  }
}
```

**Key Features**:
- Target: Small/Medium businesses with asset collateral
- EAR: 22.4% (below 48% cap)
- Income requirement: K15,000/month minimum
- DTI limit: 35% maximum
- No PMEC requirement (business loan)

## EAR Calculation Methodology

### Formula

```
EAR = (1 + (baseInterestRate + managementFee)/n)^n - 1 + adminFee/loanAmount
```

Where:
- `n` = number of compounding periods per year (typically 12 for monthly)
- `adminFee` is amortized over loan term

### Example Calculation (GEPL-001)

```
Base Interest: 12% (0.12)
Management Fee: 1% (0.01) recurring monthly
Admin Fee: 2% (0.02) one-time

Monthly rate = (0.12 + 0.01) / 12 = 0.0108333
EAR = (1 + 0.0108333)^12 - 1 + 0.02/loanAmount
EAR ≈ 0.152 (15.2%)
```

**Compliance Check**: 15.2% < 48% ✅

## Vault Operations

### Creating a New Product Configuration

```bash
# Login to Vault
vault login <token>

# Write configuration
vault kv put kv/intellifin/loan-products/GEPL-001/rules \
  productName="Government Employee Payroll Loan" \
  minAmount=1000 \
  maxAmount=50000 \
  minTermMonths=1 \
  maxTermMonths=60 \
  baseInterestRate=0.12 \
  adminFee=0.02 \
  managementFee=0.01 \
  calculatedEAR=0.152 \
  earCapCompliance=true \
  earLimit=0.48 \
  eligibilityRules='{"requiredKycStatus":"Approved","minMonthlyIncome":5000,"maxDtiRatio":0.40,"pmecRegistrationRequired":true}'
```

### Reading a Configuration

```bash
vault kv get kv/intellifin/loan-products/GEPL-001/rules
```

### Listing All Product Configurations

```bash
vault kv list kv/intellifin/loan-products/
```

### Viewing Configuration History (KV v2)

```bash
# View all versions
vault kv metadata get kv/intellifin/loan-products/GEPL-001/rules

# Read specific version
vault kv get -version=1 kv/intellifin/loan-products/GEPL-001/rules
```

## Validation Rules

The `VaultProductConfigService` enforces the following validation rules:

### 1. EAR Compliance (Critical)
- **Rule**: `calculatedEAR <= earLimit`
- **Action**: Throw `ComplianceException` if violated
- **Reason**: Bank of Zambia Money Lenders Act compliance

### 2. Amount Ranges
- **Rule**: `minAmount < maxAmount`
- **Action**: Implicit validation (application logic)

### 3. Term Ranges
- **Rule**: `minTermMonths < maxTermMonths`
- **Action**: Implicit validation (application logic)

### 4. Rate Ranges
- **Rule**: `0 < baseInterestRate < 1`
- **Action**: Business validation

### 5. DTI Ratio
- **Rule**: `0 < maxDtiRatio <= 1`
- **Action**: Business validation

## Caching Behavior

- **Cache Duration**: 5 minutes (300 seconds)
- **Cache Key**: `product-config:{productCode}`
- **Eviction**: Automatic expiration (no manual invalidation)
- **Cache Miss**: Vault read + EAR validation + caching

**Impact**: Configuration changes take up to 5 minutes to propagate to running services.

## Security Considerations

### Access Control

```hcl
# Example Vault policy for LoanOriginationService
path "kv/data/intellifin/loan-products/*" {
  capabilities = ["read", "list"]
}

path "kv/metadata/intellifin/loan-products/*" {
  capabilities = ["read", "list"]
}
```

### Audit Trail

- Vault automatically logs all read/write operations
- Access logs include: timestamp, principal, path, action
- Retention: Per Vault audit configuration (typically 90+ days)

## Troubleshooting

### Configuration Not Found

**Error**: `InvalidOperationException: Product configuration not found for {productCode}`

**Solutions**:
1. Verify Vault path: `vault kv get kv/intellifin/loan-products/{productCode}/rules`
2. Check service has read permissions
3. Ensure product code matches exactly (case-sensitive)

### EAR Compliance Violation

**Error**: `ComplianceException: Product {code} EAR {ear} exceeds Bank of Zambia limit {limit}`

**Solutions**:
1. Recalculate EAR using proper formula
2. Reduce base interest rate or fees
3. Contact Compliance Officer for review

### Vault Connection Failed

**Error**: `InvalidOperationException: Failed to load product configuration`

**Solutions**:
1. Check Vault connectivity: `curl https://vault.intellifin.local:8200/v1/sys/health`
2. Verify token validity: `vault token lookup`
3. Service falls back to in-memory configuration automatically

## Change Control

All configuration changes require dual approval:
1. Product Owner proposes change
2. Compliance Officer reviews EAR compliance
3. Both approve via documented workflow
4. Change applied to Vault with audit trail

See [Configuration Change Workflow](vault-config-change-workflow.md) for details.

## References

- [Bank of Zambia Money Lenders Act](https://www.boz.zm)
- [VaultSharp Documentation](https://github.com/rajanadar/VaultSharp)
- [HashiCorp Vault KV Secrets Engine](https://www.vaultproject.io/docs/secrets/kv)
