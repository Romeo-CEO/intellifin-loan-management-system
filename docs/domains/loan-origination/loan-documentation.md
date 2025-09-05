# Loan Documentation - Limelight Moneylink Services

## Document Information
- **Document Type**: Business Process Specification
- **Version**: 2.0
- **Last Updated**: [Date]
- **Owner**: Loan Origination Team
- **Compliance**: BoZ Requirements, Money Lenders Act, Legal Standards, Audit Requirements
- **Reference**: Loan Product Catalog, Loan Application Workflow, Credit Assessment Process

## Executive Summary

This document defines the product-specific, actionable document checklist for the Limelight Moneylink Services LMS system. It provides a Master Document Checklist that serves as the single source of truth for every document type the system can manage for our V1 products: Government Employee Payroll Loans (GEPL) and SME Asset-Backed Loans (SMEABL). This checklist is designed to be directly actionable for developers and eliminates all ambiguity about which documents are needed for which product and at what stage.

## Business Context

### Why Loan Documentation is Critical
- **Legal Protection**: Provides legal basis for loan agreements and enforcement
- **Regulatory Compliance**: Meets BoZ and Money Lenders Act requirements
- **Risk Management**: Ensures proper documentation of all loan terms and conditions
- **Customer Protection**: Clear documentation of customer rights and obligations
- **Audit Readiness**: Complete documentation for internal and external audits
- **Operational Control**: Standardized documentation ensures consistency

### Documentation Design Principles
- **Product-Specific**: Tailored to exact requirements of GEPL and SMEABL products
- **Actionable**: Direct mapping to system database and UI requirements
- **Stage-Aware**: Clear indication of when each document is required
- **Compliance-Focused**: Built-in regulatory requirement validation
- **Developer-Friendly**: Structured for direct system implementation
- **Audit-Ready**: Complete audit trail and documentation

## Master Document Checklist

### Overview
This Master Document Checklist is the single source of truth for every document type the system can manage. Each entry contains the key properties needed for system implementation:

- **Document Name**: Human-readable document title
- **Document Code**: System identifier for database and API use
- **Required For Product(s)**: GEPL (Government Employee Payroll Loan), SMEABL (SME Asset-Backed Loan), or ALL
- **Required Stage**: When the document is needed in the workflow
- **File Types**: Accepted file formats for upload
- **Versioned**: Whether the document supports versioning
- **System Generated**: Whether the document is created by the system

### Master Checklist Table

| Document Name | Document Code | Required For Product(s) | Required Stage | File Types | Versioned | System Generated |
|---------------|---------------|-------------------------|----------------|------------|-----------|------------------|
| **KYC & Identity Documents** |
| National Registration Card | KYC_NRC | ALL | Application Intake | PDF, JPG, PNG | Yes | No |
| Proof of Address | KYC_POA | ALL | Application Intake | PDF, JPG, PNG | Yes | No |
| Recent Photograph | KYC_PHOTO | ALL | Application Intake | JPG, PNG | No | No |
| Signature Specimen | KYC_SIGNATURE | ALL | Application Intake | PDF, JPG, PNG | No | No |
| **Employment & Business Documents** |
| Letter of Employment | EMP_EMPLOYMENT | GEPL | Application Intake | PDF | No | No |
| Government ID Card | EMP_GOVT_ID | GEPL | Application Intake | PDF, JPG, PNG | No | No |
| Latest 3 Payslips | FIN_PAYSLIP | GEPL | Application Intake | PDF, JPG, PNG | No | No |
| Latest 3 Bank Statements | FIN_BANKSTMT_GEPL | GEPL | Application Intake | PDF | No | No |
| Business Registration Certificate | BUS_REG | SMEABL | Application Intake | PDF | No | No |
| ZRA Tax Clearance Certificate | BUS_TAX_CLEAR | SMEABL | Application Intake | PDF | No | No |
| Latest 6 Months Business Bank Statements | FIN_BANKSTMT_SME | SMEABL | Application Intake | PDF | No | No |
| **Financial & Income Documents** |
| Employment Contract | EMP_CONTRACT | GEPL | Application Intake | PDF | No | No |
| Benefits Documentation | EMP_BENEFITS | GEPL | Application Intake | PDF | No | No |
| Business Financial Statements | BUS_FIN_STMT | SMEABL | Application Intake | PDF | No | No |
| **Collateral Documents (SMEABL Only)** |
| Vehicle Registration Book | COL_VEH_REG | SMEABL (Vehicle) | Disbursement Prep | PDF, JPG, PNG | No | No |
| Vehicle Insurance Cover Note | COL_VEH_INS | SMEABL (Vehicle) | Disbursement Prep | PDF | No | No |
| Vehicle Valuation Report | COL_VEH_VAL | SMEABL (Vehicle) | Disbursement Prep | PDF | No | No |
| Property Title Deed | COL_PROP_TITLE | SMEABL (Property) | Disbursement Prep | PDF | No | No |
| Property Rates Clearance | COL_PROP_RATES | SMEABL (Property) | Disbursement Prep | PDF | No | No |
| Property Valuation Report | COL_PROP_VAL | SMEABL (Property) | Disbursement Prep | PDF | No | No |
| **System-Generated Documents** |
| Signed Loan Application Form | SYS_APP_FORM | ALL | Application Intake | PDF | Yes | Yes |
| Director(s) Personal Guarantee Form | SYS_GUARANTEE | SMEABL | Application Intake | PDF | Yes | Yes |
| Signed Loan Agreement | SYS_LOAN_AGMT | ALL | Disbursement Prep | PDF | Yes | Yes |
| Signed Disbursement Voucher | SYS_DISB_VOUCHER | ALL | Disbursement Prep | PDF | Yes | Yes |
| Repayment Schedule | SYS_REPAY_SCHED | ALL | Post-Disbursement | PDF | Yes | Yes |

