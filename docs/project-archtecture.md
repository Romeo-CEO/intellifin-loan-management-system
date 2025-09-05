# Zambian Microfinance Loan Management System - Final Technical Architecture

## Executive Summary

This document outlines the comprehensive technical architecture for a BoZ-compliant, cloud-native back-office loan management platform specifically engineered for Zambian microfinance institutions. The system features PMEC integration for government employee payroll deductions, traditional disbursement workflows, and enterprise-grade resilience with complete Zambian data sovereignty.

## Features (MVP) - Final Architecture

### Client Management & KYC System with Performance Optimization
A unified customer profile system with intelligent caching layers that captures government employee data, tracks BoZ compliance, and provides sub-second cross-branch customer lookup. Features comprehensive document access logging and automated retention management.

#### Tech Involved
* .NET 9 Web API with Entity Framework Core and Redis caching layer
* NextJS frontend with optimized customer search components
* SQL Server with customer data partitioning and indexing strategies
* MinIO object storage with document access audit logging
* PMEC Anti-Corruption Layer for real-time government employee verification

#### Main Requirements
* Redis-based customer data caching for sub-second cross-branch lookup
* Comprehensive document access logging for compliance auditing
* 10-year document retention policy with automated lifecycle management
* Branch attribution tracking without performance degradation
* Intelligent cache invalidation strategies for data consistency
* Audit trail separation from operational database for performance isolation

### Credit Bureau Integration with Queue-Based Resilience
A cost-optimized credit assessment system with intelligent routing, comprehensive retry logic, and fallback mechanisms designed for external API reliability challenges in the Zambian market. Orchestrated with BPMN workflows for visibility and control.

#### Tech Involved
* .NET 9 microservice with Polly resilience patterns and circuit breakers
* Camunda 8 (Zeebe) workflows orchestrating credit checks and retries
* SQL Server for credit history with separate audit trail database
* Azure Service Bus for reliable message queuing
* HashiCorp Vault with annual credential rotation policies
* Background services for async processing and retry mechanisms

#### Main Requirements
* Intelligent client classification with performance-optimized lookup
* Queue-based processing for API reliability during TransUnion outages
* Automated retry with exponential backoff and manual intervention triggers
* Annual credential rotation with automated testing and validation
* Separate audit database for compliance queries without performance impact
* Cost optimization through intelligent API call batching and caching
* BPMN-modeled credit processes with human task steps for manual review

### Advanced Loan Origination with Dual-Control Offline Security
A sophisticated loan processing system supporting CEO-authorized offline modes with dual-control authentication, encrypted digital vouchers, and comprehensive conflict resolution upon reconnection. Full workflow orchestration for application, underwriting, approval, disbursement, and collections handover.

#### Tech Involved
* .NET 9 microservices with MediatR CQRS pattern
* Camunda 8 (Zeebe) BPMN workflow engine orchestrating loan lifecycle
* Electron desktop application with dual-authentication workflows
* SQL Server with geo-replication and conflict resolution algorithms
* Digital signature libraries for secure voucher generation
* Step-up authentication with time-bound session management

#### Main Requirements
* Dual-control offline disbursement (loan officer + senior staff authentication)
* Step-up authentication requirements for sensitive offline operations
* Encrypted digital voucher system without physical printing requirements
* Intelligent sync conflict resolution with business rule prioritization
* Real-time risk exposure tracking within CEO-defined parameters
* Comprehensive audit trail for all offline operations and authorizations
* Workflow-level SLAs, escalation, and visibility via Camunda Operate

### High-Performance General Ledger with Hybrid Analytics
A double-entry accounting system optimized for real-time operational queries and pre-computed historical analytics, with separate read replicas and intelligent caching strategies for sub-10-second dashboard performance.

#### Tech Involved
* .NET 9 financial services with optimized Entity Framework queries
* SQL Server Always On with async replication and dedicated read replicas
* Background services for pre-computing complex portfolio metrics
* SignalR for real-time operational metric updates
* Redis caching for frequently accessed GL balances and ratios

#### Main Requirements
* Hybrid analytics: pre-computed historical metrics + real-time operational data
* Asynchronous SQL Server replication with 1-hour RPO acceptance
* Manual failover control with management team authorization requirements
* Optimized read replica strategy for reporting without transaction impact
* Intelligent caching of GL balances with real-time invalidation
* Background job orchestration for nightly and hourly metric calculations

### PMEC Integration with Resilient Queue Architecture
A dedicated Anti-Corruption Layer microservice with local queue system for handling government PMEC system downtime, ensuring loan processing continuity regardless of external system availability.

