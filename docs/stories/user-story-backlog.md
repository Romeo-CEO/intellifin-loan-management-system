# LMS User Story Backlog

## Overview

This document contains the complete user story backlog for the Zambian Microfinance Loan Management System (LMS). Stories are organized by domain and prioritized for development. Each story includes acceptance criteria, business rules integration, and technical considerations.

## Story Organization

Stories are organized into the following domains:
1. **Foundation & Infrastructure** - Core system setup
2. **Identity & Security** - Authentication and authorization
3. **Client Management** - KYC/AML and customer profiles
4. **Loan Origination** - Multi-product loan application workflows with integrated credit assessment
5. **Financial Operations** - General ledger, payments, and collections
6. **Communications** - Multi-channel notifications
7. **Reporting & Compliance** - BoZ reporting and audit trails
8. **Offline Operations** - CEO offline capabilities
9. **System Administration** - Infrastructure and operations

---

## 1. Foundation & Infrastructure

### Epic 1.1: Core Infrastructure Setup

#### Story 1.1.1: Database Schema Creation
**As a** system administrator  
**I want** to set up the complete database schema with all required tables  
**So that** the system has a solid foundation for all business operations

**Acceptance Criteria:**
- [ ] All domain tables created (Client, Loan, Credit, GL, Collections, Communications, Audit)
- [ ] Proper foreign key relationships established
- [ ] Indexes created for performance optimization
- [ ] Audit trail tables with append-only constraints
- [ ] Database migration scripts created and tested
- [ ] Data seeding scripts for reference data (loan products, GL accounts)

**Business Rules Integration:**
- Money Lenders Act compliance fields (48% EAR cap)
- BoZ prudential classification fields
- PMEC integration fields
- Audit trail requirements

**Technical Notes:**
- SQL Server Always On setup
- Read replica configuration
- Backup and recovery procedures

#### Story 1.1.2: API Gateway Setup
**As a** developer  
**I want** to set up the API Gateway with authentication and routing  
**So that** all microservices are properly exposed and secured

**Acceptance Criteria:**
- [ ] .NET 9 Minimal API Gateway configured
- [ ] JWT authentication middleware
- [ ] Rate limiting and throttling
- [ ] Request/response logging
- [ ] Health check endpoints
- [ ] CORS configuration for frontend

**Technical Notes:**
- Load balancer integration
- SSL/TLS termination
- Service discovery configuration

#### Story 1.1.3: Message Queue Setup
**As a** system architect  
**I want** to set up RabbitMQ for reliable messaging  
**So that** microservices can communicate asynchronously

**Acceptance Criteria:**
- [ ] RabbitMQ cluster configured
- [ ] Dead letter queues for failed messages
- [ ] Message persistence enabled
- [ ] Monitoring and alerting setup
- [ ] Connection pooling configured
- [ ] Message serialization/deserialization

**Business Rules Integration:**
- PMEC integration queue resilience
- TransUnion API retry logic
- Collections workflow messaging

---

## 2. Identity & Security

### Epic 2.1: Authentication & Authorization

#### Story 2.1.1: User Authentication System
**As a** system user  
**I want** to authenticate securely with username/password  
**So that** I can access the system with proper security

**Acceptance Criteria:**
- [ ] JWT token generation and validation
- [ ] Password hashing with bcrypt
- [ ] Session management
- [ ] Token refresh mechanism
- [ ] Account lockout after failed attempts
- [ ] Password complexity requirements

**Business Rules Integration:**
- BoZ security requirements
- Audit trail for all authentication events
- Role-based access control

#### Story 2.1.2: Role-Based Access Control
**As a** system administrator  
**I want** to define user roles and permissions  
**So that** users can only access appropriate functionality

**Acceptance Criteria:**
- [ ] Role definitions (CEO, Manager, Officer, Analyst)
- [ ] Permission matrix implementation
- [ ] Role assignment interface
- [ ] Permission inheritance
- [ ] Audit trail for role changes
- [ ] Step-up authentication for sensitive operations