### Document Requirements by Product

#### Government Employee Payroll Loans (GEPL)
**Mandatory Documents (Application Intake)**:
```
1. KYC_NRC - National Registration Card
2. KYC_POA - Proof of Address
3. KYC_PHOTO - Recent Photograph
4. KYC_SIGNATURE - Signature Specimen
5. EMP_EMPLOYMENT - Letter of Employment
6. EMP_GOVT_ID - Government ID Card
7. FIN_PAYSLIP - Latest 3 Payslips
8. FIN_BANKSTMT_GEPL - Latest 3 Bank Statements
9. EMP_CONTRACT - Employment Contract
10. EMP_BENEFITS - Benefits Documentation
11. SYS_APP_FORM - Signed Loan Application Form
```

**Mandatory Documents (Disbursement Prep)**:
```
1. SYS_LOAN_AGMT - Signed Loan Agreement
2. SYS_DISB_VOUCHER - Signed Disbursement Voucher
```

**Generated Documents (Post-Disbursement)**:
```
1. SYS_REPAY_SCHED - Repayment Schedule
```

#### SME Asset-Backed Loans (SMEABL)
**Mandatory Documents (Application Intake)**:
```
1. KYC_NRC - National Registration Card
2. KYC_POA - Proof of Address
3. KYC_PHOTO - Recent Photograph
4. KYC_SIGNATURE - Signature Specimen
5. BUS_REG - Business Registration Certificate
6. BUS_TAX_CLEAR - ZRA Tax Clearance Certificate
7. FIN_BANKSTMT_SME - Latest 6 Months Business Bank Statements
8. BUS_FIN_STMT - Business Financial Statements
9. SYS_APP_FORM - Signed Loan Application Form
10. SYS_GUARANTEE - Director(s) Personal Guarantee Form
```

**Mandatory Documents (Disbursement Prep)**:
```
1. SYS_LOAN_AGMT - Signed Loan Agreement
2. SYS_DISB_VOUCHER - Signed Disbursement Voucher
3. Collateral-specific documents (as applicable):
   - Vehicle: COL_VEH_REG, COL_VEH_INS, COL_VEH_VAL
   - Property: COL_PROP_TITLE, COL_PROP_RATES, COL_PROP_VAL
```

**Generated Documents (Post-Disbursement)**:
```
1. SYS_REPAY_SCHED - Repayment Schedule
```

## Document Management System Requirements