#### Tech Involved
* Dedicated .NET 9 microservice with Azure Service Bus integration
* Local queue persistence with SQL Server for reliability
* Camunda 8 (Zeebe) workflows for PMEC request orchestration and retries
* Adaptive protocol handling with comprehensive logging
* Automated retry mechanisms with configurable intervals
* HashiCorp Vault integration for secure government credential management

#### Main Requirements
* Local queue system for PMEC downtime resilience
* Automated retry logic with exponential backoff and manual intervention
* Comprehensive audit trail for government system interactions
* Real-time queue monitoring with alerting for processing delays
* Secure credential management with annual rotation requirements
* Clean REST API abstraction isolating government system complexities

### Enterprise Security with Intelligent Branch Attribution
A sophisticated security framework supporting unified customer profiles with intelligent caching, granular role-based permissions, and comprehensive audit trail separation for regulatory compliance.

#### Tech Involved
* .NET 9 Identity with JWT and Redis-based session management
* SQL Server with optimized indexing for cross-branch customer queries
* Separate audit trail database with dedicated read replicas
* Azure Active Directory B2C with step-up authentication capabilities
* Custom authorization policies with branch context and caching

#### Main Requirements
* Intelligent customer data caching preventing cross-branch lookup degradation
* Separate audit trail storage for compliance queries without performance impact
* Granular permissions with branch context switching capabilities
* Performance-optimized unified customer model with sub-second response times
* Comprehensive security event logging with regulatory compliance focus
* Step-up authentication integration for sensitive operations

### Regulatory Reporting & Business Reporting Engine (JasperReports)
A regulatory and operational reporting capability supporting BoZ prudential reporting and internal business reports with scheduling, parameterized queries, and role-based access. Uses read replicas to isolate reporting workloads and provides traceable audit logs of report execution.

#### Tech Involved
* JasperReports Server as the reporting engine
* .NET 9 Reporting Service for parameter management and API access
* SQL Server read replicas as primary data sources
* MinIO for report artifact storage (exports, templates, snapshots)
* Camunda 8 workflows for scheduled report generation and distribution

#### Main Requirements
* Prebuilt BoZ-compliant report templates and on-time submission
* Parameterized, role-restricted reports with row-level security alignment
* Scheduled and on-demand report execution with delivery via email/SMS/portal
* Full audit trail of report access, generation parameters, and outputs
* Isolation from OLTP workloads via read replicas and caching where appropriate

### CEO Offline Command Center with Comprehensive Business Intelligence
An advanced desktop application providing complete offline business management capabilities, dual-control disbursement authorization, and intelligent synchronization with automated conflict resolution.

#### Tech Involved
* Electron desktop application with SQLCipher encrypted local database
* Intelligent sync algorithms with business rule-based conflict resolution
* Chart.js and D3.js for advanced offline data visualization
* Digital signature capabilities with dual-control workflow support
* Automated data cleanup with configurable retention policies

#### Main Requirements
* Complete offline operation with pre-computed metric calculations
* Dual-control authorization workflow for offline disbursement parameters
* Intelligent bidirectional sync with automated conflict resolution
* Comprehensive KPI dashboard with offline calculation capabilities
* Digital audit trail for all offline authorizations and decisions
* Automated local data cleanup with security-focused retention policies

## System Diagram - Production Architecture

