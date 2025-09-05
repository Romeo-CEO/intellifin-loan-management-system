# Chart of Accounts - Limelight Moneylink Services

## Document Information
- **Document Type**: Financial System Specification
- **Version**: 1.0
- **Last Updated**: [Date]
- **Owner**: Finance & Compliance Team
- **Compliance**: BoZ Requirements, IFRS Standards, Zambian Tax Law
- **Reference**: BoZ Prudential Guidelines, Money Lenders Act

## Executive Summary

This document defines the comprehensive Chart of Accounts (CoA) for the Limelight Moneylink Services LMS system. The CoA is designed to comply with Bank of Zambia (BoZ) requirements, International Financial Reporting Standards (IFRS), and Zambian tax regulations while supporting our dual-product lending strategy and regulatory reporting needs.

## Business Context

### Why Chart of Accounts is Critical
- **Regulatory Compliance**: BoZ requires specific account classifications for microfinance institutions
- **Financial Control**: Proper account structure enables accurate financial reporting and audit trails
- **Tax Compliance**: ZRA requirements for proper income and expense categorization
- **Risk Management**: Asset classification for capital adequacy and provisioning requirements
- **Operational Efficiency**: Streamlined accounting processes for loan operations

### CoA Design Principles
- **BoZ Compliance**: Follows BoZ microfinance institution account structure requirements
- **IFRS Alignment**: Aligns with International Financial Reporting Standards
- **Operational Clarity**: Clear separation between loan products and operational functions
- **Scalability**: Supports growth from startup to established microfinance institution
- **Audit Readiness**: Complete audit trail and reconciliation capabilities

## Account Structure Overview

### Account Numbering System
```
1XXXX - Assets
2XXXX - Liabilities  
3XXXX - Equity
4XXXX - Revenue
5XXXX - Expenses
6XXXX - Off-Balance Sheet Items
```

### Account Categories
1. **Assets (1XXXX)**: What we own and what is owed to us
2. **Liabilities (2XXXX)**: What we owe to others
3. **Equity (3XXXX)**: Owner's investment and retained earnings
4. **Revenue (4XXXX)**: Income from our lending activities
5. **Expenses (5XXXX)**: Costs of operating our business
6. **Off-Balance Sheet (6XXXX)**: Contingent liabilities and commitments

## Detailed Account Structure

### 1. Assets (1XXXX)

#### 1.1 Cash and Cash Equivalents (11000-11999)
```
11000 - Cash on Hand (Petty Cash)
11001 - Bank Account - Main Operating
11002 - Bank Account - Reserve (BoZ Requirement)
11003 - Bank Account - Collections
11004 - Mobile Money - Tingg Gateway
11005 - Mobile Money - Other Providers
11099 - Cash and Cash Equivalents - Total
```

#### 1.2 Loans and Advances (12000-12999)
```
12000 - Government Employee Payroll Loans
12001 - SME Asset-Backed Loans
12002 - Personal Loans (Future Product)
12003 - Emergency Loans (Future Product)
12010 - Interest Receivable - Current
12011 - Interest Receivable - Past Due
12020 - Fees Receivable - Current
12021 - Fees Receivable - Past Due
12030 - Penalty Interest Receivable
12099 - Loans and Advances - Total
```

#### 1.3 Allowance for Credit Losses (13000-13999)
```
13000 - Allowance for Credit Losses - Government Loans
13001 - Allowance for Credit Losses - SME Loans
13002 - Allowance for Credit Losses - Personal Loans
13099 - Allowance for Credit Losses - Total
```

#### 1.4 Fixed Assets (14000-14999)
```
14000 - Office Equipment
14001 - Computer Systems
14002 - Furniture and Fixtures
14003 - Motor Vehicles
14004 - Office Buildings (if owned)
14010 - Accumulated Depreciation - Equipment
14011 - Accumulated Depreciation - Vehicles
14012 - Accumulated Depreciation - Buildings
14099 - Fixed Assets - Net
```

