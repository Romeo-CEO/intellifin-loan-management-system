# Loan Product Catalog - Limelight Moneylink Services

## Document Information
- **Document Type**: Product Definition
- **Version**: 1.0
- **Last Updated**: [Date]
- **Owner**: Product Management Team
- **Compliance**: BoZ, Money Lenders Act, PMEC Integration

## Executive Summary

This document defines the core loan products offered by Limelight Moneylink Services, establishing the foundation for all loan origination workflows, underwriting rules, and compliance requirements. All products are designed to meet Zambian regulatory requirements while providing competitive solutions for government employees and small business owners.

## Product Overview

Limelight Moneylink Services offers two primary loan product categories:

1. **Government Employee Payroll Loans** - PMEC-integrated payroll deduction loans
2. **SME Asset-Backed Loans** - Collateral-secured business financing

## Product 1: Limelight Government Employee Payroll Loan

### Product Definition
**Product Name**: Limelight Government Employee Payroll Loan  
**Product Code**: GEPL-001  
**Target Market**: Government employees with PMEC registration  
**Primary Risk Mitigation**: Payroll deduction at source via PMEC integration  

### Core Parameters

#### Principal Amount Range
- **Minimum**: ZMW 1,000
- **Maximum**: ZMW 50,000
- **Increments**: ZMW 500

#### Term Range
- **Minimum**: 1 month
- **Maximum**: 60 months (configurable by lending institution)
- **Standard Terms**: 1, 3, 6, 12, 18, 24, 36, 48, 60 months
- **Note**: Lending institutions can configure custom terms within the 1-60 month range

#### Interest & Fee Structure

**IMPORTANT COMPLIANCE NOTE (Money Lenders Act)**:
The total cost of credit, expressed as an Effective Annual Rate (EAR), must NOT exceed 48.00%. The EAR calculation includes the nominal interest rate PLUS all mandatory, recurring fees required to get the loan. The LMS will be designed with a built-in EAR calculator that rigorously enforces this cap as a non-bypassable system rule.

**System Capability**: The system will support two calculation methods, selectable per product:

- **Reducing Balance (Recommended & Compliant)**: Interest is calculated on the outstanding principal balance for each period. This is the recommended method for transparency and compliance with Section 13 of the Act.
- **Flat Rate**: Interest is calculated on the initial principal for the entire term.

**Compliance Guardrail**: If a user selects the "Flat Rate" method, a warning will be displayed in the UI: "Warning: The 'Flat Rate' method may carry legal risk under Section 13 of the Money-lenders Act and could be challenged. Please consult your legal counsel."

**Lending institutions can define the total cost of a loan using a combination of the following recurring components**:

- **Nominal Interest Rate**: The base interest rate for the loan.
- **Admin Fee**: A fee for administrative and operational costs.
- **Management Fee**: A fee for portfolio management and oversight.
- **Insurance Fee**: A fee to cover the cost of credit life or asset insurance.
- **(Other configurable recurring fees as needed)**

**System-Calculated Effective Rate (The Hard Backstop)**:
The system will automatically take all the configured recurring cost components above, apply the chosen calculation method (Reducing Balance or Flat Rate), and compute the true Effective Annual Rate (EAR). This calculated EAR will be displayed as a read-only field in the product configuration screen. If the calculated EAR is greater than 48.00%, the system will prevent the product from being saved or activated.

#### Fee Types

**Recurring Cost Components**: The fees listed above that are part of the EAR calculation.

**Permissible One-Time Third-Party Fees (Deducted from Disbursement)**: These are pass-through costs for specific, auditable services:

- **Application Fee**: Configurable by lending institution
- **Insurance Premium**: Configurable by lending institution (Note: Use "Premium" instead of "Fee" to be precise)
- **CRB Fee**: Configurable by lending institution
- **Communication Fee**: Configurable by lending institution (SMS notifications and customer communication)

#### Early Repayment Rules
- **Minimum Term**: 3 months before early repayment allowed
- **Early Repayment Fee**: None
- **Interest Calculation**: Pro-rated to actual loan period
- **Insurance Refund**: Pro-rated refund of unearned premium

#### Required Documentation
- **Government ID**: National Registration Card
- **Employment Letter**: Current government position
- **PMEC Registration**: Valid PMEC employee number
- **Salary Slip**: Last 3 months
- **Bank Statement**: Primary bank account (3 months)
- **Loan Application Form**: Completed and signed

