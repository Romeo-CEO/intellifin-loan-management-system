# Loan Application Workflow - Limelight Moneylink Services

## Document Information
- **Document Type**: Business Process Specification
- **Version**: 2.0
- **Last Updated**: [Date]
- **Owner**: Loan Origination Team
- **Compliance**: BoZ Requirements, Money Lenders Act, KYC/AML Requirements
- **Reference**: Loan Product Catalog, Credit Assessment Process, PMEC Integration

## Executive Summary

This document defines the lean, system-driven loan application workflow for Limelight Moneylink Services. This workflow is designed to be lean and highly automated, leveraging real-time integrations to achieve a target application-to-decision time of under 24 hours. It focuses on speed, data quality, and customer experience while maintaining regulatory compliance.

## Business Context

### Why Application Workflow is Critical
- **Competitive Advantage**: Speed is our key differentiator - sub-24-hour decisions
- **Customer Experience**: Fast, seamless application process
- **Data Quality**: Automated validation and real-time verification
- **Operational Efficiency**: Lean, parallel processing eliminates bottlenecks
- **Risk Management**: Automated background checks and real-time risk assessment

### Workflow Design Principles
- **Speed-First**: Sub-24-hour application-to-decision target
- **Automation-Driven**: Real-time integrations and background processing
- **Lean Process**: Single session, parallel processing
- **Quality-Assured**: Automated validation and real-time verification
- **Customer-Centric**: Fast, seamless experience

## Application Workflow Overview

### High-Level Process Flow
```
Application Initiation & Data Capture → Automated Verification & Checks → Underwriter Review & Decision
```

### Workflow Stages
1. **Application Intake (Single Session)** - Guided data capture with real-time validation
2. **Underwriter Review & Decision** - Complete package review and decision making

## Stage 1: Application Intake (Single Session)

### 1.1 Application Initiation
**System Actions**:
```
- A Loan Officer (or borrower via future web portal) starts a new application
- System immediately assigns a unique Application ID
- Product selection (Payroll vs. Business) determines wizard flow
- Real-time validation begins immediately
```

**Quick Disqualification Factors**:
```
Immediate Disqualifications:
- Under 18 or over 65 years old
- Non-Zambian resident without valid work permit
- No verifiable source of income
- Currently declared bankrupt
- Active loan with another microfinance institution
- Criminal record related to financial fraud
```

### 1.2 Guided Data Capture
**Next.js Frontend Wizard**:
```
- Single, multi-step wizard that intelligently adapts based on loan product
- Real-time inline validation for NRC formats, phone numbers, etc.
- Integrated document upload within the same wizard
- OCR attempts to auto-fill fields from NRC for officer verification
```

### 1.3 Required Data Collection
**Personal Information**:
```
Identity Information:
- Full legal name (as per national ID)
- National ID number
- Date of birth
- Place of birth
- Nationality
- Marital status
- Number of dependents

Contact Information:
- Current residential address
- Address history (last 3 years)
- Primary phone number
- Secondary phone number
- Email address
- Emergency contact person
- Emergency contact phone number

Employment Information:
- Current employer name
- Job title/position
- Department/division
- Employment start date
- Employment type (permanent, contract, etc.)
- Monthly gross salary
- Monthly net salary
- Employment benefits
```

**Financial Information**:
```
Income Details:
- Monthly gross salary
- Monthly net salary
- Additional income sources
- Income frequency and stability
- Employment benefits value
- Overtime and bonus income

Other Income Sources:
- Rental income
- Investment income
- Business income (if applicable)
- Other regular income
- Seasonal income patterns

Monthly Expenses:
- Housing costs (rent/mortgage)
- Utility bills
- Food and groceries
- Transportation costs
- Healthcare expenses
- Education costs
- Insurance premiums
- Other regular expenses

Debt Obligations:
- Existing loan payments
- Credit card payments
- Other debt obligations
- Monthly debt service ratio
```

**Loan Information**:
```
Loan Details:
- Requested loan amount
- Preferred loan term
- Loan purpose
- Expected disbursement date
- Repayment preference (PMEC, manual, etc.)
- Collateral offered (if applicable)
- Guarantor information (if applicable)

Product-Specific Information:
Government Employee Loans:
- PMEC registration status
- Payroll deduction preference
- Government department
- Employee number
- Salary grade
- Employment confirmation

SME Asset-Backed Loans:
- Business registration details
- Business type and industry
- Business address and operations
- Collateral type and value
- Business financial statements
- Market analysis
- Growth projections
```

