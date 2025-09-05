# Credit Scoring Methodology - Limelight Moneylink Services

## Executive Summary

This document defines the comprehensive credit scoring methodology for the Limelight Moneylink Services LMS system. It outlines the rules-based decision framework, risk assessment criteria, and scoring algorithms used to evaluate creditworthiness for both Government Employee Payroll Loans and SME Asset-Backed Loans, ensuring consistent, objective, and compliant credit decisions.

## Business Context

### Why Credit Scoring is Critical
- **Risk Management**: Systematic assessment of borrower creditworthiness
- **Decision Consistency**: Standardized approach to credit decisions
- **Regulatory Compliance**: Adherence to BoZ and Money Lenders Act requirements
- **Portfolio Quality**: Maintaining healthy loan portfolio performance
- **Operational Efficiency**: Automated decision-making for faster processing

### Scoring Philosophy
- **Rules-Based**: Simple, configurable decision framework (A-F grades)
- **Transparent**: Clear criteria and decision logic
- **Risk-Focused**: Primary emphasis on repayment ability and risk factors
- **Compliant**: Built-in regulatory compliance and audit requirements
- **Adaptive**: Configurable parameters for different market conditions

## Credit Scoring Framework

### 1. Scoring Components
**Primary Scoring Factors**:
```
Core Factors:
- Credit Bureau Score (TransUnion)
- Debt-to-Income (DTI) Ratio
- Length of Employment/Time in Business
- Income Stability
- Collateral Quality (SME only)
- Payment History (existing customers)
```

**Secondary Scoring Factors**:
```
Supporting Factors:
- Age and experience
- Education level
- Marital status
- Number of dependents
- Banking relationship
- Geographic location
- Industry risk (SME)
```

### 2. Risk Grade System
**Risk Grade Categories**:
```
Risk Grades:
- Grade A: Excellent (Recommended for Approval)
- Grade B: Good (Recommended for Approval with Conditions)
- Grade C: Average (Manual Review Required)
- Grade D: Below Average (Recommended for Rejection - High Risk)
- Grade F: Poor (Recommended for Rejection - Critical Risk)
```

**Grade Interpretation**:
```
Grade Meanings:
- Grade A: Low risk, standard terms
- Grade B: Low-medium risk, standard terms with monitoring
- Grade C: Medium risk, enhanced terms or conditions
- Grade D: High risk, restrictive terms or rejection
- Grade F: Very high risk, automatic rejection
```

## Scoring Methodology

The system does not use a complex numerical score. Instead, it assigns a **'Calculated Risk Grade' (A, B, C, D, F)** based on a set of clear, configurable business rules.

### 1. Government Employee Payroll Loans
The Risk Grade is determined by rules based on three primary inputs:
- **Credit Bureau Score** (from TransUnion)
- **Debt-to-Income (DTI) Ratio**
- **Length of Employment**

**Example Rules**:
```
IF Credit Score > 700 AND DTI < 35% AND Employment > 3 years THEN Risk Grade = A
IF Credit Score > 650 AND DTI < 40% AND Employment > 2 years THEN Risk Grade = B
IF Credit Score > 600 AND DTI < 50% AND Employment > 1 year THEN Risk Grade = C
IF Credit Score < 600 OR DTI > 50% OR Employment < 1 year THEN Risk Grade = D
IF Credit Score < 550 OR DTI > 60% THEN Risk Grade = F
```

### 2. SME Asset-Backed Loans
The Risk Grade is determined by rules based on four primary inputs:
- **Credit Bureau Score**
- **Debt Service Coverage Ratio (DSCR)** (more appropriate for businesses than DTI)
- **Time in Business**
- **Loan-to-Value (LTV) Ratio** of the provided collateral.

**Example Rules**:
```
IF Credit Score > 700 AND DSCR > 1.5 AND Business > 3 years AND LTV < 60% THEN Risk Grade = A
IF Credit Score > 650 AND DSCR > 1.25 AND Business > 2 years AND LTV < 70% THEN Risk Grade = B
IF Credit Score > 600 AND DSCR > 1.0 AND Business > 1 year AND LTV < 80% THEN Risk Grade = C
IF LTV > 70% OR DSCR < 1.25 THEN Risk Grade = D
IF Credit Score < 550 OR DSCR < 1.0 OR LTV > 80% THEN Risk Grade = F
```

## Risk Assessment Criteria

### 1. Credit Bureau Integration
**TransUnion Integration**:
```
Integration Features:
- Real-time credit score retrieval
- Credit history analysis
- Payment behavior assessment
- Credit utilization analysis
- Recent credit inquiries
- Public records check
```

**Credit Score Interpretation**:
```
Score Ranges:
- 750-850: Excellent credit
- 700-749: Good credit
- 650-699: Fair credit
- 600-649: Poor credit
- 300-599: Very poor credit
```