#### PMEC Integration Requirements
- **Payroll Deduction**: 15% of gross salary maximum (Internal Policy). This should be a configurable parameter in the Product Engine, not a hard-coded system rule.
- **Deduction Frequency**: Monthly
- **Payment Date**: 25th of each month
- **Integration Method**: Real-time API connection
- **Fallback Process**: Manual collection if PMEC unavailable

## Product 2: Limelight SME Asset-Backed Loan

### Product Definition
**Product Name**: Limelight SME Asset-Backed Loan  
**Product Code**: SMEABL-001  
**Target Market**: Small and medium enterprises with qualifying collateral  
**Primary Risk Mitigation**: Asset collateralization and business cash flow  

### Core Parameters

#### Principal Amount Range
- **Minimum**: ZMW 5,000
- **Maximum**: ZMW 200,000
- **Increments**: ZMW 1,000

#### Term Range
- **Minimum**: 1 month
- **Maximum**: 6 months (configurable by lending institution)
- **Standard Terms**: 1, 2, 3, 4, 5, 6 months
- **Note**: Lending institutions can configure custom terms within the 1-6 month range

#### Interest & Fee Structure

**IMPORTANT COMPLIANCE NOTE (Money Lenders Act)**:
The total cost of credit, expressed as an Effective Annual Rate (EAR), must NOT exceed 48.00%. The EAR calculation includes the nominal interest rate PLUS all mandatory, recurring fees required to get the loan. The LMS will be designed with a built-in EAR calculator that rigorously enforces this cap as a non-bypassable system rule.

**System Capability**: The system will support two calculation methods, selectable per product:

- **Reducing Balance (Recommended & Compliant)**: Interest is calculated on the outstanding principal balance for each period. This is the recommended method for transparency and compliance with Section 13 of the Act.
- **Flat Rate**: Interest is calculated on the initial principal for the entire term.

**Compliance Guardrail**: If a user selects the "Flat Rate" method, a warning will be displayed in the UI: "Warning: The 'Flat Rate' method may carry legal risk under Section 13 of the Money-lenders Act and could be challenged. Please consult your legal counsel."

**Lending institutions can define the total cost of a loan using a combination of the following recurring components**:

- **Nominal Interest Rate**: The base interest rate for the loan.
- **Admin Fee**: A fee for administrative and operational costs.
- **Management Fee**: A fee for portfolio management and oversight.
- **Insurance Fee**: A fee to cover the cost of credit life or asset insurance.
- **(Other configurable recurring fees as needed)**

**System-Calculated Effective Rate (The Hard Backstop)**:
The system will automatically take all the configured recurring cost components above, apply the chosen calculation method (Reducing Balance or Flat Rate), and compute the true Effective Annual Rate (EAR). This calculated EAR will be displayed as a read-only field in the product configuration screen. If the calculated EAR is greater than 48.00%, the system will prevent the product from being saved or activated.

#### Fee Types

**Recurring Cost Components**: The fees listed above that are part of the EAR calculation.

**Permissible One-Time Third-Party Fees (Deducted from Disbursement)**: These are pass-through costs for specific, auditable services:

- **Application Fee**: Configurable by lending institution
- **Insurance Premium**: Configurable by lending institution (Note: Use "Premium" instead of "Fee" to be precise)
- **CRB Fee**: Configurable by lending institution
- **Collateral Valuation Fee**: Configurable by lending institution (SME Loan only)
- **Company Search Fee**: Configurable by lending institution (SME Loan only)
- **Communication Fee**: Configurable by lending institution (SMS notifications and customer communication)

#### Early Repayment Rules
- **Minimum Term**: 3 months before early repayment allowed
- **Early Repayment Fee**: 2% of outstanding principal
- **Interest Calculation**: Pro-rated to actual loan period
- **Insurance Refund**: Pro-rated refund of unearned premium

#### Required Collateral Types

##### Acceptable Collateral Categories
1. **Real Estate**
   - Residential property (minimum 50% LTV)
   - Commercial property (minimum 40% LTV)
   - Agricultural land (minimum 30% LTV)

2. **Vehicles**
   - Passenger vehicles (maximum 5 years old)
   - Commercial vehicles (maximum 7 years old)
   - Agricultural equipment (maximum 10 years old)