```mermaid
graph TB
    subgraph "Client Layer"
        WEB[NextJS Web App<br/>Optimized for Tablets]
        OFFLINE[CEO Offline Command Center<br/>Electron + Dual Auth]
    end

    subgraph "Kubernetes Cluster - Auto Scaling (3+ Nodes)"
        subgraph "API Gateway & Load Balancing"
            LB[Azure Load Balancer]
            GATEWAY[API Gateway<br/>.NET 9 Minimal APIs]
        end
        
        subgraph "Core Microservices"
            AUTH[Identity Service<br/>JWT + Step-up Auth]
            CLIENT[Client Management<br/>with Redis Caching]
            LOAN[Loan Origination<br/>with Offline Support]
            CREDIT[Credit Bureau Service<br/>with Queue Resilience]
            GL[General Ledger<br/>Hybrid Analytics]
            PMEC_SERVICE[PMEC Anti-Corruption<br/>Layer + Queue]
            WORKFLOW[Camunda 8 (Zeebe)<br/>Workflow Engine]
            REPORT[Reporting Service<br/>Pre-computed Metrics]
            JASPER[JasperReports Server<br/>Regulatory Reporting]
            SYNC[Offline Sync Service<br/>Conflict Resolution]
        end

        subgraph "Message Queues & Caching"
            SERVICEBUS[Azure Service Bus<br/>Reliable Queuing]
            REDIS[Redis Cache<br/>Customer & GL Data]
        end
    end

    subgraph "External Integration Layer"
        PMEC[PMEC Government System<br/>Unreliable Availability]
        TRANSUNION[TransUnion Zambia<br/>Credit Bureau API]
        SMS[SMS Gateway Provider]
        BANK[Local Banking Systems]
    end

    subgraph "Primary Data Center - Infratel"
        PRIMARY_SQL[(SQL Server Always On<br/>Primary + Read Replica)]
        AUDIT_DB[(Separate Audit Database<br/>Compliance Isolation)]
        MINIO_PRIMARY[MinIO Object Storage<br/>Document Access Logging]
        VAULT_PRIMARY[HashiCorp Vault<br/>Annual Credential Rotation)]
    end

    subgraph "Secondary Data Center - Paratus"
        SECONDARY_SQL[(SQL Server Always On<br/>Async Secondary - 1hr RPO)]
        AUDIT_BACKUP[(Audit Database<br/>Backup Instance)]
        MINIO_SECONDARY[MinIO Backup<br/>10-year Retention)]
        VAULT_SECONDARY[HashiCorp Vault<br/>Backup Instance)]
    end

    subgraph "Background Processing"
        JOBS[Background Services<br/>Pre-computed Analytics]
        DR_TEST[Semi-Annual DR Testing<br/>Manual Procedures]
    end

    subgraph "Monitoring & Compliance"
        MONITOR[Application Insights<br/>Performance Metrics]
        LOGS[Centralized Logging<br/>Operational Events]
        AUDIT_MONITOR[Audit Trail Monitor<br/>Compliance Reporting]
    end

    WEB --> LB
    OFFLINE -.->|Encrypted Sync<br/>Conflict Resolution| SYNC
    WEB --> JASPER
    
    LB --> GATEWAY
    GATEWAY --> AUTH
    
    AUTH --> CLIENT
    AUTH --> LOAN  
    AUTH --> CREDIT
    AUTH --> GL
    AUTH --> REPORT
    
    CLIENT --> REDIS
    GL --> REDIS

    %% Orchestration links
    LOAN --> WORKFLOW
    CREDIT --> WORKFLOW
    PMEC_SERVICE --> WORKFLOW
    SYNC --> WORKFLOW
    WORKFLOW --> REPORT
    
    LOAN --> PMEC_SERVICE
    CREDIT --> SERVICEBUS
    PMEC_SERVICE --> SERVICEBUS
    
    PMEC_SERVICE -.->|Queue Resilience| PMEC
    CREDIT --> TRANSUNION
    GL --> SMS
    GL --> BANK
    
    CLIENT --> PRIMARY_SQL
    LOAN --> PRIMARY_SQL
    CREDIT --> PRIMARY_SQL
    GL --> PRIMARY_SQL
    
    REPORT --> PRIMARY_SQL
    JASPER --> PRIMARY_SQL
    JOBS --> PRIMARY_SQL
    
    AUTH --> AUDIT_DB
    CLIENT --> AUDIT_DB
    LOAN --> AUDIT_DB
    PMEC_SERVICE --> AUDIT_DB
    
    CLIENT --> MINIO_PRIMARY
    LOAN --> MINIO_PRIMARY
    
    PRIMARY_SQL -.->|Async Replication<br/>Manual Failover| SECONDARY_SQL
    AUDIT_DB -.->|Replication| AUDIT_BACKUP
    MINIO_PRIMARY -.->|Geo Replication| MINIO_SECONDARY
    VAULT_PRIMARY -.->|Backup Sync| VAULT_SECONDARY
    
    PMEC_SERVICE --> VAULT_PRIMARY
    CREDIT --> VAULT_PRIMARY
    
    JOBS --> REDIS
    
    DR_TEST -.->|Semi-Annual<br/>Manual Testing| SECONDARY_SQL
    
    CLIENT --> MONITOR
    LOAN --> MONITOR
    GL --> MONITOR
    PMEC_SERVICE --> MONITOR
    
    AUDIT_DB --> AUDIT_MONITOR
    AUDIT_BACKUP --> AUDIT_MONITOR
```

## Technology Stack

### **Core Technologies**
- **Backend**: .NET 9 with ASP.NET Core Web APIs
- **Frontend**: NextJS with TypeScript (tablet-optimized)
- **Process Orchestration**: Camunda 8 (Zeebe) BPMN engine with external task workers
- **Reporting**: JasperReports Server for regulatory and operational reporting
- **Database**: SQL Server with Always On availability groups
- **Caching**: Redis cluster for performance optimization
- **Message Queuing**: Azure Service Bus for reliable async processing
- **Object Storage**: MinIO with comprehensive audit logging
- **Secrets Management**: HashiCorp Vault with annual rotation
- **Desktop Application**: Electron with SQLCipher for offline capabilities