**Business Rules Integration:**
- CEO dual authorization requirements
- BoZ compliance officer permissions
- Branch-level access controls

#### Story 2.1.3: Step-Up Authentication
**As a** user performing sensitive operations  
**I want** to be prompted for additional authentication  
**So that** high-risk operations are properly secured

**Acceptance Criteria:**
- [ ] SMS-based OTP for step-up auth
- [ ] Biometric authentication support
- [ ] Timeout for step-up sessions
- [ ] Audit trail for step-up events
- [ ] Integration with sensitive operations
- [ ] Fallback authentication methods

**Business Rules Integration:**
- CEO authorization for large loans
- BoZ reporting access controls
- Financial transaction approvals

---

## 3. Client Management

### Epic 3.1: Customer Onboarding

#### Story 3.1.1: Customer Registration
**As a** loan officer  
**I want** to register new customers with complete KYC information  
**So that** we can onboard customers according to regulatory requirements

**Acceptance Criteria:**
- [ ] Personal information capture (name, ID, address, phone)
- [ ] Document upload (ID, proof of address, payslip)
- [ ] KYC verification workflow
- [ ] Duplicate customer detection
- [ ] Data validation and error handling
- [ ] Customer profile creation

**Business Rules Integration:**
- BoZ KYC requirements
- Money Lenders Act customer verification
- AML compliance checks
- Data retention policies

#### Story 3.1.2: KYC Document Verification
**As a** compliance officer  
**I want** to verify customer documents  
**So that** we meet regulatory KYC requirements

**Acceptance Criteria:**
- [ ] Document image capture and storage
- [ ] Document validation rules
- [ ] Manual verification workflow
- [ ] Verification status tracking
- [ ] Document expiration monitoring
- [ ] Compliance reporting

**Business Rules Integration:**
- BoZ document requirements
- AML document verification
- Data sovereignty requirements

#### Story 3.1.3: Customer Profile Management
**As a** loan officer  
**I want** to view and update customer profiles  
**So that** I can maintain accurate customer information

**Acceptance Criteria:**
- [ ] Customer profile dashboard
- [ ] Profile update workflow
- [ ] Change history tracking
- [ ] Profile search and filtering
- [ ] Customer communication preferences
- [ ] Profile validation rules

**Business Rules Integration:**
- Data privacy requirements
- Audit trail for profile changes
- Customer consent management

---

## 4. Loan Origination

### Epic 4.1: Integrated Loan Origination with Credit Assessment

#### Story 4.1.1: Loan Origination Service (with Integrated Credit Assessment)
**As a** loan officer  
**I want** to process complete loan applications with integrated credit assessment  
**So that** I can efficiently handle the entire application-to-approval workflow

**Acceptance Criteria:**
- [ ] Dynamic loan application form generation based on product type
- [ ] Product-specific form fields and validation
- [ ] Integrated credit scoring with Rules-Based Risk Grade (A-F) calculation
- [ ] Credit factor analysis (income, employment, financial stability)
- [ ] Risk level classification and decision thresholds
- [ ] Camunda workflow integration for approval routing
- [ ] Score explanation and transparency reporting
- [ ] Performance optimization with caching and parallel processing

**Business Rules Integration:**
- PMEC salary loan eligibility
- Collateral loan requirements
- Business loan criteria
- Interest rate calculations (48% EAR cap)
- CEO approval thresholds
- Branch-level approvals
- BoZ compliance checks
- Money Lenders Act requirements

---

## 5. Financial Operations

### Epic 5.1: Integrated Financial Service

#### Story 5.1.1: Financial Service (Integrated GL, Payments, and Collections)
**As a** financial controller  
**I want** to manage all financial operations including GL, payments, and collections  
**So that** I can maintain accurate financial records and optimize cash flow

