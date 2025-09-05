# Transaction Processing Rules - Limelight Moneylink Services

## Document Information
- **Document Type**: Financial System Specification
- **Version**: 1.0
- **Last Updated**: [Date]
- **Owner**: Finance & Technical Teams
- **Compliance**: BoZ Requirements, IFRS Standards, Audit Requirements
- **Reference**: Chart of Accounts, Loan Product Catalog, PMEC Integration

## Executive Summary

This document defines the comprehensive transaction processing rules for the Limelight Moneylink Services LMS system. These rules ensure that all financial transactions are posted correctly to the general ledger, maintaining data integrity, regulatory compliance, and providing accurate financial reporting for both operational and compliance purposes.

## Business Context

### Why Transaction Processing Rules are Critical
- **Data Integrity**: Ensures accurate financial records and prevents accounting errors
- **Regulatory Compliance**: BoZ requires proper transaction classification and posting
- **Audit Trail**: Complete transaction history for regulatory audits and internal reviews
- **Financial Control**: Prevents unauthorized transactions and ensures proper approvals
- **Operational Efficiency**: Automated posting reduces manual errors and processing time

### Core Principles
- **Double-Entry Accounting**: Every transaction affects at least two accounts
- **Audit Trail**: Complete tracking of who, what, when, and why for every transaction
- **Validation**: Multiple levels of validation before transaction posting
- **Reversibility**: Ability to reverse or correct erroneous transactions
- **Compliance**: All transactions must meet regulatory and internal policy requirements

## Transaction Types and Rules

### 1. Loan Origination Transactions

#### 1.1 New Loan Disbursement
**Trigger**: Loan approval and disbursement authorization
**Transaction Type**: Asset increase (Loan), Asset decrease (Cash)

**Posting Rules**:
```
DR 12000 - Government Employee Payroll Loans (or 12001 - SME Asset-Backed Loans)
CR 11001 - Bank Account - Main Operating (or 11004 - Mobile Money - Tingg Gateway)
```

**Validation Rules**:
- Loan amount must be within approved limits
- Customer must have valid credit assessment
- PMEC registration must be confirmed (for government loans)
- Collateral valuation must be completed (for SME loans)
- CEO authorization required for offline disbursements

**Required Metadata**:
- Loan reference number
- Customer ID
- Loan product type
- Disbursement date
- Authorized by (user ID)
- Approval workflow reference

#### 1.2 Loan Origination Fees
**Trigger**: Loan disbursement
**Transaction Type**: Asset increase (Fees Receivable), Revenue recognition

**Posting Rules**:
```
DR 12020 - Fees Receivable - Current
CR 42000 - Admin Fee Income
CR 42001 - Management Fee Income
CR 42002 - Insurance Fee Income
CR 42003 - Communication Fee Income
CR 42004 - CRB Fee Income
CR 42005 - Company Search Fee Income (SME loans only)
```

**Validation Rules**:
- Fee amounts must match product configuration
- Total fees must not exceed regulatory limits
- EAR calculation must remain under 48% cap
- Fee structure must be approved by product configuration

### 2. Interest Accrual Transactions

#### 2.1 Monthly Interest Accrual
**Trigger**: End of month processing
**Transaction Type**: Asset increase (Interest Receivable), Revenue recognition

**Posting Rules**:
```
DR 12010 - Interest Receivable - Current
CR 41000 - Interest Income - Government Loans (or 41001 - SME Loans)
```

**Validation Rules**:
- Interest rate must match product configuration
- Calculation method must be valid (Reducing Balance or Flat Rate)
- EAR must remain under 48% compliance cap
- Accrual must be based on outstanding principal balance

#### 2.2 Interest Calculation Methods
**Reducing Balance Method**:
- Interest calculated on outstanding principal for each period
- Principal reduction affects subsequent interest calculations
- Recommended for compliance and transparency

**Flat Rate Method**:
- Interest calculated on initial principal for entire term
- System displays compliance warning
- Requires legal counsel consultation note

### 3. Fee Accrual Transactions

#### 3.1 Monthly Fee Accrual
**Trigger**: End of month processing
**Transaction Type**: Asset increase (Fees Receivable), Revenue recognition

**Posting Rules**:
```
DR 12020 - Fees Receivable - Current
CR 42000 - Admin Fee Income
CR 42001 - Management Fee Income
CR 42002 - Insurance Fee Income
CR 42003 - Communication Fee Income
```

**Validation Rules**:
- Fee amounts must match product configuration
- Fees must be recurring (not one-time)
- Total cost must remain within EAR compliance limits
- Fee structure must be approved and active

### 4. Payment Collection Transactions