### Database Schema Requirements
**DocumentTypes Table**:
```sql
CREATE TABLE document_types (
    document_code VARCHAR(20) PRIMARY KEY,
    document_name VARCHAR(100) NOT NULL,
    required_for_products TEXT[] NOT NULL,
    required_stage VARCHAR(50) NOT NULL,
    accepted_file_types TEXT[] NOT NULL,
    is_versioned BOOLEAN DEFAULT false,
    is_system_generated BOOLEAN DEFAULT false,
    is_mandatory BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

**Document Instances Table**:
```sql
CREATE TABLE documents (
    document_id UUID PRIMARY KEY,
    loan_id UUID NOT NULL,
    document_code VARCHAR(20) NOT NULL,
    file_path VARCHAR(500) NOT NULL,
    file_name VARCHAR(200) NOT NULL,
    file_size BIGINT NOT NULL,
    mime_type VARCHAR(100) NOT NULL,
    upload_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    uploaded_by UUID NOT NULL,
    verification_status VARCHAR(20) DEFAULT 'PENDING',
    verified_by UUID,
    verified_at TIMESTAMP,
    version_number INTEGER DEFAULT 1,
    is_current_version BOOLEAN DEFAULT true,
    FOREIGN KEY (document_code) REFERENCES document_types(document_code)
);
```

### System Integration Points
**Document Upload Service**:
```
- File type validation based on accepted_file_types
- File size limits and security scanning
- OCR processing for NRC and other identity documents
- Automatic file naming and organization
- Integration with document storage system
```

**Document Verification Workflow**:
```
- Automatic routing to appropriate verifiers
- Status tracking and notifications
- Version control for updated documents
- Audit trail for all verification actions
- Integration with loan workflow stages
```

**Document Generation Service**:
```
- Template-based document creation
- Dynamic content population from loan data
- Digital signature integration
- PDF generation and formatting
- Version control and archiving
```

## Compliance and Quality Standards

### Document Quality Requirements
**File Standards**:
```
- Maximum file size: 10MB per document
- Minimum image resolution: 300 DPI for scanned documents
- Accepted formats: PDF (preferred), JPG, PNG
- File naming: [DocumentCode]_[LoanID]_[Timestamp].[Extension]
- OCR processing: Required for all identity documents
```

**Verification Standards**:
```
- All documents must be verified by authorized personnel
- Verification includes authenticity, completeness, and accuracy
- Failed verifications require immediate action and documentation
- Verification status must be tracked in real-time
- Audit trail for all verification actions
```

### Regulatory Compliance
**BoZ Requirements**:
```
- Complete customer identification documentation
- Employment and income verification
- Address verification and history
- Financial capacity assessment
- Risk classification documentation
- Regulatory reporting compliance
```

**Money Lenders Act Compliance**:
```
- Interest rate documentation (48% EAR limit)
- Fee structure transparency
- Loan term documentation
- Customer protection provisions
- Regulatory reporting requirements
```

## Implementation Guidelines

### Development Priorities
**Phase 1 (Core Document Management)**:
```
1. DocumentTypes table creation and population
2. Basic document upload and storage
3. Document verification workflow
4. Integration with loan application workflow
```

**Phase 2 (Advanced Features)**:
```
1. OCR processing and data extraction
2. Document generation service
3. Version control and archiving
4. Advanced search and retrieval
```

**Phase 3 (Integration & Automation)**:
```
1. PMEC integration for government documents
2. Credit bureau integration
3. Automated compliance checking
4. Advanced reporting and analytics
```

### UI/UX Requirements
**Document Upload Interface**:
```
- Dynamic checklist based on selected product
- Clear indication of mandatory vs. optional documents
- Real-time validation and feedback
- Progress tracking and completion status
- Mobile-responsive design for field officers
```

**Document Management Dashboard**:
```
- Document status overview
- Verification queue management
- Search and filter capabilities
- Bulk operations for multiple documents
- Integration with loan workflow stages
```

## Next Steps

This Master Document Checklist serves as the foundation for:
1. **System Configuration** - Document management system setup and configuration
2. **Database Design** - Document storage and management schema
3. **UI Development** - Dynamic document checklist and upload interfaces
4. **Integration Development** - Document processing and verification workflows
5. **Quality Assurance** - Document compliance and verification testing

## Document Approval

- **Loan Origination Manager**: [Name] - [Date]
- **Legal Counsel**: [Name] - [Date]
- **Compliance Officer**: [Name] - [Date]
- **Technical Lead**: [Name] - [Date]
- **CEO**: [Name] - [Date]

---

**Document Control**: This document must be reviewed and updated quarterly or when business processes change.