3. **Business Assets**
   - Machinery and equipment
   - Inventory (maximum 40% LTV)
   - Accounts receivable (maximum 30% LTV)

##### Collateral Requirements
- **Minimum LTV**: 30% of loan amount
- **Maximum LTV**: 70% of appraised value
- **Insurance**: Full replacement value coverage
- **Documentation**: Title deeds, registration certificates
- **Valuation**: Professional appraisal required

#### Required Documentation
- **Business Registration**: Certificate of incorporation
- **Tax Clearance**: ZRA tax clearance certificate
- **Financial Statements**: Last 2 years audited accounts
- **Business Plan**: 3-year business projections
- **Collateral Documentation**: Title deeds, valuations
- **Personal Guarantees**: Directors' personal guarantees
- **Loan Application Form**: Completed and signed

## Product Comparison Matrix

| Parameter | Government Employee | SME Asset-Backed |
|-----------|-------------------|------------------|
| **Principal Range** | ZMW 1,000 - 50,000 | ZMW 5,000 - 200,000 |
| **Term Range** | 1 - 60 months | 1 - 6 months |
| **Interest Rate** | Configurable (EAR ≤48%) | Configurable (EAR ≤48%) |
| **Risk Mitigation** | PMEC payroll deduction | Asset collateralization |
| **Application Fee** | Configurable | Configurable |
| **Fee Structure** | Configurable components | Configurable components |
| **Early Repayment** | No fee | 2% of outstanding |
| **Documentation** | Employment + PMEC | Business + Collateral |

## Fee Configuration System

### Overview
The LMS system provides a flexible fee configuration system that allows lending institutions to customize their fee structures while maintaining compliance with the Money Lenders Act.

### Key Principles
1. **Effective Rate Cap**: 48% p.a. maximum is **MANDATORY** and enforced by system
2. **Fee Flexibility**: All fees are configurable by the lending institution
3. **Total Cost Control**: The system ensures the total effective rate never exceeds 48% p.a.
4. **Transparency**: All fees must be clearly disclosed to customers upfront

### Fee Configuration Options
Lending institutions can configure:
- **Recurring Fees**: Nominal interest, admin, insurance, management fees
- **One-time Fees**: Application, CRB, company search, collateral valuation
- **Calculation Methods**: Flat rate vs. reducing balance per product

### System Controls
- **Rate Validation**: System automatically validates total effective rate
- **Configuration Lock**: Fee changes require management approval
- **Audit Trail**: All fee configuration changes are logged
- **Customer Disclosure**: Updated fee schedules automatically generated

## Compliance Requirements

### Money Lenders Act Compliance
- **Interest Rate Cap**: Maximum 48% per annum (both products compliant)
- **Fee Transparency**: All fees clearly disclosed upfront
- **Penalty Limits**: Late payment penalties within regulatory limits
- **Documentation**: Complete loan agreement with all terms

### BoZ Compliance
- **KYC Requirements**: Full customer identification compliance
- **Risk Classification**: Proper loan classification and provisioning
- **Reporting**: Monthly prudential returns
- **Audit Trail**: Complete transaction audit trail

### PMEC Integration Compliance
- **Data Protection**: Government employee data security
- **Audit Requirements**: Complete PMEC transaction logging
- **Fallback Procedures**: Manual collection when PMEC unavailable

## Product Updates and Modifications

### Change Management Process
1. **Product Change Request**: Submitted to Product Management
2. **Compliance Review**: Legal and regulatory review
3. **Risk Assessment**: Credit and operational risk review
4. **Board Approval**: Final approval required for rate changes
5. **Implementation**: 30-day notice period for existing customers

### Version Control
- **Document Version**: Updated with each change
- **Change Log**: Maintained for audit purposes
- **Customer Notification**: Required for material changes
- **Regulatory Notification**: Required for compliance changes

## Next Steps

This product catalog serves as the foundation for:
1. **Application Workflow Documentation** - Process flows based on product rules
2. **Underwriting Rules** - Credit assessment criteria per product
3. **Disbursement Processes** - Product-specific disbursement workflows
4. **Collections Procedures** - Product-specific collection strategies

## Document Approval

- **Product Manager**: [Name] - [Date]
- **Legal Counsel**: [Name] - [Date]
- **Risk Manager**: [Name] - [Date]
- **CEO**: [Name] - [Date]

---

**Document Control**: This document must be reviewed and updated annually or when product changes occur.