#### 1.5 Other Assets (15000-15999)
```
15000 - Prepaid Expenses
15001 - Prepaid Insurance
15002 - Prepaid Rent
15003 - Security Deposits
15004 - Deferred Tax Assets
15099 - Other Assets - Total
```

### 2. Liabilities (2XXXX)

#### 2.1 Borrowings (21000-21999)
```
21000 - Bank Borrowings - Short Term
21001 - Bank Borrowings - Long Term
21002 - Microfinance Fund Borrowings
21003 - Other Institutional Borrowings
21099 - Borrowings - Total
```

#### 2.2 Deposits and Savings (22000-22999)
```
22000 - Customer Deposits - Demand
22001 - Customer Deposits - Time
22002 - Customer Savings Accounts
22099 - Deposits and Savings - Total
```

#### 2.3 Accrued Liabilities (23000-23999)
```
23000 - Accrued Interest Payable
23001 - Accrued Salaries and Wages
23002 - Accrued Taxes
23003 - Accrued Utilities
23004 - Accrued Professional Fees
23099 - Accrued Liabilities - Total
```

#### 2.4 Other Liabilities (24000-24999)
```
24000 - Deferred Revenue
24001 - Customer Refund Liabilities
24002 - Deferred Tax Liabilities
24099 - Other Liabilities - Total
```

### 3. Equity (3XXXX)

#### 3.1 Share Capital (31000-31999)
```
31000 - Ordinary Shares
31001 - Preference Shares (if applicable)
31099 - Share Capital - Total
```

#### 3.2 Retained Earnings (32000-32999)
```
32000 - Retained Earnings - Current Year
32001 - Retained Earnings - Prior Years
32099 - Retained Earnings - Total
```

#### 3.3 Reserves (33000-33999)
```
33000 - General Reserve
33001 - Regulatory Reserve (BoZ Requirement)
33002 - Regulatory Loan Loss Reserve
33003 - Capital Reserve
33099 - Reserves - Total
```

### 4. Revenue (4XXXX)

#### 4.1 Interest Income (41000-41999)
```
41000 - Interest Income - Government Loans
41001 - Interest Income - SME Loans
41002 - Interest Income - Personal Loans
41003 - Interest Income - Emergency Loans
41010 - Interest Income - Past Due Loans
41099 - Interest Income - Total
```

#### 4.2 Fee Income (42000-42999)
```
42000 - Admin Fee Income
42001 - Management Fee Income
42002 - Insurance Fee Income
42003 - Communication Fee Income
42004 - CRB Fee Income
42005 - Company Search Fee Income
42006 - Late Payment Fee Income
42007 - Early Repayment Fee Income
42099 - Fee Income - Total
```

#### 4.3 Other Income (43000-43999)
```
43000 - Investment Income
43001 - Foreign Exchange Gains
43002 - Miscellaneous Income
43099 - Other Income - Total
```

### 5. Expenses (5XXXX)

#### 5.1 Interest Expense (51000-51999)
```
51000 - Interest Expense - Bank Borrowings
51001 - Interest Expense - Microfinance Fund
51002 - Interest Expense - Other Borrowings
51099 - Interest Expense - Total
```

#### 5.2 Personnel Expenses (52000-52999)
```
52000 - Salaries and Wages
52001 - Employee Benefits
52002 - Training and Development
52003 - Recruitment Costs
52099 - Personnel Expenses - Total
```

#### 5.3 Operational Expenses (53000-53999)
```
53000 - Rent and Utilities
53001 - Office Supplies
53002 - Communication Costs
53003 - Insurance Premiums
53004 - Professional Fees
53005 - Legal and Compliance
53006 - Audit Fees
53099 - Operational Expenses - Total
```

#### 5.4 Technology Expenses (54000-54999)
```
54000 - Software Licenses
54001 - IT Support and Maintenance
54002 - Data and Internet Services
54003 - Mobile Money Gateway Fees
54004 - Credit Bureau Integration Fees
54099 - Technology Expenses - Total
```

