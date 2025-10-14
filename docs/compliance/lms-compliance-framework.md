# LMS Compliance Framework

## Executive Summary

The Loan Management System (LMS) is built with a "Compliance by Design" philosophy, ensuring that regulatory requirements are embedded directly into the system's architecture and features. This framework demonstrates how our system directly addresses the key requirements of:

- **Money Lenders Act** - Interest rate caps, prohibited charges, and compound interest restrictions
- **Bank of Zambia (BoZ) Prudential Guidelines** - Loan classification, provisioning, and capital adequacy
- **Standard Audit Practices** - Complete audit trails, data integrity, and regulatory reporting

This document serves as a definitive blueprint, explicitly linking each regulatory requirement to specific system features and functions, providing clear guidance for developers and demonstrating compliance to auditors and regulators.

---

## 1. Compliance with the Money Lenders Act

### Requirement: Interest Rate Cap (Section 15)
**Regulatory Mandate**: The effective annual rate of interest charged on any loan shall not exceed 48% per annum.

**How Our System Complies**:
- The **Product Engine** includes a built-in Effective Annual Rate (EAR) calculator that considers all recurring cost components:
  - Interest rates (nominal and effective)
  - Administrative fees
  - Processing fees
  - Insurance premiums
  - Any other recurring charges
- The calculator supports both **Reducing Balance** and **Flat Rate** calculation methods
- The system implements a **non-bypassable hard stop** that physically prevents saving any loan product where the calculated EAR exceeds 48.00%
- Real-time validation occurs during product configuration and loan origination
- Audit logs capture all EAR calculations for regulatory review

### Requirement: Prohibition of Certain Charges (Section 17)
**Regulatory Mandate**: Prohibition of compound interest and certain types of charges that increase the cost of credit beyond the stated interest rate.

**How Our System Complies**:
- The system allows configuration of various recurring fee components to match market practice, but all are treated as part of the total cost of credit for EAR calculation
- Only specific, **one-time, third-party pass-through fees** are permitted to be charged separately:
  - Credit Reference Bureau (CRB) fees
  - Property valuation fees
  - Legal documentation fees
  - Government registration fees
- These pass-through fees must be:
  - Deducted from loan disbursement amount
  - Clearly documented in loan agreements
  - Excluded from EAR calculations
  - Supported by third-party invoices/receipts

### Requirement: Prohibition of Compound Interest (Section 10)
**Regulatory Mandate**: Interest shall not be compounded except as explicitly permitted by the Act.

**How Our System Complies**:
- The **Calculation Engine** is architected to apply only **simple interest** on defaulted installments, as explicitly permitted by the Act
- Core interest calculation on the loan principal does not compound interest
- Default interest is calculated as simple interest on overdue amounts only
- System prevents any configuration that would result in compound interest calculations
- All interest calculations are logged and auditable

---

## 2. Compliance with BoZ Prudential Guidelines

### Requirement: Loan Classification & Provisioning
**Regulatory Mandate**: Loans must be classified according to risk categories and appropriate provisions made for potential losses.

**How Our System Complies**:
- **Automated Classification System**:
  - Nightly system job calculates Days Past Due (DPD) for every loan
  - Automatic re-classification according to BoZ-mandated buckets:
    - **Current**: 0-29 DPD
    - **Special Mention**: 30-89 DPD
    - **Substandard**: 90-179 DPD
    - **Doubtful**: 180-365 DPD
    - **Loss**: 365+ DPD
- **Automated Provisioning**:
  - End-of-month job calculates required loan loss provisions based on classifications
  - Provision rates: Special Mention (1%), Substandard (25%), Doubtful (50%), Loss (100%)
  - Automatic posting of provision entries to General Ledger
  - Real-time provision tracking and reporting

### Requirement: Non-Accrual Status
**Regulatory Mandate**: Interest income must not be accrued on loans classified as non-performing.

**How Our System Complies**:
- **Automatic Non-Accrual Trigger**: When system classifies a loan as Substandard (90+ DPD), it simultaneously sets loan status to 'Non-Accrual'
- **General Ledger Service Integration**:
  - Hard-coded logic prevents posting interest income for any loan in Non-Accrual status
  - Automatic reversal of previously accrued but unpaid interest
  - Interest income recognition resumes only when loan returns to performing status
- **Audit Trail**: All non-accrual status changes are logged with timestamps and reasons

### Requirement: Capital Adequacy & Large Exposures Reporting
**Regulatory Mandate**: Institutions must monitor and report large exposures and maintain adequate capital ratios.

**How Our System Complies**:
- **Regulatory Capital Module**:
  - Configurable module for institution's regulatory capital base
  - Real-time capital ratio calculations
  - Integration with loan portfolio for risk-weighted asset calculations
