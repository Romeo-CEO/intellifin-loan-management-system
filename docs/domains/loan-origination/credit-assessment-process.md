# Credit Assessment Process - Limelight Moneylink Services

## Document Information
- **Document Type**: Business Process Specification
- **Version**: 1.0
- **Last Updated**: [Date]
- **Owner**: Credit Assessment Team
- **Compliance**: BoZ Requirements, Money Lenders Act, Risk Management Standards
- **Reference**: Loan Product Catalog, Loan Application Workflow, PMEC Integration

## Executive Summary

This document defines the credit assessment process for Limelight's two core V1 products: Government Employee Payroll Loans and SME Asset-Backed Loans. It focuses on a system-assisted, rules-based approach, leveraging TransUnion for first-time applicants and PMEC for government employee verification to ensure consistent and compliant credit decisions. The system automates data gathering and initial checks, presenting a clear, concise summary to the underwriter, who then makes a final, reasoned decision based on policy.

## Business Context

### Why Credit Assessment is Critical
- **Risk Management**: Primary mechanism for controlling portfolio risk and credit losses
- **Portfolio Quality**: Ensures only creditworthy borrowers receive loans
- **Regulatory Compliance**: Meets BoZ requirements for prudent lending practices
- **Profitability**: Balances risk and return for sustainable business operations
- **Customer Protection**: Prevents over-indebtedness and financial distress

### Assessment Design Principles
- **System-Assisted**: Automated data gathering and risk grade calculation
- **Rules-Based**: Simple, configurable decision framework (A-F grades)
- **Focused**: Specific to Government Employee and SME loan products
- **Compliant**: Adherence to all regulatory and internal policy requirements
- **Efficient**: Streamlined process for timely decision making

## Credit Assessment Overview

### High-Level Process Flow
```
Application Received → Automated Data Checks → Underwriter Manual Review → Decision & Documentation
```

### Assessment Stages
1. **Application Review & Initial Assessment** (Data Quality & Completeness)
2. **External Verification** (Credit Bureau, Employment, Income)
3. **Risk Assessment** (Rules-Based Risk Grade Calculation)
4. **Collateral Evaluation** (Asset Assessment & Valuation)
5. **Decision Making** (Approval, Rejection, or Conditional Approval)

## Stage 1: Application Review & Initial Assessment

### 1.1 Application Completeness Review
**Required Information Validation**:
```
Personal Information:
- Complete identity verification
- Valid contact information
- Current address verification
- Employment/business details
- Financial information completeness

Documentation:
- All required documents uploaded
- Document authenticity verification
- Information consistency check
- Recent document dates
- Proper document formats
```

**Data Quality Assessment**:
```
Quality Indicators:
- Information completeness score
- Document quality rating
- Data consistency check
- Cross-reference validation
- Verification status tracking
```

### 1.2 Initial Eligibility Confirmation
**Basic Eligibility Verification**:
```
Age and Residency:
- Age verification (18-65 years)
- Zambian citizenship/residency
- Valid identification documents
- Current address confirmation

Employment/Business Status:
- Government employment verification
- Business ownership confirmation
- Minimum employment duration
- Income level verification
```

**Regulatory Compliance Check**:
```
BoZ Requirements:
- Age and residency compliance
- Employment status verification
- Income level compliance
- Previous loan history check
- Regulatory reporting preparation
```

## Stage 2: External Verification

### 2.1 Credit Bureau Check
**Smart Routing Logic**:
```
System Action: The system will first classify the applicant as 'New' or 'Existing.'

If applicant is 'New', the system will automatically make a real-time API call to TransUnion Zambia.

If applicant is 'Existing' and in good standing, this step is bypassed to optimize costs.
```

**Credit History Analysis**:
```
Credit Report Review:
- Previous loan history
- Payment behavior patterns
- Credit utilization
- Outstanding obligations
- Credit score (if available)
- Adverse information
- Collection accounts
- Bankruptcy records
```

**Credit Bureau Integration**:
```
Data Sources:
- TransUnion Zambia
- Other credit bureaus
- Public records
- Court judgments
- Tax liens
- Business registries
```

**Credit History Scoring**:
```
Scoring Criteria:
- Payment history (40%)
- Outstanding debt (30%)
- Credit history length (15%)
- New credit applications (10%)
- Credit mix (5%)
```

### 2.2 Employment Verification
**Government Employee Verification**:
```
Verification Process:
- PMEC employment confirmation
- Government department verification
- Employment status confirmation
- Salary level verification
- Employment duration confirmation
- Benefits verification

Verification Sources:
- PMEC database
- Government HR systems
- Employment letters
- Salary slips
- Government ID verification
```

**SME Business Verification**:
```
Business Verification:
- Business registration confirmation
- Tax registration verification
- Business license validation
- Business address verification
- Business operations confirmation
- Market position assessment
```