#### 5.5 Credit Losses (55000-55999)
```
55000 - Provision for Credit Losses
55001 - Write-off of Bad Debts
55099 - Credit Losses - Total
```

#### 5.6 Other Expenses (56000-56999)
```
56000 - Marketing and Advertising
56001 - Travel and Entertainment
56002 - Bank Charges
56003 - Regulatory Fees
56099 - Other Expenses - Total
```

### 6. Off-Balance Sheet Items (6XXXX)

#### 6.1 Contingent Liabilities (61000-61999)
```
61000 - Guarantees Issued
61001 - Letters of Credit
61002 - Legal Contingencies
61099 - Contingent Liabilities - Total
```

#### 6.2 Commitments (62000-62999)
```
62000 - Loan Commitments - Undrawn
62001 - Capital Expenditure Commitments
62099 - Commitments - Total
```

## Regulatory Compliance Requirements

### BoZ Prudential Guidelines
- **Asset Classification**: Proper categorization for risk weighting
- **Provisioning**: Specific accounts for credit loss allowances
- **Capital Adequacy**: Clear equity and reserve classifications
- **Liquidity Management**: Cash and near-cash asset tracking

### ZRA Tax Requirements
- **Income Recognition**: Proper revenue categorization for tax purposes
- **Expense Deductibility**: Clear expense classification for tax deductions
- **VAT Compliance**: Proper tracking of VAT-able transactions
- **Tax Provisioning**: Accurate tax liability calculations

### IFRS Standards
- **Revenue Recognition**: IFRS 15 compliance for loan income
- **Financial Instruments**: IFRS 9 compliance for loan classification
- **Leases**: IFRS 16 compliance for office and equipment leases
- **Impairment**: IFRS 9 expected credit loss model

## Operational Considerations

### Loan Product Tracking
- **Separate Accounts**: Each loan product has dedicated asset and income accounts
- **Portfolio Management**: Clear visibility into product performance
- **Risk Assessment**: Proper classification for provisioning calculations
- **Regulatory Reporting**: BoZ-required portfolio breakdowns

### Fee Structure Support
- **Configurable Fees**: System supports dynamic fee configurations
- **Fee Reconciliation**: Clear tracking of fee income by type
- **Regulatory Compliance**: Fees properly categorized for EAR calculations
- **Customer Transparency**: Clear fee breakdown for customer statements

### Integration Support
- **PMEC Integration**: Dedicated accounts for government payroll deductions
- **Tingg Gateway**: Mobile money transaction tracking
- **Credit Bureau**: CRB fee and service cost tracking
- **Banking Systems**: Clear reconciliation with bank statements

## System Implementation

### Account Creation Rules
1. **System-Generated**: Core accounts created during system setup
2. **Configurable**: Fee accounts can be added/modified by administrators
3. **Validation**: Account numbers must follow established numbering system
4. **Audit Trail**: All account modifications logged with approval trail

### Account Maintenance
1. **Regular Review**: Quarterly review of account structure
2. **Regulatory Updates**: Prompt updates for BoZ requirement changes
3. **Performance Monitoring**: Account usage and balance monitoring
4. **Reconciliation**: Monthly account balance reconciliation

### Reporting Requirements
1. **BoZ Reports**: Monthly prudential returns
2. **Management Reports**: Daily, weekly, monthly financial summaries
3. **Tax Reports**: Quarterly and annual tax returns
4. **Audit Reports**: Annual audit and compliance reports

## Next Steps

This Chart of Accounts serves as the foundation for:
1. **General Ledger Configuration** - System setup and account mapping
2. **Transaction Processing Rules** - How transactions are posted
3. **Financial Reporting Templates** - Report structure and calculations
4. **Compliance Monitoring** - Regulatory requirement tracking

## Document Approval

- **Finance Manager**: [Name] - [Date]
- **Compliance Officer**: [Name] - [Date]
- **Technical Lead**: [Name] - [Date]
- **CEO**: [Name] - [Date]

---

**Document Control**: This document must be reviewed and updated quarterly or when regulatory requirements change.
