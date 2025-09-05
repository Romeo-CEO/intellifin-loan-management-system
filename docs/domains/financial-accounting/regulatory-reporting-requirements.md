# Regulatory Reporting Requirements - Limelight Moneylink Services

## Document Information
- **Document Type**: Financial Compliance Specification
- **Version**: 1.0
- **Last Updated**: [Date]
- **Owner**: Finance & Compliance Team
- **Compliance**: BoZ Requirements, ZRA Tax Law, IFRS Standards, Audit Requirements
- **Reference**: Chart of Accounts, Transaction Processing Rules, BoZ Guidelines

## Executive Summary

This document defines the comprehensive regulatory reporting requirements for the Limelight Moneylink Services LMS system. It covers Bank of Zambia (BoZ) prudential reporting, Zambia Revenue Authority (ZRA) tax compliance, International Financial Reporting Standards (IFRS) requirements, and audit preparation standards. These requirements ensure full regulatory compliance and operational transparency.

## Business Context

### Why Regulatory Reporting is Critical
- **BoZ Compliance**: Mandatory monthly prudential returns for microfinance institutions
- **Tax Compliance**: ZRA requirements for accurate tax calculations and timely payments
- **Audit Readiness**: Complete financial transparency for internal and external audits
- **Risk Management**: Regulatory oversight for capital adequacy and portfolio quality
- **Operational Continuity**: Compliance failures can result in license suspension

### Regulatory Framework
- **Primary Regulator**: Bank of Zambia (BoZ) - Microfinance Division
- **Tax Authority**: Zambia Revenue Authority (ZRA)
- **Accounting Standards**: International Financial Reporting Standards (IFRS)
- **Audit Requirements**: Annual external audit by licensed auditors
- **Data Protection**: Zambian Data Protection Act compliance

## BoZ Prudential Reporting Requirements

### 1. Monthly Prudential Returns

#### BoZ Directive Schedules
The following reports are based on specific BoZ directive schedules that provide exact templates and data schemas:

**Fourth Schedule**: Classification and Provisioning of Loans - Portfolio Quality Report
**Fifth Schedule**: Capital Adequacy and Risk-Weighted Assets - Capital Adequacy Report  
**Sixth Schedule**: Liquidity Management and Cash Flow - Liquidity Management Report
**Large Loans Exposures Regulations**: Single borrower and concentration risk reporting

### BoZ Directive Schedule Requirements

#### Fourth Schedule - Classification and Provisioning of Loans
**Purpose**: Portfolio Quality Report based on BoZ loan classification requirements
**Data Schema**: Exact column mapping from BoZ Fourth Schedule template

**Required Columns**:
```
Loan Classification Categories:
- Pass (0-59 DPD): Outstanding Balance, Number of Loans, Provision Required (0%)
- Special Mention (60-89 DPD): Outstanding Balance, Number of Loans, Provision Required (5%)
- Substandard (90-179 DPD): Outstanding Balance, Number of Loans, Provision Required (20%)
- Doubtful (180-364 DPD): Outstanding Balance, Number of Loans, Provision Required (50%)
- Loss (365+ DPD): Outstanding Balance, Number of Loans, Provision Required (100%)

Portfolio Summary:
- Total Outstanding Balance
- Total Number of Loans
- Total Provisions Required
- Net Portfolio Value
- Non-Performing Asset Ratio
```

#### Fifth Schedule - Capital Adequacy and Risk-Weighted Assets
**Purpose**: Capital Adequacy Report based on BoZ capital requirements
**Data Schema**: Exact column mapping from BoZ Fifth Schedule template