### 2. Income Assessment
**Income Verification**:
```
Verification Methods:
- Salary slips (last 3 months)
- Bank statements
- Employment verification
- PMEC verification (Government employees)
- Business financial statements (SME)
- Tax returns
```

**Income Stability Factors**:
```
Stability Indicators:
- Consistent payment history
- Salary growth patterns
- Employment stability
- Industry stability
- Economic conditions
- Seasonal variations
```

### 3. Debt Analysis
**Debt Assessment**:
```
Debt Components:
- Existing loan payments
- Credit card payments
- Other financial obligations
- Business debts (SME)
- Personal guarantees
- Co-signed loans
```

**DTI Calculation**:
```
DTI Formula:
DTI = (Total Monthly Debt Payments / Gross Monthly Income) × 100

DTI Thresholds:
- 0-30%: Low risk
- 31-40%: Medium risk
- 41-50%: High risk
- Above 50%: Very high risk
```

## Decision Rules

### 1. Approval Rules
**Automatic Approval**:
```
Approval Criteria:
- Grade A risk score
- Complete documentation
- Income verification
- No red flags
- Regulatory compliance
```

**Conditional Approval**:
```
Conditional Criteria:
- Grade B risk score
- Minor documentation gaps
- Income verification pending
- Acceptable risk factors
- Additional conditions required
```

### 2. Rejection Rules
**Automatic Rejection**:
```
Rejection Criteria:
- Grade F risk score
- Incomplete documentation
- Income below minimum
- High DTI ratio
- Regulatory violations
- Fraud indicators
```

**Manual Review Required**:
```
Review Criteria:
- Grade C risk score
- Borderline documentation
- Income verification issues
- Unusual circumstances
- Policy exceptions
- Special cases
```

## Scoring Configuration

### 1. Configurable Parameters
**Weight Adjustments**:
```
Configurable Weights:
- Credit Bureau Score weight
- DTI Ratio weight
- Employment/Business length weight
- Income stability weight
- Collateral quality weight (SME)
```

**Threshold Adjustments**:
```
Configurable Thresholds:
- Risk grade boundaries
- DTI ratio limits
- Income minimums
- Collateral requirements
- Score cutoffs
```

### 2. Market Conditions
**Economic Factors**:
```
Market Adjustments:
- Interest rate environment
- Economic growth
- Industry conditions
- Regulatory changes
- Risk appetite
- Portfolio performance
```

## Quality Assurance

### 1. Scoring Validation
**Validation Methods**:
```
Validation Techniques:
- Backtesting
- Model validation
- Performance monitoring
- Accuracy assessment
- Bias testing
- Regulatory compliance
```

**Performance Metrics**:
```
Key Metrics:
- Default rates by grade
- Loss rates by grade
- Score distribution
- Model accuracy
- Predictive power
- Stability measures
```

### 2. Continuous Improvement
**Model Updates**:
```
Update Triggers:
- Performance degradation
- Regulatory changes
- Market conditions
- New data availability
- Model validation results
- Business requirements
```

**Improvement Process**:
```
Improvement Steps:
1. Performance analysis
2. Model assessment
3. Parameter adjustment
4. Validation testing
5. Implementation
6. Monitoring
7. Documentation
```

## Compliance and Audit

### 1. Regulatory Compliance
**BoZ Requirements**:
```
Compliance Standards:
- Risk assessment procedures
- Credit scoring methodology
- Documentation requirements
- Audit trail maintenance
- Reporting requirements
- Model validation
```

**Money Lenders Act Compliance**:
```
Act Requirements:
- Fair lending practices
- Non-discriminatory scoring
- Transparency requirements
- Documentation standards
- Audit requirements
- Consumer protection
```

### 2. Audit Trail
**Required Documentation**:
```
Audit Information:
- Scoring methodology
- Parameter settings
- Decision logic
- Model validation
- Performance metrics
- Regulatory compliance
```

## Technology Implementation

### 1. Scoring Engine
**System Requirements**:
```
Engine Features:
- Real-time scoring
- Configurable parameters
- Audit trail
- Performance monitoring
- Integration capabilities
- Reporting functions
```

**Integration Points**:
```
System Integration:
- Customer management system
- Loan origination system
- Credit bureau system
- Risk management system
- Compliance system
- Reporting system
```

### 2. Data Management
**Data Requirements**:
```
Data Management:
- Data quality controls
- Data validation
- Data security
- Data retention
- Data backup
- Data recovery
```

## Next Steps

This Credit Scoring Methodology document serves as the foundation for:
1. **System Development** - Credit scoring engine implementation and configuration
2. **Model Validation** - Scoring model testing and validation procedures
3. **Process Implementation** - Credit assessment workflows and procedures
4. **Quality Assurance** - Scoring accuracy monitoring and improvement
5. **Compliance Management** - Regulatory compliance and audit requirements

---

**Document Status**: Ready for Review  
**Next Document**: `risk-assessment-framework.md` - Comprehensive risk assessment framework