### **Integration & External Services**
- **PMEC Integration**: Dedicated .NET 9 Anti-Corruption Layer microservice orchestrated by Camunda
- **Credit Bureau**: TransUnion Zambia API with resilience patterns and workflow orchestration
- **SMS Gateway**: Local Zambian provider integration
- **Banking Systems**: Traditional bank transfer integration
- **Identity Management**: Azure Active Directory B2C with step-up authentication

### **Infrastructure & Deployment**
- **Orchestration**: Kubernetes with horizontal pod autoscaling
- **Workflow Engine**: Camunda 8 cluster (Zeebe brokers, Operate) deployed in primary DC
- **Primary Hosting**: Infratel Data Center (Zambian)
- **Secondary Hosting**: Paratus Data Center (Zambian)
- **Load Balancing**: Azure Load Balancer with health checks
- **Monitoring**: Application Insights with custom metrics
- **Logging**: Centralized ELK stack implementation

## Final Technical Architecture Decisions

### **Infrastructure & Scaling**
- **Kubernetes Strategy**: Start with 3-node cluster, horizontal pod autoscaling enabled from day one
- **Database Configuration**: SQL Server Always On with asynchronous replication (1-hour RPO)
- **Failover Control**: Manual failover with management team authorization (no automatic failover)
- **Caching Strategy**: Redis cluster for customer data and GL balances ensuring sub-second cross-branch performance
- **Document Storage**: MinIO with comprehensive access logging and 10-year retention policy
- **Workflow Engine**: Camunda 8 (Zeebe) standardized for all long-running and human-in-the-loop processes
- **Reporting Engine**: JasperReports Server standardized for regulatory and operational reporting

### **Resilience & Reliability**
- **PMEC Integration**: Local queue system with Azure Service Bus for government system downtime resilience
- **Credit Bureau**: Queue-based processing with intelligent retry mechanisms and circuit breakers
- **Disaster Recovery**: Semi-annual manual testing with 4-hour RTO target and formal reporting procedures
- **Security Management**: Primary-secondary HashiCorp Vault configuration with annual credential rotation
- **External API Resilience**: Polly resilience patterns with exponential backoff and manual intervention triggers
- **Workflow Resilience**: Camunda Operate for incident management, retries, and visibility

### **Performance Optimization**
- **Dashboard Analytics**: Hybrid approach combining pre-computed historical metrics with real-time operational queries
- **Customer Performance**: Intelligent caching strategies preventing degradation with 50+ concurrent users
- **Database Strategy**: Dedicated read replicas for reporting workloads separate from transactional processing
- **Audit Separation**: Isolated audit trail database for compliance queries without operational performance impact
- **Response Time Targets**: Sub-10-second dashboard responses, sub-3-second page loads on 3G connections
- **Report Performance**: JasperReports backed by read replicas and materialized views where needed

### **Security & Compliance Framework**
- **Offline Disbursement**: Dual-control authentication requiring both loan officer and senior staff credentials
- **Step-up Authentication**: Additional authentication required for sensitive operations with time-bound sessions
- **Document Compliance**: Comprehensive access logging for all customer documents with separate audit storage
- **Data Sovereignty**: Complete Zambian data residency with geo-redundant backup between certified providers
- **Audit Trail**: Immutable event sourcing with separate database for regulatory compliance isolation
- **Reporting Compliance**: Versioned BoZ templates and report parameter auditing via JasperReports

### **Business Continuity Features**
- **Offline Desktop Application**: Full .NET MAUI or WPF application with native performance and encrypted SQLite database
- **Two-way Delta Synchronization**: Timestamp-based sync mechanism with automatic conflict detection and resolution
- **Risk Management**: CEO-authorized offline limits with real-time exposure tracking and digital voucher generation
- **Local Data Encryption**: AES-256 encryption for SQLite database with automatic key rotation
- **Sync Conflict Resolution**: Intelligent algorithms with timestamp precedence and manual override capabilities
- **Unified Customer Model**: Cross-branch customer access with branch attribution for performance tracking
- **Workflow Continuity**: In-flight Camunda process instances survive service restarts and resume on reconnect

## Business Requirements Alignment