**Required Columns**:
```
Capital Components:
- Paid-up Share Capital
- Retained Earnings
- General Reserves
- Regulatory Reserves
- Regulatory Loan Loss Reserve
- Total Capital

Risk-Weighted Assets:
- Cash and Cash Equivalents (0% risk weight)
- Government Securities (0% risk weight)
- Loans to Government Employees (20% risk weight)
- SME Loans (100% risk weight)
- Fixed Assets (100% risk weight)
- Total Risk-Weighted Assets

Capital Ratios:
- Total Capital Ratio (Minimum 15%)
- Tier 1 Capital Ratio (Minimum 10%)
- Leverage Ratio (Maximum 33:1)
```

#### Sixth Schedule - Liquidity Management and Cash Flow
**Purpose**: Liquidity Management Report based on BoZ liquidity requirements
**Data Schema**: Exact column mapping from BoZ Sixth Schedule template

**Required Columns**:
```
Liquidity Assets:
- Cash and Cash Equivalents
- Liquid Assets (7 days)
- Liquid Assets (30 days)
- Total Liquid Assets

Liquidity Liabilities:
- Current Liabilities
- Expected Outflows (7 days)
- Expected Outflows (30 days)
- Contingent Liabilities

Liquidity Ratios:
- Liquidity Coverage Ratio (LCR)
- Net Stable Funding Ratio (NSFR)
- Loan-to-Deposit Ratio
- Cash Reserve Ratio
```

#### Large Loans Exposures Regulations
**Purpose**: Single borrower and concentration risk reporting
**Data Schema**: Exact column mapping from BoZ Large Loans template

**Required Columns**:
```
Large Loan Exposures:
- Single Borrower Exposures (>5% of capital)
- Related Party Exposures
- Sector Concentration Limits
- Geographic Concentration Limits
- Top 10 Borrower Exposures

Concentration Risk Metrics:
- Maximum Single Borrower Exposure
- Maximum Sector Exposure
- Maximum Geographic Exposure
- Concentration Risk Ratios
```

#### 1.1 Portfolio Quality Report (PQR)
**Submission Deadline**: 15th of each month
**Reporting Period**: Previous month end
**Format**: BoZ-prescribed Excel template

**Required Data**:
```
Portfolio Breakdown by Product:
- Government Employee Payroll Loans
- SME Asset-Backed Loans
- Total Portfolio Value
- Number of Active Loans
- Average Loan Size
- Portfolio Growth Rate

Risk Classification:
- Standard (0% provisioning)
- Watch (5% provisioning)
- Substandard (25% provisioning)
- Doubtful (50% provisioning)
- Loss (100% provisioning)

Provisioning Summary:
- Total Provisions Required
- Provisions Held
- Provision Coverage Ratio
- Net Portfolio Value
```

**Data Sources**:
- Loan Origination Service
- Collections Service
- Financial Accounting System
- Risk Management Module

#### 1.2 Capital Adequacy Report (CAR)
**Submission Deadline**: 15th of each month
**Reporting Period**: Previous month end
**Format**: BoZ-prescribed Excel template

**Required Data**:
```
Capital Structure:
- Paid-up Share Capital
- Retained Earnings
- General Reserves
- Regulatory Reserves
- Total Capital

Risk-Weighted Assets:
- Cash and Cash Equivalents (0% risk weight)
- Government Securities (0% risk weight)
- Loans to Government Employees (20% risk weight)
- SME Loans (100% risk weight)
- Fixed Assets (100% risk weight)

Capital Ratios:
- Total Capital Ratio (Minimum 15%)
- Tier 1 Capital Ratio (Minimum 10%)
- Leverage Ratio (Maximum 33:1)
```

**Data Sources**:
- Chart of Accounts
- Transaction Processing Rules
- Risk Weighting Module
- Capital Calculation Engine

#### 1.3 Liquidity Management Report (LMR)
**Submission Deadline**: 15th of each month
**Reporting Period**: Previous month end
**Format**: BoZ-prescribed Excel template

