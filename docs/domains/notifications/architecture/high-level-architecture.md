# High Level Architecture

### Technical Summary

The LMS system is architected as a cloud-native, microservices-based platform deployed on Kubernetes within Zambia's borders, ensuring complete data sovereignty. The frontend is built with Next.js for tablet-optimized loan officer interfaces, while the backend uses .NET 9 microservices with Camunda 8 workflow orchestration. The system integrates with PMEC for government payroll deductions, TransUnion for credit assessments, and Tingg for mobile money processing, all orchestrated through a comprehensive workflow engine that enforces business rules and regulatory compliance.

### Platform and Infrastructure Choice

**Platform:** Kubernetes on Zambian Infrastructure (Infratel Primary, Paratus Secondary)
**Key Services:** SQL Server Always On, Redis Cluster, RabbitMQ, Camunda 8, JasperReports, MinIO, HashiCorp Vault
**Deployment Host and Regions:** Infratel Data Center (Lusaka) - Primary, Paratus Data Center (Lusaka) - Secondary

### Repository Structure

**Structure:** Monorepo with microservices architecture
**Monorepo Tool:** Nx workspace for .NET and Next.js coordination
**Package Organization:** Domain-driven microservices with shared libraries

### High Level Architecture Diagram

```mermaid
graph TB
    subgraph "Client Layer"
        WEB[NextJS Web App<br/>Tablet-Optimized]
        CEO[CEO Offline Desktop<br/>Electron + Dual Auth]
    end

    subgraph "Kubernetes Cluster - Zambian Infrastructure"
        subgraph "API Gateway & Load Balancing"
            LB[Load Balancer]
            GATEWAY[API Gateway<br/>.NET 9 Minimal APIs]
        end
        
        subgraph "Core Microservices"
            AUTH[Identity Service<br/>JWT + Step-up Auth]
            CLIENT[Client Management<br/>KYC/AML Compliance]
            LOAN[Loan Origination<br/>Multi-Product Workflow]
            CREDIT[Credit Assessment<br/>TransUnion Integration]
            GL[General Ledger<br/>BoZ Compliance]
            PMEC_SERVICE[PMEC Anti-Corruption<br/>Layer + Queue]
            COLLECTIONS[Collections Service<br/>Automated Lifecycle]
            COMMUNICATIONS[Communications Service<br/>Multi-Channel Notifications]
            WORKFLOW[Camunda 8 (Zeebe)<br/>Business Process Engine]
            REPORT[Reporting Service<br/>JasperReports Integration]
            SYNC[Offline Sync Service<br/>Conflict Resolution]
        end

        subgraph "Message Queues & Caching"
            SERVICEBUS[RabbitMQ<br/>In-Country Messaging]
            REDIS[Redis Cache<br/>Performance Layer]
        end
    end

    subgraph "External Integration Layer"
        PMEC[PMEC Government System<br/>Payroll Deductions]
        TRANSUNION[TransUnion Zambia<br/>Credit Bureau API]
        TINGG[Tingg Payment Gateway<br/>Mobile Money]
        SMS[SMS Gateway Provider]
        BANK[Local Banking Systems]
    end

    subgraph "Primary Data Center - Infratel"
        PRIMARY_SQL[(SQL Server Always On<br/>Primary + Read Replica)]
        AUDIT_DB[(Separate Audit Database<br/>Compliance Isolation)]
        MINIO_PRIMARY[MinIO Object Storage<br/>Document Management]
        VAULT_PRIMARY[HashiCorp Vault<br/>Secrets Management]
    end

    subgraph "Secondary Data Center - Paratus"
        SECONDARY_SQL[(SQL Server Always On<br/>Async Secondary - 1hr RPO)]
        AUDIT_BACKUP[(Audit Database<br/>Backup Instance)]
        MINIO_SECONDARY[MinIO Backup<br/>10-year Retention]
        VAULT_SECONDARY[HashiCorp Vault<br/>Backup Instance]
    end

    subgraph "Background Processing"
        JOBS[Background Services<br/>Compliance Automation]
        DR_TEST[Semi-Annual DR Testing<br/>Manual Procedures]
    end

    subgraph "Monitoring & Compliance"
        MONITOR[Application Insights<br/>Performance Metrics]
        LOGS[Centralized Logging<br/>Operational Events]
        AUDIT_MONITOR[Audit Trail Monitor<br/>BoZ Compliance]
    end

    WEB --> LB
    CEO -.->|Encrypted Sync<br/>Conflict Resolution| SYNC
    WEB --> REPORT
    
    LB --> GATEWAY
    GATEWAY --> AUTH
    
    AUTH --> CLIENT
    AUTH --> LOAN  
    AUTH --> CREDIT
    AUTH --> GL
    AUTH --> COLLECTIONS
    AUTH --> COMMUNICATIONS
    AUTH --> REPORT
    
    CLIENT --> REDIS
    GL --> REDIS

    %% Orchestration links
    LOAN --> WORKFLOW
    CREDIT --> WORKFLOW
    PMEC_SERVICE --> WORKFLOW
    COLLECTIONS --> WORKFLOW
    COMMUNICATIONS --> WORKFLOW
    SYNC --> WORKFLOW
    WORKFLOW --> REPORT
    
    LOAN --> PMEC_SERVICE
    CREDIT --> SERVICEBUS
    PMEC_SERVICE --> SERVICEBUS
    COLLECTIONS --> SERVICEBUS
    COMMUNICATIONS --> SERVICEBUS
    
    PMEC_SERVICE -.->|Queue Resilience| PMEC
    CREDIT --> TRANSUNION
    COLLECTIONS --> TINGG
    COMMUNICATIONS --> SMS
    GL --> BANK
    
    CLIENT --> PRIMARY_SQL
    LOAN --> PRIMARY_SQL
    CREDIT --> PRIMARY_SQL
    GL --> PRIMARY_SQL
    COLLECTIONS --> PRIMARY_SQL
    COMMUNICATIONS --> PRIMARY_SQL
    
    REPORT --> PRIMARY_SQL
    JOBS --> PRIMARY_SQL
    
    AUTH --> AUDIT_DB
    CLIENT --> AUDIT_DB
    LOAN --> AUDIT_DB
    PMEC_SERVICE --> AUDIT_DB
    COLLECTIONS --> AUDIT_DB
    COMMUNICATIONS --> AUDIT_DB
    
    CLIENT --> MINIO_PRIMARY
    LOAN --> MINIO_PRIMARY
    
    PRIMARY_SQL -.->|Async Replication<br/>Manual Failover| SECONDARY_SQL
    AUDIT_DB -.->|Replication| AUDIT_BACKUP
    MINIO_PRIMARY -.->|Geo Replication| MINIO_SECONDARY
    VAULT_PRIMARY -.->|Backup Sync| VAULT_SECONDARY
    
    PMEC_SERVICE --> VAULT_PRIMARY
    CREDIT --> VAULT_PRIMARY
    TINGG --> VAULT_PRIMARY
    
    JOBS --> REDIS
    
    DR_TEST -.->|Semi-Annual<br/>Manual Testing| SECONDARY_SQL
    
    CLIENT --> MONITOR
    LOAN --> MONITOR
    GL --> MONITOR
    PMEC_SERVICE --> MONITOR
    COLLECTIONS --> MONITOR
    COMMUNICATIONS --> MONITOR
    
    AUDIT_DB --> AUDIT_MONITOR
    AUDIT_BACKUP --> AUDIT_MONITOR
```

### Architectural Patterns

- **Microservices Architecture:** Domain-driven services with clear boundaries - _Rationale:_ Enables independent scaling, deployment, and maintenance of complex business domains
- **Event-Driven Architecture:** Camunda 8 orchestration with RabbitMQ messaging - _Rationale:_ Ensures reliable business process execution and system resilience
- **CQRS Pattern:** Command/Query separation for loan origination - _Rationale:_ Optimizes read/write operations and enables complex business rule enforcement
- **Anti-Corruption Layer:** PMEC integration isolation - _Rationale:_ Protects core business logic from external system changes and failures
- **Repository Pattern:** Data access abstraction - _Rationale:_ Enables testing and future database migration flexibility
- **API Gateway Pattern:** Single entry point for all API calls - _Rationale:_ Centralized auth, rate limiting, and monitoring
- **Circuit Breaker Pattern:** External API resilience - _Rationale:_ Prevents cascade failures from external system outages
- **Saga Pattern:** Distributed transaction management - _Rationale:_ Ensures data consistency across microservices for complex business processes