#### 4.1 PMEC Payroll Deduction
**Trigger**: PMEC deduction confirmation
**Transaction Type**: Asset increase (Cash), Asset decrease (Receivables)

**Posting Rules**:
```
DR 11001 - Bank Account - Main Operating
CR 12010 - Interest Receivable - Current
CR 12020 - Fees Receivable - Current
CR 12000 - Government Employee Payroll Loans (principal portion)
```

**Validation Rules**:
- PMEC confirmation must be received
- Deduction amount must match expected payment
- Payment must be allocated according to Reducing Balance rules
- Partial payments must be handled according to policy

**Payment Allocation Priority**:

For loans in 'Accrual' status:
1. Penalty interest (if any)
2. Current interest
3. Current fees
4. Principal reduction

For loans in 'Non-Accrual' status (BoZ Directive Section 11):
1. Principal (until fully recovered)
2. Any excess may then be recognized as income

#### 4.2 Manual Payment Collection
**Trigger**: Customer payment (cash, bank transfer, mobile money)
**Transaction Type**: Asset increase (Cash), Asset decrease (Receivables)

**Posting Rules**:
```
DR 11001 - Bank Account - Main Operating (or 11004 - Mobile Money - Tingg Gateway)
CR 12010 - Interest Receivable - Current
CR 12020 - Fees Receivable - Current
CR 12000 - Government Employee Payroll Loans (or 12001 - SME Asset-Backed Loans)
```

**Validation Rules**:
- Payment must be verified and confirmed
- Receipt must be generated and recorded
- Payment allocation must follow established rules
- Offline payments require dual authorization

### 5. Credit Loss and Provisioning Transactions

#### 5.1 Provision for Credit Losses
**Trigger**: Monthly provisioning calculation
**Transaction Type**: Asset decrease (Allowance), Expense recognition

**Posting Rules**:
```
DR 55000 - Provision for Credit Losses
CR 13000 - Allowance for Credit Losses - Government Loans
CR 13001 - Allowance for Credit Losses - SME Loans
```

**Validation Rules**:
- Provisioning must follow BoZ guidelines
- Risk classification must be current and accurate
- Provisioning rates must be approved by management
- Calculations must be auditable and documented

#### 5.2 Write-off of Bad Debts
**Trigger**: Loan classification as loss or write-off approval
**Transaction Type**: Asset decrease (Loan), Asset decrease (Allowance)

**Posting Rules**:
```
DR 13000 - Allowance for Credit Losses - Government Loans (or 13001 - SME Loans)
CR 12000 - Government Employee Payroll Loans (or 12001 - SME Asset-Backed Loans)
```

**Validation Rules**:
- Write-off must be approved by authorized personnel
- All collection efforts must be exhausted
- Legal documentation must be complete
- Write-off must be reported to BoZ

### 6. Operational Expense Transactions

#### 6.1 Salary and Wage Payments
**Trigger**: Monthly payroll processing
**Transaction Type**: Asset decrease (Cash), Liability decrease (Accrued Salaries)

**Posting Rules**:
```
DR 52000 - Salaries and Wages
DR 23001 - Accrued Salaries and Wages
CR 11001 - Bank Account - Main Operating
```

**Validation Rules**:
- Payroll must be approved by management
- Tax calculations must be accurate
- Benefits must be properly allocated
- Payment must be within budget limits

#### 6.2 Technology and Integration Fees
**Trigger**: Monthly billing or usage
**Transaction Type**: Asset decrease (Cash), Expense recognition

**Posting Rules**:
```
DR 54003 - Mobile Money Gateway Fees
DR 54004 - Credit Bureau Integration Fees
DR 54000 - Software Licenses
CR 11001 - Bank Account - Main Operating
```

**Validation Rules**:
- Fees must be verified against service agreements
- Usage must be within contracted limits
- Billing must be accurate and documented
- Payment must be approved by authorized personnel

### 7. Offline Operations Transactions

#### 7.1 Offline Loan Disbursement
**Trigger**: CEO authorization for offline mode
**Transaction Type**: Asset increase (Loan), Asset decrease (Cash)

**Posting Rules**:
```
DR 12000 - Government Employee Payroll Loans (or 12001 - SME Asset-Backed Loans)
CR 11000 - Cash on Hand
```

**Validation Rules**:
- CEO authorization must be active
- Risk limits must not be exceeded
- Voucher system must be properly documented
- Dual control must be maintained

#### 7.2 Offline Payment Collection
**Trigger**: Manual payment collection in offline mode
**Transaction Type**: Asset increase (Cash), Asset decrease (Receivables)