### **Regulatory Compliance**
- **Bank of Zambia (BoZ)**: Full supervisory compliance with automated prudential reporting
- **Zambian Data Protection Act**: Complete in-country data hosting with certified providers (Infratel/Paratus)
- **Credit Reporting Act**: TransUnion integration for new clients with cost optimization
- **Money-lenders Act**: Automated interest rate cap compliance and enforcement

### **Operational Requirements**
- **Primary Market**: Government employees via PMEC payroll deduction integration
- **Scale Planning**: Architecture supporting growth from 500 to 10,000+ loans without re-platforming
- **Branch Strategy**: Single Lusaka launch with future expansion to Copperbelt and Southern provinces
- **User Growth**: Support for scaling from 10-15 to 50+ concurrent users with maintained performance

### **Integration Requirements**
- **PMEC Integration**: Mission-critical government payroll system integration with queue resilience
- **Traditional Banking**: Bank transfer and cash disbursement workflows with comprehensive audit trails
- **Credit Assessment**: Selective TransUnion integration for first-time applicants only
- **SMS Communications**: Integrated gateway for automated notifications and payment reminders
- **Future Readiness**: Architecture prepared for Phase 2 mobile money integration via Tingg payment gateway

## Implementation Roadmap

### **Months 1-2: Foundation & Core Integrations**
- Core microservices architecture setup with Kubernetes deployment
- Camunda 8 platform (Zeebe + Operate) deployment and workflow engine configuration
- JasperReports Server setup with BoZ report template development
- BoZ compliance framework implementation and validation
- PMEC Anti-Corruption Layer development and integration testing
- TransUnion Zambia API integration with queue resilience implementation
- Infrastructure setup across both Zambian data centers with replication configuration

### **Months 3-4: Core Functionality Development**
- Camunda workflow orchestration implementation for loan processing and approvals
- Back-office loan workflow development with government employee focus
- Multi-branch architecture implementation with intelligent caching
- Integrated General Ledger development with hybrid analytics approach
- Traditional disbursement processing implementation (bank transfers and cash)
- JasperReports integration with core business reporting requirements
- Redis caching layer deployment for performance optimization

### **Months 5-6: Advanced Features & Security**
- CEO offline command center development with dual-control authentication
- Advanced Camunda workflows for collections and PMEC integration
- Comprehensive JasperReports suite for BoZ prudential reporting
- Collections workflow automation with PMEC integration
- Comprehensive security testing including penetration testing
- BoZ compliance validation and regulatory approval processes
- Semi-annual disaster recovery testing procedures establishment

### **Months 7-8: Deployment & Production Launch**
- Production environment setup with full geo-redundancy
- User acceptance testing and comprehensive staff training programs
- Soft launch with limited government employee payroll loan processing
- Performance optimization and monitoring system deployment
- Full production launch with ongoing support and monitoring

## Risk Mitigation Strategies

### **Technical Risks**
- **PMEC System Reliability**: Local queue system with automated retry mechanisms
- **Data Center Reliability**: Geo-redundant setup across two certified Zambian providers
- **Performance Degradation**: Intelligent caching and read replica strategies
- **Integration Failures**: Circuit breaker patterns and comprehensive monitoring
- **Security Vulnerabilities**: Regular penetration testing and automated security scanning
- **Workflow Incidents**: Camunda Operate-driven incident management and retries
- **Reporting Risks**: JasperReports template versioning and preflight validation

### **Operational Risks**
- **Staff Training**: Comprehensive training programs with ongoing support
- **Regulatory Changes**: Flexible architecture supporting configuration changes without code deployment
- **Business Continuity**: Offline operation capabilities with manual failover procedures
- **Audit Compliance**: Separate audit database with comprehensive logging and retention policies
- **Credential Management**: Automated rotation with HashiCorp Vault and secure backup procedures

## Success Metrics & Monitoring

### **Performance Metrics**
- **Response Time**: Sub-3 second page loads on 3G connections
- **Dashboard Performance**: Sub-10 second complex query responses
- **Availability**: 99.5% uptime SLA with 4-hour RTO for disaster recovery
- **Scalability**: Support for 50+ concurrent users without performance degradation
- **Cache Hit Rates**: >95% cache hit rate for frequently accessed customer data
- **Workflow Throughput**: Target >95% on-time task completion within SLA windows
- **Report SLAs**: 100% on-time BoZ reporting submissions

### **Business Metrics**
- **Loan Processing**: Seamless processing of 500-1,000 loans in Year 1
- **PMEC Integration**: >99% successful payroll deduction processing
- **Credit Bureau Optimization**: Cost reduction through intelligent first-time applicant routing
- **Regulatory Compliance**: 100% on-time BoZ prudential reporting submission
- **Operational Continuity**: Zero loan processing interruptions due to system downtime
```
}