- **Large Exposures Reporting**:
  - Dedicated JasperReports report matching official "Large Loans Exposures Regulations" schedule
  - Automatic identification of exposures exceeding regulatory thresholds:
    - 10% of capital (warning threshold)
    - 25% of capital (maximum threshold)
  - Real-time monitoring and alerting for threshold breaches
  - Automated regulatory report generation

---

## 3. Core Audit & Data Integrity Framework

### Requirement: Complete & Immutable Audit Trail
**Regulatory Mandate**: Complete audit trail of all system activities for regulatory compliance and forensic analysis.

**How Our System Complies**:
- **Append-Only Audit Log Architecture**:
  - Dedicated `AuditEvents` table records every state-changing action
  - Each log entry contains comprehensive information:
    - **Who**: User ID, role, session information
    - **What**: Action performed, business object affected
    - **When**: Precise timestamp with timezone
    - **Where**: IP address, device information, location
    - **Before/After**: Complete state change documentation
- **Database Security**:
  - Database permissions prevent any application service from updating or deleting audit logs
  - Audit logs are stored in separate, read-only database schema
  - Cryptographic hashing ensures data integrity
- **Comprehensive Coverage**:
  - All financial transactions
  - User authentication and authorization events
  - System configuration changes
  - Data access and modification events
  - Integration events with external systems

### Requirement: Data Retention
**Regulatory Mandate**: Financial institutions must retain records for specified periods as per various regulatory requirements.

**How Our System Complies**:
- **Mandatory 10-Year Retention Policy**:
  - Enforced for all transactional and audit data
  - Aligns with strictest requirements of Credit Reporting Act and AML guidelines
  - Automated retention management with configurable policies
- **Data Lifecycle Management**:
  - Automated archival processes for older data
  - Secure deletion procedures after retention period
  - Compliance reporting for data retention status
- **Document Management**:
  - All loan documents stored in MinIO with retention policies
  - Digital signatures and timestamps for document integrity
  - Automated backup and disaster recovery procedures

### Requirement: Data Sovereignty & Security
**Regulatory Mandate**: Data must be stored within national borders and protected according to local data protection laws.

**How Our System Complies**:
- **Zambian Data Sovereignty**:
  - Entire platform deployed on Kubernetes cluster hosted within Zambia (Infratel/Paratus)
  - All data, including documents in MinIO and backups, encrypted and stored locally
  - No data transmission or storage outside Zambia's borders
  - Full compliance with Zambian Data Protection Act
- **Security Framework**:
  - End-to-end encryption for all data at rest and in transit
  - Role-based access control with principle of least privilege
  - Multi-factor authentication for all system access
  - API Gateway enforces Keycloak-issued OAuth2 tokens exclusively, validating
    issuer/audience over HTTPS to satisfy FR11 zero-trust mandates
  - Regular security audits and penetration testing
  - Incident response procedures and breach notification protocols

---

## 4. Regulatory Reporting & Monitoring

### Automated Regulatory Reports
**How Our System Complies**:
- **BoZ Prudential Returns**: Automated generation of monthly and quarterly prudential returns
- **Credit Reference Bureau Reporting**: Automated submission of loan performance data
- **AML/CFT Reporting**: Suspicious transaction monitoring and reporting
- **Tax Reporting**: Automated generation of tax-related reports and submissions

### Real-Time Compliance Monitoring
**How Our System Complies**:
- **Compliance Dashboard**: Real-time monitoring of all compliance metrics
- **Automated Alerts**: Immediate notification of any compliance breaches
- **Exception Reporting**: Daily reports of any system exceptions or anomalies
- **Regulatory Change Management**: System updates to accommodate regulatory changes

---

## 5. Implementation & Maintenance

### System Architecture Compliance
- **Microservices Architecture**: Each compliance requirement is implemented as a dedicated service
- **Event-Driven Design**: All compliance events trigger appropriate audit and monitoring processes
- **API-First Approach**: All compliance functions exposed through secure APIs for integration
- **Cloud-Native Design**: Built for scalability and reliability on Kubernetes platform

### Ongoing Compliance Management
- **Regular Compliance Reviews**: Quarterly reviews of compliance framework effectiveness
- **Regulatory Change Updates**: Systematic process for incorporating new regulatory requirements
- **Audit Support**: Comprehensive tools and reports to support internal and external audits
- **Training and Documentation**: Regular updates to user training and system documentation

---

## Conclusion

This compliance framework ensures that the LMS not only meets current regulatory requirements but is architected to adapt to future regulatory changes. By embedding compliance directly into the system's design, we eliminate the risk of human error and ensure consistent, auditable compliance with all applicable regulations.

The framework serves as both a development guide and a regulatory demonstration tool, clearly showing how each legal requirement is met through specific system features and processes.
