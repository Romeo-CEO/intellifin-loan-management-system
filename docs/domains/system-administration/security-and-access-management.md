# Security and Access Management - Limelight Moneylink Services

## Executive Summary

This document defines the security and access control framework for the Limelight Moneylink Services LMS system, centered on the Principle of Least Privilege and Segregation of Duties. It specifies the use of ASP.NET Core Identity to manage a set of pre-defined business roles, ensuring secure access control while maintaining operational efficiency for our V1 system.

## Business Context

### Why Security and Access Management is Critical
- **Regulatory Compliance**: Meeting BoZ and Money Lenders Act requirements for access control and audit trails
- **Risk Management**: Preventing unauthorized access and ensuring proper segregation of duties
- **Operational Security**: Protecting sensitive customer and financial data
- **Audit Readiness**: Complete audit trail for all user activities and access changes
- **Business Continuity**: Ensuring secure access during both online and offline operations

### Security Philosophy
- **Principle of Least Privilege**: Users receive minimum necessary access for their job functions
- **Segregation of Duties**: Critical functions require multiple approvals and different user roles
- **Defense in Depth**: Multiple layers of security controls
- **Audit-Ready**: Complete audit trail for all security events
- **Compliance-First**: Built-in regulatory compliance and security requirements

## V1 User Roles & Key Permissions

### 1. Definitive V1 User Roles
**Core Business Roles**:
```
Loan Officer:
- Can create clients and loan applications
- CANNOT approve any loan
- Can view assigned branch data only
- Can process payments and collections
- Can update customer information

Credit Analyst:
- Can review applications and approve loans up to ZMW 50,000
- Can approve Risk Grades A & B only
- CANNOT disburse funds
- Can view credit bureau reports
- Can update risk assessments

Head of Credit:
- Can approve higher-value loans (above ZMW 50,000)
- Can approve Risk Grades C, D, F with justification
- CANNOT disburse funds
- Can override Credit Analyst decisions
- Can manage credit policies

Finance Officer:
- Can authorize disbursements for already-approved loans
- CANNOT approve loans
- Can process settlements and reconciliations
- Can manage payment processing
- Can access financial reports

CEO:
- Has read-only access to most data
- Can approve high-value loans (dual-control)
- Can authorize offline mode
- Can access executive dashboards
- Can manage risk limits and policies

System Administrator:
- Can manage users, roles, and business configurations
- Can configure loan product settings
- CANNOT perform any business-level functions
- Can manage system settings and integrations
- Can access audit logs and system reports
```

### 2. Role Hierarchy and Authority Matrix
**Approval Authority Matrix**:
```
Loan Approval Authority:
- ZMW 0 - 25,000: Credit Analyst (Risk Grades A & B only)
- ZMW 25,001 - 50,000: Credit Analyst (Risk Grades A & B only)
- ZMW 50,001 - 100,000: Head of Credit (All Risk Grades)
- ZMW 100,001+: Head of Credit + CEO Approval (Dual Control)
- Offline Loans: CEO Authorization Required

Disbursement Authority:
- All Approved Loans: Finance Officer
- High-Value Loans (ZMW 100,001+): Finance Officer + CEO Approval
- Offline Disbursements: CEO Authorization Required
```

**Segregation of Duties**:
```
Critical Separations:
- Loan Processing ≠ Loan Approval
- Loan Approval ≠ Loan Disbursement
- User Management ≠ Business Operations
- System Configuration ≠ Business Operations
- Audit Functions ≠ Operational Functions
```

## Authentication & Session Management

### 1. Technical Implementation
**Authentication Framework**:
```
Authentication System: ASP.NET Core Identity
- User authentication and management
- Role-based authorization
- Password policies and management
- Multi-factor authentication support
- Account lockout and security policies
```

**Session Management**:
```
Session Technology: JWT (JSON Web Tokens)
- Short-lived access tokens (15 minutes)
- Long-lived refresh tokens (7 days)
- Secure, HttpOnly cookies for token storage
- Immediate token revocation via Redis denylist
- Session timeout and automatic logout
```

### 2. Security Policies
**Password Policies**:
```
Password Requirements:
- Minimum 8 characters
- At least 1 uppercase letter
- At least 1 lowercase letter
- At least 1 number
- At least 1 special character
- Cannot reuse last 5 passwords
- Password expires every 90 days
```

**Account Security**:
```
Security Measures:
- Account lockout after 5 failed attempts
- Lockout duration: 30 minutes
- Multi-factor authentication for sensitive roles
- Session timeout: 8 hours of inactivity
- Automatic logout on browser close
```

## Authorization & Multi-Branch Context

### 1. Unified Customer, Attributed Performance Model
**Branch-Aware Authorization**:
```
Authorization Model:
- Unified customer database across all branches
- Performance attribution to originating branch
- User access filtered by assigned branch
- Cross-branch access for management roles
- Audit trail includes branch context
```

**Branch Access Control**:
```
Access Levels:
- Branch Level: Users see only their branch data
- Regional Level: Users see multiple branches in region
- National Level: Users see all branches (CEO, Head of Credit)
- System Level: Users see all data (System Administrator)
```

### 2. Authorization Policies
**Policy Implementation**:
```
Authorization Policies:
- Role-based policies for business functions
- Resource-based policies for data access
- Branch-based policies for location filtering
- Time-based policies for session management
- Context-aware policies for business rules
```