**Required Data**:
```
Liquidity Position:
- Cash and Cash Equivalents
- Liquid Assets (7 days)
- Liquid Assets (30 days)
- Total Liquid Assets

Liquidity Requirements:
- Current Liabilities
- Expected Outflows (7 days)
- Expected Outflows (30 days)
- Contingent Liabilities

Liquidity Ratios:
- Liquidity Coverage Ratio (LCR)
- Net Stable Funding Ratio (NSFR)
- Loan-to-Deposit Ratio
- Cash Reserve Ratio
```

**Data Sources**:
- Cash Management Module
- Liability Management System
- Cash Flow Forecasting Engine
- Contingency Planning Module

#### 1.4 Asset Quality Report (AQR)
**Submission Deadline**: 15th of each month
**Reporting Period**: Previous month end
**Format**: BoZ-prescribed Excel template

**Required Data**:
```
Asset Classification:
- Performing Assets
- Non-Performing Assets (NPA)
- NPA Ratio (Maximum 5%)
- Restructured Loans
- Write-offs

Collateral Coverage:
- Secured Loans
- Unsecured Loans
- Collateral Valuation
- Loan-to-Value Ratios
- Collateral Realization

Recovery Performance:
- Collections Efficiency
- Recovery Rates
- Write-off Rates
- Provisioning Adequacy
```

**Data Sources**:
- Loan Portfolio Management
- Collections and Recovery
- Collateral Management
- Risk Assessment Module

### 2. Quarterly Reports

#### 2.1 Quarterly Financial Performance Report
**Submission Deadline**: 45 days after quarter end
**Reporting Period**: Previous quarter
**Format**: BoZ-prescribed Excel template

**Required Data**:
```
Income Statement:
- Interest Income by Product
- Fee Income by Type
- Total Operating Income
- Operating Expenses by Category
- Net Operating Income
- Provisioning Expenses
- Net Income

Balance Sheet:
- Asset Composition
- Liability Structure
- Equity Position
- Capital Adequacy
- Liquidity Position

Key Performance Indicators:
- Return on Assets (ROA)
- Return on Equity (ROE)
- Cost-to-Income Ratio
- Portfolio Yield
- Operating Efficiency
```

#### 2.2 Quarterly Risk Management Report
**Submission Deadline**: 45 days after quarter end
**Reporting Period**: Previous quarter
**Format**: BoZ-prescribed Excel template

**Required Data**:
```
Credit Risk:
- Portfolio Concentration
- Sector Exposure
- Geographic Distribution
- Single Borrower Limits
- Related Party Exposure

Operational Risk:
- Internal Control Assessment
- Fraud Incidents
- System Failures
- Compliance Violations
- Risk Mitigation Measures

Market Risk:
- Interest Rate Risk
- Foreign Exchange Risk
- Liquidity Risk
- Concentration Risk
```

### 3. Annual Reports

#### 3.1 Annual Financial Statements
**Submission Deadline**: 90 days after year end
**Reporting Period**: Previous financial year
**Format**: IFRS-compliant financial statements

**Required Components**:
```
Primary Statements:
- Statement of Financial Position
- Statement of Comprehensive Income
- Statement of Changes in Equity
- Statement of Cash Flows

Notes to Financial Statements:
- Significant Accounting Policies
- Portfolio Disclosures
- Risk Management Disclosures
- Related Party Transactions
- Contingent Liabilities
- Capital Adequacy Disclosures
```

#### 3.2 Annual Compliance Report
**Submission Deadline**: 90 days after year end
**Reporting Period**: Previous financial year
**Format**: BoZ-prescribed template

**Required Data**:
```
Regulatory Compliance:
- BoZ Requirements Compliance
- Capital Adequacy Compliance
- Liquidity Requirements Compliance
- Portfolio Quality Standards
- Provisioning Requirements

Operational Compliance:
- Internal Control Effectiveness
- Risk Management Framework
- Corporate Governance
- Anti-Money Laundering (AML)
- Know Your Customer (KYC)
```

## ZRA Tax Compliance Requirements

### 1. Monthly Tax Returns