### 2.3 Income Verification
**Income Source Validation**:
```
Primary Income:
- Salary verification
- Employment confirmation
- Income stability assessment
- Benefits calculation
- Overtime and bonus income
- Income frequency verification

Additional Income:
- Rental income verification
- Investment income validation
- Business income confirmation
- Other income sources
- Income sustainability assessment
```

**Income Documentation Review**:
```
Required Documents:
- Salary slips (last 6 months)
- Bank statements (last 6 months)
- Tax returns (last 2 years)
- Employment contracts
- Benefits documentation
- Business financial statements
```

## Stage 3: Risk Assessment

### 3.1 Financial Analysis
**Income Analysis**:
```
Income Assessment:
- Monthly gross income
- Monthly net income
- Income stability
- Income growth trends
- Income diversification
- Seasonal income patterns
- Income sustainability
```

**Expense Analysis**:
```
Expense Assessment:
- Monthly living expenses
- Debt obligations
- Housing costs
- Transportation costs
- Healthcare expenses
- Education costs
- Other regular expenses
```

**Debt Service Analysis**:
```
Debt Service Calculation:
- Total monthly debt payments
- Debt service ratio (DSR)
- Available income for new loan
- Debt-to-income ratio
- Credit utilization ratio
- Payment capacity assessment
```

### 3.2 Risk Indicators Identification
**Personal Risk Factors**:
```
Risk Indicators:
- Age and health status
- Marital status and dependents
- Employment stability
- Income volatility
- Previous financial problems
- Legal issues
- Personal references
```

**Financial Risk Factors**:
```
Financial Risk:
- High debt levels
- Irregular income
- Poor payment history
- Multiple credit applications
- High credit utilization
- Insufficient savings
- Asset deficiency
```

**Business Risk Factors (SME)**:
```
Business Risk:
- Industry volatility
- Market competition
- Business model sustainability
- Financial performance
- Management capability
- Market position
- Growth prospects
```

### 3.3 Risk Classification
**Risk Categories**:
```
Risk Classification:
- Low Risk: Excellent credit history, stable income, low debt
- Medium Risk: Good credit history, stable income, moderate debt
- High Risk: Poor credit history, unstable income, high debt
- Very High Risk: Multiple risk factors, poor financial situation
```

**Rules-Based Risk Grade**:
```
The system will automatically calculate a 'Calculated Risk Grade' (A, B, C, D, F) for each application.

This grade is determined by a simple, configurable rules-based scorecard based on three primary, weighted inputs:
- CRB Score (from TransUnion for new applicants)
- Debt-to-Income (DTI) Ratio (calculated from application data and CRB report)
- Length of Employment/Time in Business (as a stability metric)

The output is a recommendation, not a final decision. For example:
- Grade A: 'Recommended for Approval'
- Grade B: 'Recommended for Approval with Conditions'
- Grade C: 'Manual Review Required'
- Grade D: 'Recommended for Rejection - High Risk'
- Grade F: 'Recommended for Rejection - Critical Risk'
```

## Stage 4: Collateral Evaluation

### 4.1 Collateral Assessment (SME Loans)
**Collateral Types**:
```
Acceptable Collateral:
- Real estate (residential/commercial)
- Motor vehicles
- Equipment and machinery
- Inventory and stock
- Investment securities
- Insurance policies
- Other valuable assets
```

**Collateral Valuation**:
```
Valuation Process:
- Professional appraisal
- Market value assessment
- Liquidation value calculation
- Collateral quality rating
- Marketability assessment
- Legal encumbrance check
- Insurance coverage verification
```

**Loan-to-Value (LTV) Calculation**:
```
LTV Requirements:
- Real Estate: Maximum 70% LTV
- Motor Vehicles: Maximum 60% LTV
- Equipment: Maximum 50% LTV
- Inventory: Maximum 40% LTV
- Securities: Maximum 60% LTV
- Other Assets: Maximum 50% LTV
```

### 4.2 Government Employee Loan Security
**Primary Security Mechanism**:
```
Payroll deduction at source via PMEC integration. The system's primary risk mitigation is the verification of the applicant's status and salary through the PMEC API. This is considered the primary security for this product.

Security Features:
- Payroll deduction guarantee
- Government employment security
- Salary stability
- Employment verification
- Automatic payment processing
- Default risk mitigation
```



## Stage 6: Decision Making

### 6.1 Decision Categories
**Approval Decisions**:
```
Approval Types:
- Full Approval: All terms as requested
- Conditional Approval: Specific conditions must be met
- Modified Approval: Reduced amount or modified terms
- Staged Approval: Conditional with periodic reviews
```

**Rejection Decisions**:
```
Rejection Reasons:
- Insufficient income
- Poor credit history
- High debt levels
- Incomplete documentation
- Regulatory non-compliance
- Risk too high
- Collateral insufficient
```