**Policy Examples**:
```
Policy Examples:
- Loan Officers can only view their branch customers
- Credit Analysts can approve loans within their authority
- Finance Officers can disburse approved loans
- System Administrators cannot perform business functions
- CEO can access all data but cannot perform operational tasks
```

## Audit Trail

### 1. Append-Only Audit Log
**Audit Requirements**:
```
Critical Audit Events:
- User creation and deactivation
- Role assignments and changes
- Permission modifications
- Login and logout events
- Failed authentication attempts
- Business transaction activities
- System configuration changes
- Data access and modifications
```

**Audit Log Structure**:
```
Audit Log Fields:
- Timestamp (UTC)
- User ID and username
- User role and permissions
- Action performed
- Resource accessed
- IP address and location
- Session identifier
- Branch context
- Result (success/failure)
- Additional context data
```

### 2. Audit Management
**Audit Process**:
```
Audit Workflow:
1. Event capture
2. Log entry creation
3. Immutable storage
4. Audit review
5. Compliance reporting
6. Forensic analysis
```

**Audit Compliance**:
```
Compliance Requirements:
- BoZ audit requirements
- Money Lenders Act audit requirements
- Data protection audit requirements
- Internal audit requirements
- External audit requirements
- Regulatory audit requirements
```

## Security Implementation

### 1. Technical Security
**Security Controls**:
```
Security Measures:
- HTTPS encryption for all communications
- Data encryption at rest and in transit
- Secure authentication and authorization
- Input validation and sanitization
- SQL injection prevention
- Cross-site scripting (XSS) prevention
- Cross-site request forgery (CSRF) protection
```

**Security Monitoring**:
```
Monitoring Activities:
- Real-time security event monitoring
- Failed authentication attempt tracking
- Unusual access pattern detection
- Security vulnerability scanning
- Compliance monitoring
- Incident response
```

### 2. Data Protection
**Data Security**:
```
Protection Measures:
- Personal data encryption
- Financial data encryption
- Credit bureau data encryption
- Secure data transmission
- Data access controls
- Data retention policies
- Secure data disposal
```

**Privacy Compliance**:
```
Privacy Requirements:
- Data protection regulations compliance
- Privacy policy implementation
- Consent management
- Data subject rights
- Data breach notification
- Privacy impact assessments
```

## Offline Operations Security

### 1. Offline Mode Authorization
**CEO Authorization Process**:
```
Authorization Workflow:
1. CEO initiates offline mode
2. System validates CEO credentials
3. Risk limits are set and validated
4. Offline mode is activated
5. Dual-control security is enforced
6. All activities are logged for sync
```

**Offline Security Controls**:
```
Security Measures:
- CEO authorization required
- Risk limit enforcement
- Dual-control for all transactions
- Local audit logging
- Secure data storage
- Automatic sync on reconnection
```

### 2. Voucher System Security
**Voucher Security**:
```
Security Features:
- Cryptographic hashes
- Digital signatures
- QR code integration
- Unique voucher identifiers
- Expiration timestamps
- Redemption tracking
```

**Voucher Management**:
```
Management Process:
1. Voucher generation
2. Digital signature application
3. Secure distribution
4. Redemption validation
5. Settlement processing
6. Audit trail maintenance
```

## Compliance and Governance

### 1. Regulatory Compliance
**Compliance Requirements**:
```
Compliance Standards:
- BoZ access control requirements
- Money Lenders Act compliance
- Data protection regulations
- Anti-money laundering requirements
- Know your customer requirements
- Consumer protection requirements
```

**Compliance Monitoring**:
```
Monitoring Activities:
- Access control compliance
- Audit trail compliance
- Data protection compliance
- Security compliance
- Operational compliance
- Regulatory compliance
```

### 2. Governance Framework
**Governance Structure**:
```
Governance Components:
- Security policy management
- Access control governance
- Audit governance
- Compliance governance
- Risk management governance
- Incident response governance
```

**Governance Process**:
```
Governance Workflow:
1. Policy development
2. Policy implementation
3. Policy monitoring
4. Policy review
5. Policy updates
6. Policy communication
```

## Performance and Monitoring

### 1. Performance Requirements
**Performance Metrics**:
```
Key Metrics:
- Authentication response time: < 2 seconds
- Authorization check time: < 1 second
- Session management performance
- Audit log performance
- System availability: 99.9%
- Security event response time
```

**Performance Monitoring**:
```
Monitoring Activities:
- Real-time performance monitoring
- Security event monitoring
- User activity monitoring
- System performance monitoring
- Compliance monitoring
- Incident monitoring
```

### 2. Incident Response
**Incident Management**:
```
Incident Process:
1. Incident detection
2. Incident classification
3. Response team activation
4. Incident investigation
5. Resolution implementation
6. Post-incident review
```

**Incident Types**:
```
Incident Categories:
- Security incidents
- Access control incidents
- Authentication incidents
- Authorization incidents
- Audit incidents
- Compliance incidents
```

## Next Steps

This Security and Access Management document serves as the foundation for:
1. **System Development** - Security and access control system implementation
2. **User Management** - User creation, role assignment, and permission management
3. **Authentication Setup** - ASP.NET Core Identity implementation and configuration
4. **Authorization Framework** - Role-based and resource-based authorization policies
5. **Audit Implementation** - Comprehensive audit trail and compliance monitoring

---

**Document Status**: Ready for Review  
**Next Document**: `infrastructure-and-operations.md` - Infrastructure and operations management