#### 1.1 Value Added Tax (VAT) Return
**Submission Deadline**: 25th of each month
**Reporting Period**: Previous month
**Format**: ZRA online portal

**Required Data**:
```
VAT Output Tax:
- Loan Processing Fees (VAT-able)
- Insurance Premiums (VAT-able)
- Communication Services (VAT-able)
- Other VAT-able Services

VAT Input Tax:
- Office Supplies and Services
- Technology Services
- Professional Services
- Transportation Services

VAT Calculation:
- Net VAT Payable
- VAT Refund Claims
- VAT Payment Due
```

**Data Sources**:
- Fee Structure Configuration
- Expense Transaction Records
- VAT Calculation Engine
- Tax Compliance Module

#### 1.2 Pay As You Earn (PAYE) Return
**Submission Deadline**: 10th of each month
**Reporting Period**: Previous month
**Format**: ZRA online portal

**Required Data**:
```
Employee Information:
- Employee Names and PINs
- Gross Salaries
- Taxable Benefits
- Tax Deductions
- Net Pay

Tax Calculations:
- Taxable Income
- Tax Computed
- Tax Credits
- Net Tax Payable
- Tax Payment Due
```

**Data Sources**:
- Human Resources System
- Payroll Processing Module
- Tax Calculation Engine
- Employee Records

### 2. Quarterly Tax Returns

#### 2.1 Corporate Income Tax Return
**Submission Deadline**: 30 days after quarter end
**Reporting Period**: Previous quarter
**Format**: ZRA online portal

**Required Data**:
```
Income Calculation:
- Gross Interest Income
- Gross Fee Income
- Other Operating Income
- Total Gross Income

Expense Deductions:
- Interest Expenses
- Operating Expenses
- Depreciation
- Provisioning Expenses
- Total Deductible Expenses

Tax Calculation:
- Taxable Income
- Tax Computed
- Tax Credits
- Net Tax Payable
- Tax Payment Due
```

**Data Sources**:
- Financial Accounting System
- Chart of Accounts
- Transaction Processing Rules
- Tax Calculation Engine

### 3. Annual Tax Returns

#### 3.1 Annual Corporate Income Tax Return
**Submission Deadline**: 6 months after year end
**Reporting Period**: Previous financial year
**Format**: ZRA online portal

**Required Data**:
```
Comprehensive Income Statement:
- All Revenue Streams
- All Expense Categories
- Depreciation Schedules
- Provisioning Calculations
- Net Taxable Income

Tax Computations:
- Corporate Tax Rate Application
- Tax Credits and Deductions
- Minimum Tax Calculations
- Alternative Minimum Tax
- Final Tax Liability

Supporting Documentation:
- Financial Statements
- Tax Reconciliation
- Supporting Schedules
- Audit Reports
```

## IFRS Compliance Requirements

### 1. Financial Instrument Classification (IFRS 9)

#### 1.1 Loan Portfolio Classification
**Classification Categories**:
```
Amortized Cost:
- Government Employee Payroll Loans
- SME Asset-Backed Loans
- Other Lending Products

Fair Value Through Other Comprehensive Income:
- Investment Securities
- Available-for-Sale Assets

Fair Value Through Profit or Loss:
- Trading Securities
- Derivatives
```

#### 1.2 Expected Credit Loss (ECL) Model
**ECL Calculation Requirements**:
```
Three-Stage Model:
- Stage 1: Performing (12-month ECL)
- Stage 2: Underperforming (Lifetime ECL)
- Stage 3: Non-performing (Lifetime ECL)

ECL Components:
- Probability of Default (PD)
- Loss Given Default (LGD)
- Exposure at Default (EAD)

Forward-Looking Information:
- Economic Indicators
- Industry Trends
- Portfolio Performance
- Regulatory Changes
```

### 2. Revenue Recognition (IFRS 15)