### 6.2 Decision Authority Matrix
**Approval authority is governed by a matrix based on both Loan Amount and the Calculated Risk Grade.**

**Example Rules**:
```
Rule 1 (Segregation of Duties): A Loan Officer can process an application, but cannot approve any loan. Approval authority starts at the Credit Analyst level.

Rule 2 (Amount-Based): A Credit Analyst can approve loans up to ZMW 50,000. Loans above this amount must be escalated to the Head of Credit.

Rule 3 (Risk-Based): Any loan with a Calculated Risk Grade of 'C' or below, regardless of amount, must be approved by the Head of Credit.

Rule 4 (CEO Authority): The CEO has the authority to issue provisional offline approvals and is the final approver for loans exceeding a specific high-value threshold (e.g., ZMW 250,000).
```

**Authority Structure**:
```
Authority Levels:
- Credit Analyst: Up to ZMW 50,000 (Risk Grades A & B only)
- Head of Credit: Up to ZMW 100,000 (All Risk Grades)
- Credit Committee: Up to ZMW 250,000 (All Risk Grades)
- CEO: Above ZMW 250,000 (All Risk Grades, plus offline approvals)
```

**Special Approval Requirements**:
```
Special Circumstances:
- High-risk applications
- Large loan amounts
- New product types
- Special customer categories
- Regulatory exceptions
- Policy deviations
```

### 6.3 Decision Documentation
**Decision Record**:
```
Required Documentation:
- Credit assessment summary
- Risk analysis results
- Calculated Risk Grade (A-F)
- Decision justification
- Approval conditions
- Risk mitigation measures
- Monitoring requirements
```

**Decision Communication**:
```
Customer Communication:
- Decision notification
- Approval terms and conditions
- Rejection reasons (if applicable)
- Appeal process information
- Next steps guidance
- Contact information
```

## Risk Management Controls

### 1. Portfolio Risk Controls
**Concentration Limits**:
```
Risk Limits:
- Single borrower limit: 5% of total portfolio
- Sector concentration: Maximum 30% in any sector
- Geographic concentration: Maximum 40% in any region
- Product concentration: Maximum 60% in any product
- Collateral concentration: Maximum 50% in any asset type
```

**Risk Monitoring**:
```
Monitoring Metrics:
- Portfolio risk distribution
- Risk grade distribution (A-F)
- Risk rating trends
- Default rate monitoring
- Loss rate tracking
- Portfolio quality metrics
```

### 2. Process Controls
**Quality Assurance**:
```
Quality Checks:
- Assessment accuracy review
- Decision consistency check
- Documentation completeness
- Policy compliance verification
- Risk rating validation
- Decision authority verification
```

**Audit Trail**:
```
Audit Requirements:
- Complete assessment record
- Decision justification
- Authority verification
- Policy compliance
- Risk assessment documentation
- Decision communication record
```

### 3. Performance Controls
**Timeline Management**:
```
Processing Targets:
- Initial review: 1-2 days
- External verification: 2-3 days
- Risk assessment: 1-2 days
- Collateral evaluation: 1-2 days
- Decision making: 1-2 days
- Total assessment time: 6-9 days
```

**Quality Metrics**:
```
Performance Indicators:
- Assessment accuracy
- Decision consistency
- Processing time compliance
- Customer satisfaction
- Portfolio quality
- Default rate
- Loss rate
```

## Compliance Requirements

### 1. Regulatory Compliance
**BoZ Requirements**:
```
Regulatory Standards:
- Prudent lending practices
- Risk management framework
- Portfolio quality standards
- Capital adequacy requirements
- Liquidity management
- Regulatory reporting
```

**Money Lenders Act**:
```
Act Compliance:
- Interest rate limits (48% EAR)
- Fee structure compliance
- Loan term requirements
- Documentation standards
- Customer protection
- Regulatory reporting
```

### 2. Internal Policy Compliance
**Policy Requirements**:
```
Internal Standards:
- Credit policy compliance
- Risk management policy
- Approval authority matrix
- Documentation standards
- Quality assurance requirements
- Performance standards
```

**Process Compliance**:
```
Process Standards:
- Workflow compliance
- Documentation requirements
- Approval procedures
- Risk assessment standards
- Decision documentation
- Communication requirements
```

## Next Steps

This Credit Assessment Process document serves as the foundation for:
1. **System Configuration** - Rules-based risk grade calculation and workflow setup
2. **Staff Training** - Credit analyst training on simplified assessment procedures
3. **Policy Development** - Credit policy and risk management framework
4. **Quality Assurance** - Assessment accuracy and consistency monitoring

## Document Approval

- **Credit Assessment Manager**: [Name] - [Date]
- **Risk Management Officer**: [Name] - [Date]
- **Compliance Officer**: [Name] - [Date]
- **CEO**: [Name] - [Date]

---

**Document Control**: This document must be reviewed and updated quarterly or when business processes change.