**Acceptance Criteria:**
- [ ] BoZ-compliant General Ledger with double-entry bookkeeping
- [ ] Automated transaction posting and reconciliation
- [ ] Payment processing with Tingg and PMEC integration
- [ ] Collections lifecycle management with DPD calculation
- [ ] BoZ prudential classification and provisioning
- [ ] Real-time GL balances and financial reporting
- [ ] Payment reconciliation and exception handling
- [ ] Comprehensive audit trail for all financial transactions

**Business Rules Integration:**
- BoZ accounting standards
- Money Lenders Act transaction rules
- PMEC integration requirements
- Tingg payment gateway specifications
- BoZ prudential classification rules
- Automated provisioning calculations

---

## 6. Communications

### Epic 6.1: Multi-Channel Notifications

#### Story 6.1.1: SMS Notification System
**As a** customer  
**I want** to receive SMS notifications about my loan  
**So that** I stay informed about important updates

**Acceptance Criteria:**
- [ ] SMS gateway integration (Africa's Talking)
- [ ] Template management system
- [ ] Personalization capabilities
- [ ] Delivery status tracking
- [ ] Retry logic for failed deliveries
- [ ] Cost tracking and monitoring

**Business Rules Integration:**
- Customer communication preferences
- Regulatory notification requirements
- Data privacy compliance

#### Story 6.1.2: Email Notification System
**As a** customer  
**I want** to receive email notifications about my loan  
**So that** I have a record of important communications

**Acceptance Criteria:**
- [ ] Email service integration
- [ ] HTML email templates
- [ ] Attachment capabilities
- [ ] Delivery status tracking
- [ ] Bounce handling
- [ ] Unsubscribe management

**Business Rules Integration:**
- Customer communication preferences
- Email marketing compliance
- Data retention policies

#### Story 6.1.3: In-App Notifications
**As a** system user  
**I want** to receive in-app notifications  
**So that** I stay informed about system events

**Acceptance Criteria:**
- [ ] Real-time notification system
- [ ] Notification preferences
- [ ] Notification history
- [ ] Read/unread status tracking
- [ ] Notification categorization
- [ ] Push notification support

**Business Rules Integration:**
- User communication preferences
- System event notifications
- Audit trail requirements

---

## 7. Reporting & Compliance

### Epic 7.1: BoZ Compliance Reporting

#### Story 7.1.1: Prudential Reporting
**As a** compliance officer  
**I want** to generate BoZ prudential reports  
**So that** we meet regulatory reporting requirements

**Acceptance Criteria:**
- [ ] BoZ report templates
- [ ] Automated data collection
- [ ] Report validation rules
- [ ] Report submission workflow
- [ ] Report approval process
- [ ] Report audit trail

**Business Rules Integration:**
- BoZ prudential requirements
- Money Lenders Act reporting rules
- Regulatory submission deadlines

#### Story 7.1.2: Audit Trail System
**As a** compliance officer  
**I want** to maintain comprehensive audit trails  
**So that** we can demonstrate compliance with regulations

**Acceptance Criteria:**
- [ ] Comprehensive event logging
- [ ] Immutable audit records
- [ ] Audit trail querying
- [ ] Audit report generation
- [ ] Data retention management
- [ ] Audit trail monitoring

**Business Rules Integration:**
- BoZ audit requirements
- Money Lenders Act audit rules
- 10-year data retention policy

#### Story 7.1.3: Compliance Monitoring
**As a** compliance officer  
**I want** to monitor compliance metrics in real-time  
**So that** we can identify and address compliance issues

**Acceptance Criteria:**
- [ ] Real-time compliance dashboards
- [ ] Compliance metric calculations
- [ ] Alert system for violations
- [ ] Compliance reporting
- [ ] Trend analysis
- [ ] Remediation tracking

**Business Rules Integration:**
- BoZ compliance requirements
- Money Lenders Act compliance rules
- Automated compliance checks

---

## 8. Offline Operations

### Epic 8.1: CEO Offline Capabilities

#### Story 8.1.1: Offline Loan Origination
**As a** CEO  
**I want** to process loans offline when connectivity is poor  
**So that** business operations can continue regardless of network conditions

**Acceptance Criteria:**
- [ ] Offline data capture
- [ ] Local data storage
- [ ] Offline validation rules
- [ ] Sync when connectivity restored
- [ ] Conflict resolution
- [ ] Offline audit trail

**Business Rules Integration:**
- CEO authorization requirements
- Offline transaction limits
- Sync conflict resolution rules

#### Story 8.1.2: Offline Sync System
**As a** system administrator  
**I want** to sync offline data when connectivity is restored  
**So that** all data is properly synchronized

**Acceptance Criteria:**
- [ ] Bidirectional sync capability
- [ ] Conflict detection and resolution
- [ ] Sync status tracking
- [ ] Data integrity validation
- [ ] Sync error handling
- [ ] Sync audit trail

**Business Rules Integration:**
- Data integrity requirements
- Sync conflict resolution rules
- Audit trail for sync operations

---

## 9. System Administration

### Epic 9.1: Infrastructure Management

#### Story 9.1.1: System Monitoring
**As a** system administrator  
**I want** to monitor system performance and health  
**So that** I can ensure optimal system operation

**Acceptance Criteria:**
- [ ] Application Insights integration
- [ ] Performance metrics collection
- [ ] Health check endpoints
- [ ] Alert system configuration
- [ ] Dashboard creation
- [ ] Historical data analysis

**Business Rules Integration:**
- BoZ system availability requirements
- Performance SLA monitoring
- Incident response procedures

#### Story 9.1.2: Backup and Recovery
**As a** system administrator  
**I want** to implement comprehensive backup and recovery procedures  
**So that** we can protect against data loss

**Acceptance Criteria:**
- [ ] Automated backup scheduling
- [ ] Point-in-time recovery
- [ ] Disaster recovery procedures
- [ ] Backup validation
- [ ] Recovery testing
- [ ] Backup monitoring

**Business Rules Integration:**
- BoZ data protection requirements
- Money Lenders Act data retention
- Business continuity requirements

---

## Story Prioritization

### Phase 1: Foundation (Weeks 1-4)
- Database schema creation
- API Gateway setup
- Message queue setup
- User authentication system
- Role-based access control

### Phase 2: Core Business Logic (Weeks 5-12)
- KYC document verification and customer profile management
- Integrated loan origination with credit assessment
- Financial service with GL, payments, and collections

### Phase 3: Communications & Integration (Weeks 13-20)
- Multi-channel notification system (SMS, Email, In-App)
- PMEC integration via PMEC ACL Service
- Tingg payment gateway integration

### Phase 4: Compliance & Operations (Weeks 21-28)
- BoZ compliance reporting and audit trail system
- Compliance monitoring and real-time dashboards
- Offline operations and sync system

### Phase 5: System Administration & Optimization (Weeks 29-32)
- System monitoring and performance optimization
- Backup and recovery procedures
- System hardening and security enhancements
- User experience improvements

---

## Definition of Done

Each story must meet the following criteria before being considered complete:

### Functional Requirements
- [ ] All acceptance criteria met
- [ ] Business rules properly implemented
- [ ] Integration points working correctly
- [ ] Error handling implemented
- [ ] Data validation in place

### Quality Requirements
- [ ] Unit tests written and passing
- [ ] Integration tests implemented
- [ ] Code review completed
- [ ] Performance requirements met
- [ ] Security requirements satisfied

### Documentation Requirements
- [ ] API documentation updated
- [ ] User documentation updated
- [ ] Technical documentation updated
- [ ] Business rules documented
- [ ] Deployment procedures documented

### Compliance Requirements
- [ ] BoZ compliance verified
- [ ] Money Lenders Act compliance verified
- [ ] Audit trail implemented
- [ ] Data privacy requirements met
- [ ] Security requirements satisfied

---

## Notes

- All stories must comply with Zambian regulatory requirements
- Stories should be estimated using story points (1, 2, 3, 5, 8, 13)
- Stories should be sized to fit within 2-week sprints
- Dependencies between stories should be clearly identified
- Stories should be reviewed and approved by stakeholders before development begins