#### 2.1 Loan Revenue Recognition
**Recognition Criteria**:
```
Interest Income:
- Accrual basis over loan term
- Effective interest rate method
- Amortization of fees and costs

Fee Income:
- Performance obligation satisfaction
- Time-based recognition
- Transaction-based recognition

Revenue Measurement:
- Transaction price determination
- Variable consideration
- Significant financing component
```

#### 2.2 Fee Structure Compliance
**Fee Classification**:
```
Recurring Fees (Part of EAR):
- Admin fees
- Management fees
- Insurance fees
- Communication fees

One-time Fees (Not Part of EAR):
- CRB fees
- Company search fees
- Collateral valuation fees
- Application fees
```

### 3. Leases (IFRS 16)

#### 3.1 Operating Lease Recognition
**Lease Accounting Requirements**:
```
Right-of-Use Asset:
- Initial measurement
- Subsequent measurement
- Depreciation calculation

Lease Liability:
- Initial measurement
- Interest calculation
- Principal reduction

Lease Expenses:
- Depreciation expense
- Interest expense
- Variable lease payments
```

## Audit Requirements

### 1. Internal Audit

#### 1.1 Quarterly Internal Audit
**Audit Scope**:
```
Financial Controls:
- Transaction processing accuracy
- Account reconciliation
- Authorization controls
- Segregation of duties

Operational Controls:
- Loan processing procedures
- Collections processes
- Risk management practices
- Compliance monitoring

Technology Controls:
- System access controls
- Data integrity
- Backup and recovery
- Security measures
```

#### 1.2 Annual Internal Audit
**Comprehensive Review**:
```
Full System Review:
- All business processes
- Control environment
- Risk assessment
- Compliance status

Management Review:
- Strategic objectives
- Performance metrics
- Risk mitigation
- Improvement opportunities
```

### 2. External Audit

#### 2.1 Annual External Audit
**Audit Requirements**:
```
Financial Statement Audit:
- Balance sheet verification
- Income statement accuracy
- Cash flow validation
- Disclosure completeness

Compliance Audit:
- Regulatory compliance
- Tax compliance
- Internal control effectiveness
- Risk management adequacy

Management Letter:
- Control weaknesses
- Compliance issues
- Recommendations
- Action plans
```

#### 2.2 Regulatory Audit
**BoZ Audit Requirements**:
```
Prudential Compliance:
- Capital adequacy verification
- Liquidity management
- Portfolio quality assessment
- Risk management framework

Operational Compliance:
- Internal controls
- Corporate governance
- Risk management
- Compliance monitoring
```

## JasperReports Server Configuration

### BoZ Schedule Report Templates
This section provides the primary source for configuring our JasperReports Server with the exact BoZ directive schedules.

#### Report Template Mapping
**Template Configuration for JasperReports Server**:

```
Fourth Schedule - Portfolio Quality Report:
- Report Name: "BoZ_Fourth_Schedule_PQR"
- Data Source: Loan Portfolio Management System
- Template: BoZ Fourth Schedule Excel format
- Columns: Exact mapping to BoZ directive requirements
- Calculations: Automated provisioning calculations per BoZ rates

Fifth Schedule - Capital Adequacy Report:
- Report Name: "BoZ_Fifth_Schedule_CAR"
- Data Source: Chart of Accounts + Risk Weighting Module
- Template: BoZ Fifth Schedule Excel format
- Columns: Exact mapping to BoZ directive requirements
- Calculations: Automated capital ratio calculations

Sixth Schedule - Liquidity Management Report:
- Report Name: "BoZ_Sixth_Schedule_LMR"
- Data Source: Cash Management + Liability Management
- Template: BoZ Sixth Schedule Excel format
- Columns: Exact mapping to BoZ directive requirements
- Calculations: Automated liquidity ratio calculations

Large Loans Exposures Report:
- Report Name: "BoZ_Large_Loans_Exposures"
- Data Source: Loan Portfolio + Concentration Risk Module
- Template: BoZ Large Loans template format
- Columns: Exact mapping to BoZ directive requirements
- Calculations: Automated concentration risk calculations
```