**Posting Rules**:
```
DR 11000 - Cash on Hand
CR 12010 - Interest Receivable - Current
CR 12020 - Fees Receivable - Current
CR 12000 - Government Employee Payroll Loans (or 12001 - SME Asset-Backed Loans)
```

**Validation Rules**:
- Offline mode must be authorized
- Payment must be verified by dual control
- Receipt must be properly documented
- Reconciliation must be performed when online

## Transaction Validation and Controls

### Pre-Posting Validation
1. **Business Rule Validation**: Transaction must meet business logic requirements
2. **Regulatory Validation**: Transaction must comply with BoZ and other regulations
3. **Approval Validation**: Transaction must have proper authorization
4. **Data Validation**: All required fields must be complete and accurate
5. **Balance Validation**: Account balances must be sufficient for debits

### Post-Posting Controls
1. **Audit Trail**: Complete transaction history with user and timestamp
2. **Reconciliation**: Daily reconciliation of cash and bank accounts
3. **Exception Reporting**: Automated alerts for unusual transactions
4. **Access Controls**: Role-based access to transaction functions
5. **Backup and Recovery**: Transaction data backup and recovery procedures

### Error Handling and Correction
1. **Validation Errors**: Transactions rejected with detailed error messages
2. **System Errors**: Failed transactions logged and reported
3. **Manual Corrections**: Authorized personnel can reverse/correct transactions
4. **Audit Trail**: All corrections logged with approval trail
5. **Reconciliation**: Corrected transactions must reconcile with supporting documents

## Automated End-of-Day & End-of-Month Processes

### Overview
These automated processes are mandated by BoZ directives and must be executed by the system to maintain regulatory compliance. These processes ensure proper loan classification, provisioning, and non-accrual status management.

### Daily Automated Processes

#### Daily Arrears Calculation
**Trigger**: Automated nightly job (runs at 11:59 PM daily)
**Purpose**: Calculate Days Past Due (DPD) for all active loans

**System Actions**:
```
For every active loan:
1. Calculate Days Past Due (DPD) based on last payment date
2. Update loan record with current DPD value
3. Log calculation results in audit trail
4. Generate alerts for loans approaching classification thresholds
```

**Validation Rules**:
- DPD calculation must be based on actual payment dates
- Grace periods must be properly accounted for
- Calculation must be auditable and traceable
- Results must be logged with timestamp and system user

#### Loan Classification Update
**Trigger**: Automated nightly job (runs after arrears calculation)
**Purpose**: Update loan classification based on BoZ DPD buckets

**Classification Rules** (per BoZ directives):
```
Pass: DPD = 0-59 days
Special Mention: DPD = 60-89 days
Substandard: DPD = 90-179 days
Doubtful: DPD = 180-364 days
Loss: DPD = 365+ days
```

**System Actions**:
```
For every active loan:
1. Determine new classification based on current DPD
2. Update LoanClassification field if changed
3. Log classification change in audit trail
4. Trigger non-accrual status update if applicable
5. Generate management alerts for downgrades
```

**Posting Rule**: No direct financial transaction occurs, but this classification change is a critical, auditable event that affects subsequent provisioning calculations.

#### Non-Accrual Status Update
**Trigger**: When a loan's classification changes to 'Substandard' or worse (>90 DPD)
**Purpose**: Set loan to non-accrual status and reverse accrued interest

**System Actions**:
```
When classification changes to Substandard, Doubtful, or Loss:
1. Set AccrualStatus flag to 'Non-Accrual'
2. Calculate total accrued but unpaid interest
3. Generate interest reversal journal entry
4. Log non-accrual status change in audit trail
5. Notify management of status change
```

**Posting Rule** (Interest Reversal):
As per Section 10(b) of BoZ directives, the system must generate a journal entry to reverse all previously accrued, but unpaid interest from the income statement:

```
DR 4100X - Interest Income (appropriate product account)
CR 12011 - Interest Receivable - Past Due
```

**Validation Rules**:
- Interest reversal must be calculated accurately
- Reversal must be posted to correct income account
- Non-accrual status must be clearly documented
- Audit trail must include calculation details

### Monthly Automated Processes

#### Loan Loss Provisioning Calculation
**Trigger**: Automated end-of-month job (runs on last business day of month)
**Purpose**: Calculate required loan loss provisions based on BoZ minimum percentages

**Provisioning Rates** (per BoZ Second Schedule):
```
Pass: 0% (no provision required)
Special Mention: 5% of outstanding balance
Substandard: 20% of outstanding balance
Doubtful: 50% of outstanding balance
Loss: 100% of outstanding balance
```