### 1.4 Integrated Document Upload
**Required Documentation**:
```
Identity Documents:
- National ID card (front and back)
- Passport (if applicable)
- Driver's license (if applicable)
- Birth certificate
- Marriage certificate (if applicable)

Address Verification:
- Utility bill (electricity, water, etc.)
- Bank statement
- Rental agreement
- Property ownership documents
- Recent mail with current address

Employment Documents:
Government Employee:
- Employment letter
- Salary slip (last 3 months)
- PMEC registration confirmation
- Government ID card
- Department assignment letter

SME Business Owner:
- Business registration certificate
- Tax registration certificate
- Business license
- Financial statements (last 2 years)
- Bank statements (last 6 months)
- Business plan and projections

Financial Documents:
- Salary slips (last 6 months)
- Bank statements (last 6 months)
- Tax returns (last 2 years)
- Employment contract
- Benefits documentation
```

### 1.5 Automated Background Processing
**Real-Time System Actions**:
```
As soon as sufficient data is available:
- Internal duplicate client profile check
- PMEC ACL Service API call for government employment verification
- Credit Bureau Service API call for TransUnion report (new clients)
- Document quality validation and OCR processing
- Risk grade calculation (A-F) based on available data
```

## Stage 2: Underwriter Review & Decision

### 2.1 Application Queue Management
**System Actions**:
```
- Completed applications automatically land in Underwriter's queue
- Priority scoring based on risk grade and completeness
- Real-time notifications for new applications
- Background checks completion status tracking
```

### 2.2 Underwriter Review Screen
**Consolidated View**:
```
Single, comprehensive review interface displaying:
- All captured application data
- All uploaded documents with easy viewing
- PMEC verification results
- TransUnion credit report
- System-calculated risk grade (A-F)
- Automated validation results
- Compliance check status
- Risk indicators and flags
```

### 2.3 Decision Making Process
**Underwriter Actions**:
```
- Review complete application package
- Validate automated risk assessment
- Make final credit decision
- Set loan terms and conditions
- Approve, reject, or request additional information
- Document decision rationale
- Update application status
```

## Performance Controls

### Timeline Management
**Aggressive Performance Targets**:
```
Application Intake: Less than 30 minutes (for a trained loan officer)
Automated Checks: Less than 5 minutes (concurrently)
Underwriter Review & Decision: Target of 4 business hours
Total Target Application-to-Decision Time: Less than 24 hours
```

### Quality Metrics
**Performance Indicators**:
```
- Application completion rate
- Document quality score
- Validation accuracy
- Customer satisfaction
- Processing time compliance
- Error rate
- Rejection rate
- Time to decision
```

## Customer Experience Considerations

### Application Accessibility
**Multi-Channel Access**:
```
- Branch office (primary)
- Mobile application (future)
- Web portal (future)
- Call center support
- Field officer visits
- Partner locations
```

### Customer Support
**Support Services**:
```
- Dedicated loan officers
- Application assistance
- Document guidance
- Process explanation
- Status updates
- Problem resolution
- Real-time application tracking
```

## System Integration Points

### Real-Time APIs
**External Services**:
```
- PMEC ACL Service: Government employment verification
- Credit Bureau Service: TransUnion credit reports
- Document OCR Service: Automated data extraction
- Risk Assessment Engine: Real-time risk grading
```

### Internal Systems
**Core Platform**:
```
- Next.js Frontend: Application wizard
- Node.js Backend: Business logic and API management
- PostgreSQL Database: Application data storage
- Document Management: Secure file storage and retrieval
- Workflow Engine: Application state management
```

## Next Steps

This revised Loan Application Workflow document serves as the foundation for:
1. **System Configuration** - Application workflow setup and automation
2. **Staff Training** - Loan officer training on lean application processes
3. **Customer Communication** - Application guides and customer education
4. **Process Optimization** - Continuous improvement and efficiency gains

## Document Approval

- **Loan Origination Manager**: [Name] - [Date]
- **Compliance Officer**: [Name] - [Date]
- **Technical Lead**: [Name] - [Date]
- **CEO**: [Name] - [Date]

---

**Document Control**: This document must be reviewed and updated quarterly or when business processes change.