#### Automated Report Generation
**Scheduled Report Execution**:
```
Monthly Reports (15th of each month):
- Fourth Schedule PQR: Automated generation and BoZ submission
- Fifth Schedule CAR: Automated generation and BoZ submission
- Sixth Schedule LMR: Automated generation and BoZ submission
- Large Loans Exposures: Automated generation and BoZ submission

Data Validation:
- Cross-reference validation between reports
- Mathematical accuracy verification
- BoZ template format compliance
- Submission deadline monitoring
```

## Reporting Infrastructure

### 1. Data Collection Systems

#### 1.1 Automated Data Extraction
**Data Sources**:
```
Core Systems:
- Loan Origination Service
- Collections Service
- Financial Accounting System
- Risk Management Module

Integration Points:
- PMEC Integration
- Tingg Payment Gateway
- Credit Bureau Integration
- Banking Systems
```

#### 1.2 Data Validation Rules
**Validation Requirements**:
```
Accuracy Checks:
- Balance reconciliation
- Cross-reference validation
- Mathematical accuracy
- Logical consistency

Completeness Checks:
- Required field completion
- Data coverage verification
- Time period completeness
- Portfolio coverage
```

### 2. Report Generation

#### 2.1 Automated Report Generation
**Report Types**:
```
Scheduled Reports:
- Daily financial summaries
- Weekly portfolio reports
- Monthly regulatory returns
- Quarterly performance reports

Ad Hoc Reports:
- Management requests
- Regulatory inquiries
- Audit support
- Risk assessments
```

#### 2.2 Report Distribution
**Distribution Channels**:
```
Internal Distribution:
- Management team
- Board of directors
- Compliance officers
- Risk managers

External Distribution:
- Bank of Zambia
- Zambia Revenue Authority
- External auditors
- Regulatory bodies
```

## Compliance Monitoring

### 1. Real-time Monitoring

#### 1.1 Key Performance Indicators
**Monitoring Metrics**:
```
Financial Metrics:
- Capital adequacy ratios
- Liquidity ratios
- Portfolio quality metrics
- Profitability indicators

Compliance Metrics:
- Regulatory deadline compliance
- Report accuracy rates
- Audit finding resolution
- Compliance violation tracking
```

#### 1.2 Alert Systems
**Alert Mechanisms**:
```
Threshold Alerts:
- Capital ratio breaches
- Liquidity shortfalls
- Portfolio quality deterioration
- Compliance deadline approaching

Exception Alerts:
- Unusual transactions
- System failures
- Data inconsistencies
- Compliance violations
```

### 2. Periodic Reviews

#### 2.1 Monthly Compliance Review
**Review Process**:
```
Compliance Status:
- Regulatory return status
- Tax payment status
- Audit finding status
- Compliance violation status

Action Items:
- Outstanding compliance issues
- Required corrective actions
- Timeline for resolution
- Responsibility assignment
```

#### 2.2 Quarterly Compliance Assessment
**Assessment Scope**:
```
Comprehensive Review:
- All regulatory requirements
- Compliance framework effectiveness
- Risk assessment updates
- Control environment review

Improvement Planning:
- Process improvements
- System enhancements
- Training requirements
- Resource allocation
```

## Next Steps

This Regulatory Reporting Requirements document serves as the foundation for:
1. **System Configuration** - Automated report generation and compliance monitoring
2. **Process Design** - Regulatory reporting workflows and procedures
3. **Training Programs** - Staff training on compliance requirements
4. **Audit Preparation** - Internal and external audit support

## Document Approval

- **Finance Manager**: [Name] - [Date]
- **Compliance Officer**: [Name] - [Date]
- **Technical Lead**: [Name] - [Date]
- **CEO**: [Name] - [Date]

---

**Document Control**: This document must be reviewed and updated quarterly or when regulatory requirements change.