**System Actions**:
```
For every active loan:
1. Determine current loan classification
2. Calculate required provision based on classification
3. Apply provision rate to outstanding balance (net of collateral)
4. Calculate change in required provision from previous month
5. Generate provisioning journal entry if change is required
6. Update loan record with new provision amount
7. Log all calculations in audit trail
```

**Posting Rule**:
The system creates a journal entry for the change in the required provision for the month:

```
DR 55000 - Provision for Credit Losses
CR 1300X - Allowance for Credit Losses (appropriate product account)
```

**Validation Rules**:
- Provisioning must be based on current classification
- Collateral values must be properly netted
- Calculations must be auditable and documented
- Changes must be properly authorized and logged

#### Regulatory Loan Loss Reserve Update
**Trigger**: Monthly provisioning calculation completion
**Purpose**: Update the non-distributable regulatory loan loss reserve

**System Actions**:
```
1. Calculate total required loan loss provisions
2. Compare with existing regulatory reserve balance
3. Transfer excess provisions to regulatory reserve
4. Generate reserve transfer journal entry if required
5. Log reserve activity in audit trail
```

**Posting Rule**:
```
DR 55000 - Provision for Credit Losses
CR 33002 - Regulatory Loan Loss Reserve
```

### Process Monitoring and Controls

#### Automated Process Validation
1. **Completion Verification**: All processes must complete successfully
2. **Data Integrity Checks**: Results must be validated against business rules
3. **Exception Handling**: Failed processes must be logged and reported
4. **Audit Trail**: Complete logging of all automated activities
5. **Performance Monitoring**: Process execution times and resource usage

#### Management Reporting
1. **Daily Classification Report**: Summary of loan classification changes
2. **Monthly Provisioning Report**: Detailed provisioning calculations and changes
3. **Exception Reports**: Failed processes and data quality issues
4. **Compliance Reports**: Regulatory requirement adherence status
5. **Performance Reports**: Process execution metrics and trends

#### Error Handling and Recovery
1. **Process Failures**: Automatic retry with exponential backoff
2. **Data Validation Errors**: Detailed error logging and management notification
3. **System Errors**: Automatic rollback and error reporting
4. **Manual Intervention**: Procedures for manual process completion
5. **Audit Trail**: Complete documentation of all error handling activities

## Integration Transaction Rules

### PMEC Integration
1. **Deduction Registration**: Loan registration creates receivable accounts
2. **Payment Confirmation**: PMEC confirmation triggers payment posting
3. **Reconciliation**: Monthly reconciliation with PMEC reports
4. **Error Handling**: Failed deductions handled through collections process

### Tingg Payment Gateway
1. **Disbursement**: Mobile money disbursement to customer accounts
2. **Collection**: Direct debit from customer mobile money accounts
3. **Fee Reconciliation**: Gateway fees posted to technology expense accounts
4. **Settlement**: Daily settlement with Tingg for net positions

### Credit Bureau Integration
1. **CRB Fees**: Customer fees posted to fee income accounts
2. **Service Costs**: Integration costs posted to technology expense accounts
3. **Data Exchange**: Credit data exchange logged for audit purposes
4. **Compliance**: All exchanges logged for regulatory reporting

## Reporting and Reconciliation

### Daily Reconciliation
1. **Cash Reconciliation**: Cash on hand vs. system records
2. **Bank Reconciliation**: Bank statements vs. system records
3. **Mobile Money Reconciliation**: Gateway balances vs. system records
4. **Exception Reporting**: Unreconciled items reported to management

### Monthly Reconciliation
1. **Account Balance Review**: All account balances reviewed and verified
2. **Provisioning Review**: Credit loss provisions reviewed and adjusted
3. **Interest Accrual Review**: Interest accruals verified and adjusted
4. **Fee Accrual Review**: Fee accruals verified and adjusted

### Regulatory Reporting
1. **BoZ Monthly Returns**: Portfolio and financial position reports
2. **Tax Returns**: Monthly and quarterly tax calculations and payments
3. **Audit Reports**: Annual audit preparation and support
4. **Compliance Reports**: Regulatory compliance status reports

## Next Steps

This Transaction Processing Rules document serves as the foundation for:
1. **System Configuration** - Transaction posting rules and validation
2. **User Training** - Staff training on transaction processing
3. **Audit Procedures** - Internal and external audit support
4. **Compliance Monitoring** - Regulatory requirement tracking

## Document Approval

- **Finance Manager**: [Name] - [Date]
- **Technical Lead**: [Name] - [Date]
- **Compliance Officer**: [Name] - [Date]
- **CEO**: [Name] - [Date]

---

**Document Control**: This document must be reviewed and updated quarterly or when business processes change.
