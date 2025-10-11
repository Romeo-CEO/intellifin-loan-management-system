# System Administration Control Plane - Architecture Document

## Document Overview

**Project**: IntelliFin Loan Management System - System Administration Control Plane Enhancement  
**Document Type**: Architecture Document  
**Version**: 1.0  
**Date**: 2025-10-11  
**Author**: Technical Architecture Team  
**Status**: In Review

---

## Change Log

| Change | Date | Version | Description | Author |
|--------|------|---------|-------------|---------|
| Initial Architecture | 2025-10-11 | 1.0 | Complete architecture document for System Administration Control Plane | Architecture Team |

---

## Related Documents

- **PRD**: `docs/domains/system-administration/system-administration-control-plane-prd.md`
- **Brownfield Analysis**: `docs/domains/system-administration/system-administration-brownfield-analysis.md`
- **ADRs**: `docs/domains/system-administration/adrs/` (individual ADR files)

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [System Context](#2-system-context)
3. [Architecture Overview](#3-architecture-overview)
4. [Component Architecture](#4-component-architecture)
5. [Data Architecture](#5-data-architecture)
6. [Integration Architecture](#6-integration-architecture)
7. [Security Architecture](#7-security-architecture)
8. [Observability Architecture](#8-observability-architecture)
9. [Deployment Architecture](#9-deployment-architecture)
10. [Architectural Decision Records (ADRs)](#10-architectural-decision-records-adrs)
11. [Migration Strategy](#11-migration-strategy)
12. [Quality Attributes](#12-quality-attributes)

---

## 1. Executive Summary

### 1.1 Architectural Vision

The System Administration Control Plane represents a fundamental architectural evolution from a **tactical support layer** to a **strategic governance orchestration platform**. This transformation introduces a dedicated Admin microservice that coordinates identity (Keycloak), access control (RBAC with SoD), audit (tamper-evident chain), configuration (GitOps + Vault), and observability (OpenTelemetry stack) across all IntelliFin services.

**Key Architectural Principles**:

1. **Separation of Concerns**: Extract identity provider from IdentityService, centralize audit from FinancialService, establish Admin microservice as single governance orchestrator
2. **Defense in Depth**: Layer security with mTLS, NetworkPolicies, JIT access, step-up MFA, and PAM
3. **Data Sovereignty**: Deploy all observability infrastructure in-country (Zambia) for compliance
4. **Zero Trust**: Assume breach - verify every service-to-service call, time-bound all access
5. **Immutable Audit**: WORM storage + cryptographic chaining for regulatory compliance
6. **Policy as Code**: Automate governance decisions through workflows and policy engines

### 1.2 Architecture Transformation Summary

**Before (V1)**:
```
┌─────────────────────────────────────────────────────────────┐
│                    API Gateway (Yarp)                       │
│              ASP.NET Identity JWT Validation                │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
┌───────▼────────┐   ┌───────▼────────┐   ┌───────▼────────┐
│ IdentityService│   │ LoanOrigination│   │ FinancialService│
│  (ASP.NET ID)  │   │                │   │  (Audit ❌)     │
└────────────────┘   └────────────────┘   └─────────────────┘
        │
        ▼
  SQL Server (users)    No observability ❌
                        No PAM ❌
```

**After (V2 - Target Architecture)**:
```
                    ┌──────────────────────────┐
                    │   Observability Stack    │
                    │  Prometheus │ Grafana    │
                    │  Jaeger │ Loki          │
                    └──────────┬───────────────┘
                               │ OTLP
    ┌──────────────────────────┼──────────────────────────┐
    │                          │                          │
┌───▼────────────┐   ┌─────────▼──────────┐   ┌─────────▼────────┐
│   Keycloak     │◄──│   Admin Service    │◄──│   API Gateway    │
│  (OIDC IdP)    │   │ (Control Plane Hub)│   │ (JWT Validation) │
└────┬───────────┘   └─────────┬──────────┘   └─────────┬────────┘
     │                          │                        │
     │ Federation      ┌────────┴────────┐              │
     │                 │                 │              │
┌────▼───────┐   ┌────▼────────┐  ┌─────▼──────┐      │
│  Azure AD  │   │   Camunda   │  │   Vault    │      │
│  B2C (opt) │   │ (Workflows) │  │ (Secrets)  │      │
└────────────┘   └─────────────┘  └────────────┘      │
                                                       │
        ┌──────────────────────────┬───────────────────┤
        │                          │                   │
┌───────▼────────┐   ┌─────────────▼───┐   ┌─────────▼────────┐
│ IdentityService│   │ LoanOrigination │   │ FinancialService │
│  (Delegated)   │   │  + OpenTelemetry│   │  (Audit Removed) │
└────────────────┘   └─────────────────┘   └──────────────────┘
        │                     │                       │
        └─────────────────────┴───────────────────────┘
                              │ mTLS
                      ┌───────▼────────┐
                      │  NetworkPolicy │
                      │ (Micro-segment)│
                      └────────────────┘
```

### 1.3 Technology Stack Decision Matrix

|| Component | Technology | Rationale | ADR Reference |
||-----------|-----------|-----------|---------------|
|| **Identity Provider** | Keycloak 24+ | Self-hosted, OIDC/OAuth2, federation support, mature admin API | ADR-001 |
|| **Distributed Tracing** | Jaeger | OpenTelemetry native, proven scale, Zambian data sovereignty | ADR-002 |
|| **Centralized Logging** | Loki | Cost-effective, LogQL query, integrates with Prometheus/Grafana | ADR-003 |
|| **Metrics** | Prometheus + Grafana | Industry standard, Kubernetes native, rich ecosystem | ADR-004 |
|| **Telemetry SDK** | OpenTelemetry | Vendor-neutral, future-proof, single instrumentation | ADR-005 |
|| **Service Mesh** | Linkerd (automatic mTLS) | Automatic mTLS with low ops complexity and policy enforcement; aligns with zero-trust | ADR-006 |
|| **GitOps** | ArgoCD | Kubernetes native, declarative, audit trail, rollback | ADR-007 |
|| **PAM Solution** | Teleport (selected) | Keycloak SSO, JIT access, session recording | ADR-008 |
|| **Container Signing** | Cosign + Syft | CNCF standard, SBOM generation, OCI registry compatible | ADR-009 |
|| **Audit Storage** | MinIO (WORM mode) | S3-compatible, on-prem, object locking, 10-year retention | ADR-010 |

---

## 2. System Context

### 2.1 Context Diagram

```
┌──────────────────────────────────────────────────────────────────────┐
│                     IntelliFin Loan Management System                 │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │          System Administration Control Plane                  │   │
│  │                                                                │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐          │   │
│  │  │  Keycloak   │  │    Admin    │  │ Observability│          │   │
│  │  │    (IdP)    │  │   Service   │  │    Stack     │          │   │
│  │  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘          │   │
│  │         │                │                │                   │   │
│  │         └────────────────┴────────────────┘                   │   │
│  │                          │                                     │   │
│  └──────────────────────────┼─────────────────────────────────────┘   │
│                             │                                         │
│  ┌──────────────────────────┼─────────────────────────────────────┐  │
│  │               Business Microservices Layer                      │  │
│  │                                                                 │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐        │  │
│  │  │IdentityService│  │ Loan Origin │  │ Collections  │  ...   │  │
│  │  └──────────────┘  └──────────────┘  └──────────────┘        │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                        │
└──────────────────────────────────────────────────────────────────────┘
                                │
        ┌───────────────────────┼───────────────────────┐
        │                       │                       │
┌───────▼────────┐   ┌──────────▼──────────┐   ┌──────▼──────────┐
│  Branch Users  │   │  External Systems   │   │  Administrators │
│  (Next.js UI)  │   │  PMEC │ TransUnion  │   │  (Admin Portal) │
└────────────────┘   └─────────────────────┘   └─────────────────┘
```

### 2.2 Actors and External Systems

**Internal Actors**:
- **Branch Users**: Loan officers, collections officers, customer service (600-800 users)
- **Administrators**: System admins, compliance officers, auditors (20-30 users)
- **DevOps Engineers**: Infrastructure access via PAM (5-10 users)
- **Managers**: Approval workflows for JIT elevation, access recertification (30-50 users)
- **CEO**: Offline desktop app user with audit merge requirement (1 user)

**External Systems**:
- **PMEC (Zambia)**: Government employee payroll integration (existing)
- **TransUnion Zambia**: Credit bureau integration (existing)
- **Tingg (Cellulant)**: Payment gateway integration (existing)
- **Africa's Talking**: SMS gateway (existing)
- **Azure AD B2C**: Optional enterprise SSO federation (future)

**Infrastructure Systems**:
- **Kubernetes Cluster**: Primary compute orchestration (on-prem Zambia)
- **SQL Server 2022**: Primary data store (Always On HA)
- **PostgreSQL**: Keycloak identity store (new)
- **Redis**: Token family tracking, caching (existing, expanded)
- **RabbitMQ**: Async messaging, audit event transport (existing)
- **MinIO**: Document storage, audit WORM, session recordings (existing, expanded)

---

## 3. Architecture Overview

### 3.1 Logical Architecture Layers

```
┌─────────────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                                │
│  ┌───────────────────┐  ┌───────────────────┐  ┌─────────────────┐ │
│  │  Next.js Frontend │  │  Admin Portal UI  │  │ CEO Desktop App │ │
│  │   (Branch Users)  │  │  (Administrators) │  │ (MAUI/Offline)  │ │
│  └───────────────────┘  └───────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
                                  │
┌─────────────────────────────────┼───────────────────────────────────┐
│                    API GATEWAY LAYER                                 │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │              Yarp API Gateway (ASP.NET Core)                  │  │
│  │  • Dual JWT Validation (ASP.NET ID + Keycloak)               │  │
│  │  • Rate Limiting │ Correlation ID │ Branch Claim Extraction  │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
                                  │
┌─────────────────────────────────┼───────────────────────────────────┐
│              CONTROL PLANE LAYER (New Architecture)                  │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                     Admin Microservice                        │  │
│  │  • User/Role Management • Audit Orchestration                │  │
│  │  • JIT Elevation • Config Policy • PAM Gateway               │  │
│  └────────┬──────────────────────────────┬──────────────────────┘  │
│           │                              │                          │
│  ┌────────▼─────────┐          ┌─────────▼──────────┐             │
│  │    Keycloak      │          │  Observability     │             │
│  │  Identity Provider│          │  (OTLP Collector) │             │
│  └──────────────────┘          └────────────────────┘             │
└─────────────────────────────────────────────────────────────────────┘
                                  │
┌─────────────────────────────────┼───────────────────────────────────┐
│                  BUSINESS SERVICES LAYER                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │
│  │ IdentityService │  │LoanOrigination│  │ Financial    │             │
│  │  (Refactored) │  │               │  │  Service     │             │
│  └──────────────┘  └──────────────┘  └──────────────┘             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │
│  │ClientMgmt    │  │Communications │  │ Collections  │   + 3 more  │
│  └──────────────┘  └──────────────┘  └──────────────┘             │
│                                                                       │
│  All services instrumented with OpenTelemetry                       │
│  All inter-service calls use mTLS (Linkerd service mesh)            │
└─────────────────────────────────────────────────────────────────────┘
                                  │
┌─────────────────────────────────┼───────────────────────────────────┐
│                  INFRASTRUCTURE LAYER                                │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │
│  │ SQL Server   │  │  PostgreSQL  │  │    Redis     │             │
│  │ (Business)   │  │  (Keycloak)  │  │  (Caching)   │             │
│  └──────────────┘  └──────────────┘  └──────────────┘             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │
│  │  RabbitMQ    │  │    Vault     │  │    MinIO     │             │
│  │ (Messaging)  │  │  (Secrets)   │  │ (WORM/Docs)  │             │
│  └──────────────┘  └──────────────┘  └──────────────┘             │
│                                                                       │
│  Kubernetes 1.28+ │ NetworkPolicies │ Helm │ ArgoCD                │
└─────────────────────────────────────────────────────────────────────┘
```

### 3.2 Component Interaction Flow

**Typical Authentication Flow (Phase 1-2)**:
```
User (Browser)
    │
    │ 1. Navigate to app
    ▼
Next.js App
    │
    │ 2. Redirect to login
    ▼
Keycloak (IdP)
    │ 3. OIDC auth
    │ 4. Generate JWT (branch claims + roles)
    │
    ▼
Next.js App
    │ 5. Store tokens
    │
    │ 6. API call (Bearer token)
    ▼
API Gateway
    │ 7. Validate JWT signature (JWKS)
    │ 8. Extract branch claims
    │ 9. Add correlation ID
    │
    ▼
Business Service (e.g., LoanOrigination)
    │ 10. Authorize via branch claims (no DB query)
    │ 11. Process request
    │ 12. Emit audit event → Admin Service
    │ 13. Emit OpenTelemetry trace → Jaeger
    │
    ▼
Response to User
```

**JIT Privilege Elevation Flow (Phase 4)**:
```
Developer (Admin UI)
    │
    │ 1. Request elevation (role, justification, duration)
    ▼
Admin Service
    │
    │ 2. Create Camunda workflow instance
    ▼
Camunda (Zeebe)
    │
    │ 3. Human task → Manager notification (SignalR)
    ▼
Manager (Admin UI)
    │
    │ 4. Approve/reject decision
    ▼
Admin Service
    │
    │ 5. Call Keycloak Admin API
    ▼
Keycloak
    │ 6. Create temporary role mapping (TTL metadata)
    │
    ▼
Developer (receives new JWT on next refresh)
    │
    │ 7. Scheduled job (every 5 min)
    ▼
Admin Service
    │ 8. Check expired elevations → Revoke in Keycloak
    │
    ▼
Audit Trail (tamper-evident chain in MinIO)
```

---

## 4. Component Architecture

### 4.1 Admin Microservice Architecture

**Responsibility**: Governance orchestration hub coordinating identity, access, audit, configuration, and PAM across all services.

#### 4.1.1 Component Diagram

```
┌────────────────────────────────────────────────────────────────────┐
│                    IntelliFin.AdminService                          │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐ │
│  │                    API Controllers                            │ │
│  │  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐ │ │
│  │  │ UserMgmt       │  │  RoleMgmt      │  │ AccessGov      │ │ │
│  │  │ Controller     │  │  Controller    │  │ Controller     │ │ │
│  │  └────────────────┘  └────────────────┘  └────────────────┘ │ │
│  │  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐ │ │
│  │  │ AuditTrail     │  │  Configuration │  │ PAM            │ │ │
│  │  │ Controller     │  │  Controller    │  │ Controller     │ │ │
│  │  └────────────────┘  └────────────────┘  └────────────────┘ │ │
│  └──────────────────────────────────────────────────────────────┘ │
│                                │                                    │
│  ┌──────────────────────────────────────────────────────────────┐ │
│  │                    Business Services                          │ │
│  │  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐ │ │
│  │  │ KeycloakAdmin  │  │ AuditChain     │  │ PolicyEngine   │ │ │
│  │  │ Service        │  │ Service        │  │ Service        │ │ │
│  │  └────────────────┘  └────────────────┘  └────────────────┘ │ │
│  │  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐ │ │
│  │  │ CamundaClient  │  │ VaultClient    │  │ MinIOClient    │ │ │
│  │  │ Service        │  │ Service        │  │ Service        │ │ │
│  │  └────────────────┘  └────────────────┘  └────────────────┘ │ │
│  └──────────────────────────────────────────────────────────────┘ │
│                                │                                    │
│  ┌──────────────────────────────────────────────────────────────┐ │
│  │                    Data Access Layer                          │ │
│  │  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐ │ │
│  │  │ AdminDbContext │  │ AuditEventRepo │  │ PolicyRepo     │ │ │
│  │  │ (EF Core)      │  │                │  │                │ │ │
│  │  └────────────────┘  └────────────────┘  └────────────────┘ │ │
│  └──────────────────────────────────────────────────────────────┘ │
│                                │                                    │
└────────────────────────────────┼────────────────────────────────────┘
                                 │
        ┌────────────────────────┼────────────────────────┐
        │                        │                        │
┌───────▼────────┐   ┌───────────▼──────────┐   ┌───────▼────────┐
│ SQL Server     │   │     Keycloak         │   │    Camunda     │
│ AdminService DB│   │   (Admin API)        │   │  (REST API)    │
└────────────────┘   └──────────────────────┘   └────────────────┘
```

#### 4.1.2 Database Schema (AdminService)

**Database**: `IntelliFin_AdminService` (SQL Server 2022)

```sql
-- Policy Configuration
CREATE TABLE ConfigurationPolicies (
    PolicyId UNIQUEIDENTIFIER PRIMARY KEY,
    PolicyName NVARCHAR(100) NOT NULL,
    ConfigKey NVARCHAR(200) NOT NULL UNIQUE,
    RequiresApproval BIT NOT NULL DEFAULT 1,
    ApprovalWorkflowKey NVARCHAR(100), -- Camunda process key
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy NVARCHAR(100) NOT NULL
);

-- SoD (Segregation of Duties) Rules
CREATE TABLE SodRules (
    RuleId UNIQUEIDENTIFIER PRIMARY KEY,
    ConflictingRole1 NVARCHAR(100) NOT NULL, -- Keycloak role name
    ConflictingRole2 NVARCHAR(100) NOT NULL,
    Severity NVARCHAR(20) NOT NULL, -- 'Critical', 'High', 'Medium'
    AllowExceptions BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- SoD Exception Approvals
CREATE TABLE SodExceptions (
    ExceptionId UNIQUEIDENTIFIER PRIMARY KEY,
    RuleId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES SodRules(RuleId),
    UserId NVARCHAR(100) NOT NULL, -- Keycloak user UUID
    Justification NVARCHAR(500) NOT NULL,
    ApprovedBy NVARCHAR(100) NOT NULL, -- Compliance Officer
    ApprovedAt DATETIME2 NOT NULL,
    ExpiresAt DATETIME2 NULL,
    RevokedAt DATETIME2 NULL
);

-- JIT Elevation History
CREATE TABLE ElevationHistory (
    ElevationId UNIQUEIDENTIFIER PRIMARY KEY,
    UserId NVARCHAR(100) NOT NULL,
    RequestedRoles NVARCHAR(500) NOT NULL, -- JSON array
    Justification NVARCHAR(500) NOT NULL,
    RequestedDurationMinutes INT NOT NULL,
    RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ApprovedBy NVARCHAR(100) NULL,
    ApprovedAt DATETIME2 NULL,
    RejectedBy NVARCHAR(100) NULL,
    RejectedAt DATETIME2 NULL,
    ActivatedAt DATETIME2 NULL,
    ExpiredAt DATETIME2 NULL,
    RevokedBy NVARCHAR(100) NULL,
    RevokedAt DATETIME2 NULL,
    Status NVARCHAR(20) NOT NULL -- 'Pending', 'Approved', 'Rejected', 'Active', 'Expired', 'Revoked'
);

-- Audit Events (active 90-day window)
CREATE TABLE AuditEvents (
    EventId BIGINT IDENTITY(1,1) PRIMARY KEY,
    EventGuid UNIQUEIDENTIFIER NOT NULL UNIQUE DEFAULT NEWID(),
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CorrelationId NVARCHAR(100) NOT NULL, -- W3C trace-id
    Actor NVARCHAR(100) NOT NULL, -- User ID or service name
    Action NVARCHAR(100) NOT NULL, -- 'UserCreated', 'LoanApproved', etc.
    EntityType NVARCHAR(100) NOT NULL,
    EntityId NVARCHAR(100) NOT NULL,
    Details NVARCHAR(MAX) NULL, -- JSON
    IpAddress NVARCHAR(50) NULL,
    UserAgent NVARCHAR(500) NULL,
    
    -- Tamper-evident chain
    PreviousEventHash NVARCHAR(64) NULL, -- SHA-256 of previous event
    CurrentEventHash NVARCHAR(64) NOT NULL, -- SHA-256 of this event
    
    -- MinIO archival
    ArchivedToMinIO BIT NOT NULL DEFAULT 0,
    ArchivedAt DATETIME2 NULL,
    MinIOObjectKey NVARCHAR(200) NULL
);

-- Index for correlation ID lookups
CREATE INDEX IX_AuditEvents_CorrelationId ON AuditEvents(CorrelationId);
CREATE INDEX IX_AuditEvents_Timestamp ON AuditEvents(Timestamp DESC);
CREATE INDEX IX_AuditEvents_Actor ON AuditEvents(Actor);

-- User ID Mapping (for backward compatibility with ASP.NET Identity)
CREATE TABLE UserIdMapping (
    AspNetUserId NVARCHAR(128) PRIMARY KEY, -- Old ASP.NET Identity ID
    KeycloakUserId NVARCHAR(100) NOT NULL UNIQUE, -- Keycloak UUID
    MappedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- PAM Session Recordings
CREATE TABLE PamSessions (
    SessionId UNIQUEIDENTIFIER PRIMARY KEY,
    UserId NVARCHAR(100) NOT NULL,
    TargetServer NVARCHAR(200) NOT NULL,
    AccessType NVARCHAR(20) NOT NULL, -- 'SSH', 'RDP', 'kubectl'
    RequestJustification NVARCHAR(500) NOT NULL,
    ApprovedBy NVARCHAR(100) NULL,
    SessionStartedAt DATETIME2 NOT NULL,
    SessionEndedAt DATETIME2 NULL,
    RecordingMinIOKey NVARCHAR(200) NULL,
    RecordingUploadedAt DATETIME2 NULL,
    DurationSeconds INT NULL
);
```

#### 4.1.3 API Endpoints

**User Management**:
```
GET    /api/admin/users                    # List users (paginated, filterable)
GET    /api/admin/users/{id}               # Get user details
POST   /api/admin/users                    # Create user (calls Keycloak)
PUT    /api/admin/users/{id}               # Update user
DELETE /api/admin/users/{id}               # Soft delete (disable in Keycloak)
POST   /api/admin/users/{id}/reset-password # Force password reset
```

**Role Management**:
```
GET    /api/admin/roles                    # List roles
POST   /api/admin/roles                    # Create role (Keycloak realm role)
PUT    /api/admin/roles/{id}               # Update role
DELETE /api/admin/roles/{id}               # Delete role
GET    /api/admin/roles/{id}/permissions   # Get role permissions
POST   /api/admin/users/{id}/roles         # Assign role (SoD validation)
DELETE /api/admin/users/{userId}/roles/{roleId} # Revoke role
```

**Access Governance**:
```
POST   /api/admin/access/elevate           # Request JIT elevation
GET    /api/admin/access/elevations        # List active elevations
POST   /api/admin/access/elevations/{id}/approve # Approve elevation
POST   /api/admin/access/elevations/{id}/reject  # Reject elevation
DELETE /api/admin/access/elevations/{id}/revoke  # Revoke active elevation
GET    /api/admin/access/recertification   # Get recertification campaign status
POST   /api/admin/access/recertification/{userId}/approve # Approve user access
```

**Audit Trail**:
```
GET    /api/admin/audit/events             # Query audit events (filters: date, actor, action)
GET    /api/admin/audit/events/{id}        # Get event details
POST   /api/admin/audit/events             # Ingest audit event (from services)
POST   /api/admin/audit/merge-offline      # Merge offline audit batch (CEO app)
POST   /api/admin/audit/verify-integrity   # Verify tamper-evident chain
GET    /api/admin/audit/reports/boz        # Generate BoZ compliance report
```

**Configuration Management**:
```
GET    /api/admin/config/policies          # List configuration policies
POST   /api/admin/config/policies          # Create/update policy
POST   /api/admin/config/changes           # Request config change (triggers workflow)
GET    /api/admin/config/changes/pending   # List pending changes
POST   /api/admin/config/changes/{id}/approve # Approve config change
POST   /api/admin/config/rollback          # Rollback last config change
```

**PAM (Privileged Access Management)**:
```
POST   /api/admin/pam/access-request       # Request infrastructure access
GET    /api/admin/pam/sessions             # List PAM sessions (active + history)
GET    /api/admin/pam/sessions/{id}/recording # Get session recording URL
POST   /api/admin/pam/sessions/{id}/terminate # Emergency session termination
```

### 4.2 Keycloak Identity Provider Architecture

**Version**: Keycloak 24.0+ (latest LTS)

#### 4.2.1 Realm Configuration

**Realm Name**: `intellifin`

**Realm Settings**:
```yaml
realm: intellifin
enabled: true
sslRequired: external
registrationAllowed: false  # Admin-only user creation
loginWithEmailAllowed: true
duplicateEmailsAllowed: false
resetPasswordAllowed: true
editUsernameAllowed: false
bruteForceProtected: true
maxFailureWaitSeconds: 900  # 15 minutes lockout
passwordPolicy: "length(12) and digits(1) and upperCase(1) and specialChars(1) and notUsername"

# Token settings
accessTokenLifespan: 900            # 15 minutes (matches current)
refreshTokenMaxReuse: 0             # Rotation enabled
revokeRefreshToken: true            # Rotation: issue new, revoke old
refreshTokenReuseInterval: 0
ssoSessionIdleTimeout: 1800         # 30 minutes (matches current)
ssoSessionMaxLifespan: 43200        # 12 hours

# Branch-scoped claims
attributes:
  branchScope: true
```

#### 4.2.2 Client Configurations

**Admin Service Client**:
```yaml
clientId: admin-service
clientAuthenticatorType: client-secret
serviceAccountsEnabled: true         # For Admin API calls
authorizationServicesEnabled: false
standardFlowEnabled: false
directAccessGrantsEnabled: false
protocol: openid-connect
```

**API Gateway Client**:
```yaml
clientId: api-gateway
clientAuthenticatorType: client-secret
publicClient: false
standardFlowEnabled: true
directAccessGrantsEnabled: true
protocol: openid-connect
redirectUris:
  - "https://lms.intellifin.zm/*"
  - "http://localhost:3000/*"  # Dev
webOrigins:
  - "https://lms.intellifin.zm"
  - "http://localhost:3000"
protocolMappers:
  - name: branch-mapper
    protocol: openid-connect
    protocolMapper: oidc-usermodel-attribute-mapper
    config:
      user.attribute: branchId
      claim.name: branchId
      jsonType.label: String
      id.token.claim: true
      access.token.claim: true
  - name: branch-name-mapper
    protocol: openid-connect
    protocolMapper: oidc-usermodel-attribute-mapper
    config:
      user.attribute: branchName
      claim.name: branchName
      jsonType.label: String
      access.token.claim: true
  - name: branch-region-mapper
    protocol: openid-connect
    protocolMapper: oidc-usermodel-attribute-mapper
    config:
      user.attribute: branchRegion
      claim.name: branchRegion
      jsonType.label: String
      access.token.claim: true
  - name: allowed-branches-mapper
    protocol: openid-connect
    protocolMapper: oidc-usermodel-attribute-mapper
    config:
      user.attribute: allowedBranches
      claim.name: allowedBranches
      jsonType.label: String
      multivalued: true
      access.token.claim: true
```

#### 4.2.3 Role Hierarchy

```
IntelliFin Roles (Keycloak Realm Roles)

┌───────────────┐
│      CEO      │  (Super Admin)
└───────┬───────┘
        │
    ┌───┴───┬───────┬───────┬──────────┬──────────────┐
    │       │       │       │          │              │
┌───▼───┐ ┌─▼─────┐ ┌──▼───┐ ┌────▼────┐ ┌──────▼───────┐ ┌───▼─────┐
│ CFO   │ │ CTO   │ │ Head │ │ Head    │ │ Compliance   │ │ Risk    │
│       │ │       │ │ Ops  │ │ Credit  │ │ Officer      │ │ Manager │
└───┬───┘ └───┬───┘ └──┬───┘ └────┬────┘ └──────┬───────┘ └───┬─────┘
    │         │        │          │              │             │
┌───▼─────────┴────────┴──────────┴──────────────┴─────────────▼───┐
│                   Branch Manager                                   │
└───┬───────────────────┬────────────────────┬───────────────────┬──┘
    │                   │                    │                   │
┌───▼────────┐  ┌───────▼────────┐  ┌───────▼──────┐  ┌────────▼──────┐
│ Loan       │  │ Collections    │  │ Treasury     │  │ GL Accountant │
│ Officer    │  │ Officer        │  │ Officer      │  │               │
└────────────┘  └────────────────┘  └──────────────┘  └───────────────┘

┌────────────────────────────────────────────────────────────────────┐
│  Support Roles (No hierarchy)                                       │
│  • Auditor (read-only audit access)                                │
│  • Customer Service Rep (view-only loans)                          │
│  • Reports User (view-only dashboards)                             │
└────────────────────────────────────────────────────────────────────┘
```

**Composite Roles** (Phase 4):
```yaml
# Branch Manager includes:
- branch-manager
  - loan-officer  # Can perform loan officer duties
  - collections-officer  # Can perform collections
  - reports-user  # Can view reports
```

#### 4.2.4 Federation with Azure AD B2C (Optional - Phase 1)

```yaml
identityProviders:
  - alias: azure-ad-b2c
    providerId: oidc
    enabled: true
    config:
      clientId: "${AZURE_AD_CLIENT_ID}"
      clientSecret: "${AZURE_AD_CLIENT_SECRET}"
      authorizationUrl: "https://{tenant}.b2clogin.com/{tenant}.onmicrosoft.com/{policy}/oauth2/v2.0/authorize"
      tokenUrl: "https://{tenant}.b2clogin.com/{tenant}.onmicrosoft.com/{policy}/oauth2/v2.0/token"
      logoutUrl: "https://{tenant}.b2clogin.com/{tenant}.onmicrosoft.com/{policy}/oauth2/v2.0/logout"
      userInfoUrl: "https://{tenant}.b2clogin.com/{tenant}.onmicrosoft.com/{policy}/openidconnect/v2.0/userinfo"
      defaultScope: "openid profile email"
      syncMode: IMPORT  # Create local Keycloak user on first login
```

### 4.3 Observability Stack Architecture

#### 4.3.1 OpenTelemetry Instrumentation

**Shared Library**: `IntelliFin.Shared.Observability`

```csharp
// Program.cs extension
public static IServiceCollection AddOpenTelemetryInstrumentation(
    this IServiceCollection services,
    IConfiguration configuration,
    string serviceName)
{
    var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"] 
        ?? "http://otel-collector:4317";
    
    services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(serviceName)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = configuration["Environment"] ?? "Production",
                ["service.version"] = Assembly.GetExecutingAssembly()
                    .GetName().Version?.ToString() ?? "unknown",
                ["service.namespace"] = "IntelliFin.LMS"
            }))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequest = (activity, request) =>
                {
                    activity.SetTag("http.user_agent", request.Headers.UserAgent.ToString());
                };
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.SetDbStatementForText = true;
                options.SetDbStatementForStoredProcedure = true;
            })
            .AddSource("IntelliFin.*")
            .SetSampler(new AdaptiveSampler()) // Custom: 100% errors, 10% normal
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
                options.Protocol = OtlpExportProtocol.Grpc;
            }))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddMeter("IntelliFin.*")
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
                options.Protocol = OtlpExportProtocol.Grpc;
            }))
        .WithLogging(logging => logging
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
                options.Protocol = OtlpExportProtocol.Grpc;
            }));

    // Correlation ID middleware
    services.AddSingleton<CorrelationIdMiddleware>();
    
    return services;
}

// Custom adaptive sampler
public class AdaptiveSampler : Sampler
{
    private readonly AlwaysOnSampler _alwaysOn = new AlwaysOnSampler();
    private readonly TraceIdRatioBasedSampler _ratio = new TraceIdRatioBasedSampler(0.1);

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        // Always sample errors
        if (samplingParameters.Tags != null)
        {
            foreach (var tag in samplingParameters.Tags)
            {
                if (tag.Key == "error" || tag.Key == "exception")
                {
                    return _alwaysOn.ShouldSample(samplingParameters);
                }
            }
        }

        // 10% sampling for normal requests
        return _ratio.ShouldSample(samplingParameters);
    }
}
```

#### 4.3.2 Observability Stack Deployment

```yaml
# Kubernetes namespace
apiVersion: v1
kind: Namespace
metadata:
  name: observability

---
# OpenTelemetry Collector (DaemonSet)
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: otel-collector
  namespace: observability
spec:
  selector:
    matchLabels:
      app: otel-collector
  template:
    metadata:
      labels:
        app: otel-collector
    spec:
      containers:
      - name: otel-collector
        image: otel/opentelemetry-collector-contrib:0.96.0
        ports:
        - containerPort: 4317  # OTLP gRPC
        - containerPort: 4318  # OTLP HTTP
        volumeMounts:
        - name: otel-config
          mountPath: /etc/otel
        env:
        - name: JAEGER_ENDPOINT
          value: "jaeger-collector.observability.svc.cluster.local:14250"
        - name: PROMETHEUS_ENDPOINT
          value: "prometheus-server.observability.svc.cluster.local:9090"
        - name: LOKI_ENDPOINT
          value: "loki.observability.svc.cluster.local:3100"
      volumes:
      - name: otel-config
        configMap:
          name: otel-collector-config

---
# OpenTelemetry Collector Config
apiVersion: v1
kind: ConfigMap
metadata:
  name: otel-collector-config
  namespace: observability
data:
  config.yaml: |
    receivers:
      otlp:
        protocols:
          grpc:
            endpoint: 0.0.0.0:4317
          http:
            endpoint: 0.0.0.0:4318
    
    processors:
      batch:
        timeout: 10s
        send_batch_size: 1024
      
      # PII redaction for Zambian Data Protection Act compliance
      attributes:
        actions:
          - key: nrc_number
            action: delete
          - key: phone
            pattern: ^\+260\d{9}$
            action: hash
          - key: email
            pattern: .*@.*
            action: hash
      
      memory_limiter:
        check_interval: 1s
        limit_mib: 512
    
    exporters:
      otlp/jaeger:
        endpoint: ${JAEGER_ENDPOINT}
        tls:
          insecure: true
      
      prometheusremotewrite:
        endpoint: ${PROMETHEUS_ENDPOINT}/api/v1/write
        tls:
          insecure: true
      
      loki:
        endpoint: ${LOKI_ENDPOINT}/loki/api/v1/push
        tls:
          insecure: true
    
    service:
      pipelines:
        traces:
          receivers: [otlp]
          processors: [memory_limiter, batch, attributes]
          exporters: [otlp/jaeger]
        
        metrics:
          receivers: [otlp]
          processors: [memory_limiter, batch]
          exporters: [prometheusremotewrite]
        
        logs:
          receivers: [otlp]
          processors: [memory_limiter, batch, attributes]
          exporters: [loki]
```

#### 4.3.3 Grafana Dashboard Definitions

**BoZ Compliance Dashboard** (`BoZ-Compliance-Overview.json`):
```json
{
  "dashboard": {
    "title": "Bank of Zambia Compliance Overview",
    "tags": ["compliance", "boz", "audit"],
    "timezone": "Africa/Lusaka",
    "panels": [
      {
        "id": 1,
        "title": "Audit Events (Last 30 Days)",
        "type": "graph",
        "targets": [
          {
            "expr": "sum(rate(intellifin_audit_events_total[1h])) by (action)",
            "legendFormat": "{{action}}"
          }
        ]
      },
      {
        "id": 2,
        "title": "Access Recertification Completion Rate",
        "type": "gauge",
        "targets": [
          {
            "expr": "(count(intellifin_recertification_completed) / count(intellifin_recertification_total)) * 100",
            "legendFormat": "Completion %"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "thresholds": {
              "steps": [
                {"value": 0, "color": "red"},
                {"value": 80, "color": "yellow"},
                {"value": 95, "color": "green"}
              ]
            }
          }
        }
      },
      {
        "id": 3,
        "title": "Security Incidents by Severity",
        "type": "piechart",
        "targets": [
          {
            "expr": "sum by (severity) (intellifin_security_incidents_total)",
            "legendFormat": "{{severity}}"
          }
        ]
      },
      {
        "id": 4,
        "title": "SoD Violations Detected",
        "type": "stat",
        "targets": [
          {
            "expr": "sum(increase(intellifin_sod_violations_total[30d]))"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "thresholds": {
              "steps": [
                {"value": 0, "color": "green"},
                {"value": 1, "color": "red"}
              ]
            }
          }
        }
      },
      {
        "id": 5,
        "title": "Tamper-Evident Chain Integrity",
        "type": "stat",
        "targets": [
          {
            "expr": "intellifin_audit_chain_integrity_status"
          }
        ],
        "mappings": [
          {"value": 1, "text": "VALID", "color": "green"},
          {"value": 0, "text": "BROKEN", "color": "red"}
        ]
      }
    ]
  }
}
```

---

## 5. Data Architecture

### 5.1 Data Flow Diagram

```
┌──────────────────────────────────────────────────────────────────┐
│                    DATA SOURCES                                   │
│                                                                    │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐        │
│  │ Business │  │ Identity │  │  Audit   │  │  Config  │        │
│  │  Events  │  │  Events  │  │  Events  │  │  Changes │        │
│  └─────┬────┘  └─────┬────┘  └─────┬────┘  └─────┬────┘        │
└────────┼─────────────┼─────────────┼─────────────┼──────────────┘
         │             │             │             │
         │             │             │             │
┌────────▼─────────────▼─────────────▼─────────────▼──────────────┐
│                 PROCESSING LAYER                                  │
│                                                                    │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │         Admin Service (Event Processing)                  │   │
│  │  • Audit Chain Calculation • Policy Validation           │   │
│  │  • Correlation ID Enrichment • PII Redaction             │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                    │
└────────┬─────────────┬─────────────┬─────────────┬──────────────┘
         │             │             │             │
         │             │             │             │
┌────────▼─────────────▼─────────────▼─────────────▼──────────────┐
│                   STORAGE LAYER                                   │
│                                                                    │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐             │
│  │ SQL Server  │  │ PostgreSQL  │  │   Redis     │             │
│  │ (Hot Audit) │  │ (Keycloak)  │  │  (Caching)  │             │
│  │  90 days    │  │             │  │             │             │
│  └─────────────┘  └─────────────┘  └─────────────┘             │
│                                                                    │
│  ┌─────────────┐  ┌─────────────────────────────────────────┐  │
│  │   MinIO     │  │      OpenTelemetry Store                 │  │
│  │ (WORM 10yr) │  │  Jaeger │ Loki │ Prometheus             │  │
│  │ Audit+SSH   │  │  Traces │ Logs │ Metrics                │  │
│  └─────────────┘  └─────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

### 5.2 Data Retention Strategy

|| Data Type | Hot Storage | Warm Storage | Cold Storage | Total Retention |
||-----------|-------------|--------------|--------------|-----------------|
|| **Audit Events** | SQL Server (90 days) | MinIO WORM (10 years) | N/A | 10 years (client requirement) |
|| **Traces** | Jaeger (7 days) | N/A | N/A | 7 days |
|| **Logs** | Loki (90 days) | N/A | N/A | 90 days |
|| **Metrics** | Prometheus (30 days) | N/A | N/A | 30 days |
|| **SSH Recordings** | MinIO WORM (10 years) | N/A | N/A | 10 years (client requirement) |
|| **User Data** | Keycloak PostgreSQL | N/A | N/A | Active users only |
|| **Business Data** | SQL Server | N/A | Tape backup (10 years) | 10 years |

### 5.3 Data Sovereignty Compliance

**Requirement**: All sensitive data must remain within Zambian borders per Bank of Zambia regulations.

**Deployment Locations**:
- **Primary Data Center**: Lusaka, Zambia (on-premises)
- **Secondary Data Center**: Kitwe, Zambia (DR site)
- **Prohibited**: Cloud storage outside Zambia (AWS S3, Azure Blob, etc.)

**Compliant Architecture**:
```
┌─────────────────────────────────────────────────────────────┐
│          PRIMARY DATA CENTER (Lusaka, Zambia)               │
│                                                               │
│  Kubernetes Cluster                                          │
│  ├── All microservices                                       │
│  ├── Keycloak (PostgreSQL)                                   │
│  ├── Observability stack (Jaeger, Loki, Prometheus)         │
│  └── MinIO (WORM audit storage)                             │
│                                                               │
│  SQL Server Always On (Primary replica)                     │
└─────────────────────────────────────────────────────────────┘
                            │
                            │ Async replication
                            │
┌─────────────────────────────────────────────────────────────┐
│         SECONDARY DATA CENTER (Kitwe, Zambia)               │
│                                                               │
│  Kubernetes Cluster (standby)                                │
│  SQL Server Always On (Secondary replica - read-only)       │
│  MinIO replication (audit backup)                           │
└─────────────────────────────────────────────────────────────┘
```

---

## 6. Integration Architecture

### 6.1 Integration Patterns

#### 6.1.1 Synchronous Integration (REST/HTTP)

**Used for**: Real-time operations requiring immediate response

```
Service A                Admin Service
    │                          │
    │ POST /api/admin/audit    │
    │ (audit event)            │
    ├─────────────────────────►│
    │                          │ 1. Validate schema
    │                          │ 2. Calculate hash chain
    │                          │ 3. Store in SQL Server
    │                          │
    │ 201 Created              │
    │◄─────────────────────────┤
    │                          │
```

**Circuit Breaker Pattern** (Polly):
```csharp
services.AddHttpClient<IAdminServiceClient, AdminServiceClient>()
    .AddTransientHttpErrorPolicy(policy => policy
        .WaitAndRetryAsync(3, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
    .AddTransientHttpErrorPolicy(policy => policy
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
```

#### 6.1.2 Asynchronous Integration (RabbitMQ)

**Used for**: High-volume audit events, decoupled processing

```
Service A                RabbitMQ              Admin Service
    │                        │                      │
    │ Publish audit event    │                      │
    ├───────────────────────►│                      │
    │                        │ audit.events queue   │
    │ ACK                    │                      │
    │◄───────────────────────┤                      │
    │                        │                      │
    │                        │ Consume (batch)      │
    │                        │◄─────────────────────┤
    │                        │                      │
    │                        │ Process 100 events   │
    │                        │ (tamper-chain batch) │
    │                        │                      │
    │                        │ ACK batch            │
    │                        ├─────────────────────►│
```

**RabbitMQ Exchange Configuration**:
```yaml
exchanges:
  - name: audit.events
    type: topic
    durable: true
    queues:
      - name: audit.events.critical
        routingKey: audit.critical.*
        arguments:
          x-max-priority: 10
      - name: audit.events.normal
        routingKey: audit.normal.*
```

### 6.2 API Gateway Integration

**Yarp Configuration** (appsettings.json):
```json
{
  "ReverseProxy": {
    "Routes": {
      "admin-route": {
        "ClusterId": "admin-cluster",
        "Match": {
          "Path": "/api/admin/{**catch-all}"
        },
        "Transforms": [
          {
            "RequestHeader": "X-Correlation-Id",
            "Set": "{TraceIdentifier}"
          },
          {
            "RequestHeader": "X-Branch-Id",
            "Set": "{BranchClaim}"
          }
        ]
      },
      "identity-route": {
        "ClusterId": "identity-cluster",
        "Match": {
          "Path": "/api/auth/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "admin-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "https://admin-service.admin.svc.cluster.local"
          }
        },
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:10",
            "Timeout": "00:00:05",
            "Policy": "ConsecutiveFailures",
            "Path": "/health"
          }
        }
      }
    }
  }
}
```

**JWT Validation Middleware**:
```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("AspNetIdentity", options =>
    {
        // Legacy ASP.NET Identity tokens (30-day transition)
        options.Authority = "https://identity-service.intellifin.local";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "IntelliFin.IdentityService",
            ValidateAudience = true,
            ValidAudience = "IntelliFin.API",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    })
    .AddJwtBearer("Keycloak", options =>
    {
        // New Keycloak tokens (primary after migration)
        options.Authority = "https://keycloak.intellifin.local/realms/intellifin";
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = "api-gateway",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
        
        // Extract branch claims
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var branchId = context.Principal?.FindFirst("branchId")?.Value;
                var branchName = context.Principal?.FindFirst("branchName")?.Value;
                
                context.HttpContext.Items["BranchId"] = branchId;
                context.HttpContext.Items["BranchName"] = branchName;
                
                return Task.CompletedTask;
            }
        };
    });

// Policy-based authorization supporting both schemes
services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("AspNetIdentity", "Keycloak")
        .RequireAuthenticatedUser()
        .Build();
});
```

#### ABAC Enforcement Model
- Edge default-deny for cross-branch access enforced in ApiGateway (YARP) using branchId/branchName/branchRegion and allowedBranches/scope claims.
- Per-service EF Core global query filters enforce branch scoping on reads and writes.
- Managers explicitly “assume branch” with justification; all such actions are audited in Admin Service.

---

## 7. Security Architecture

### 7.1 Zero-Trust Architecture

**Principle**: Never trust, always verify

```
┌─────────────────────────────────────────────────────────────────┐
│                    ZERO-TRUST LAYERS                             │
│                                                                   │
│  Layer 1: Identity (Keycloak + MFA)                             │
│  ├── Every user authenticated via OIDC                          │
│  ├── Step-up MFA for sensitive operations                       │
│  └── JIT elevation with approval workflows                      │
│                                                                   │
│  Layer 2: Service-to-Service (mTLS)                             │
│  ├── All HTTP calls authenticated with client certificates      │
│  ├── Certificate rotation every 30 days (cert-manager)          │
│  └── Mutual verification (both client and server)               │
│                                                                   │
│  Layer 3: Network (NetworkPolicies)                             │
│  ├── Default deny all ingress/egress                            │
│  ├── Explicit whitelists per service                            │
│  └── Namespace isolation (admin / business / observability)     │
│                                                                   │
│  Layer 4: Data (Encryption at rest + in transit)                │
│  ├── TLS 1.3 for all external traffic                           │
│  ├── SQL Server TDE (Transparent Data Encryption)               │
│  └── MinIO server-side encryption                               │
│                                                                   │
│  Layer 5: Access (PAM + Time-bound credentials)                 │
│  ├── No permanent infrastructure access                         │
│  ├── All access via bastion with approval                       │
│  └── Vault dynamic credentials (SSH keys expire)                │
│                                                                   │
│  Layer 6: Audit (Tamper-evident + WORM)                         │
│  ├── Cryptographic hash chain                                   │
│  ├── Immutable storage (MinIO object locking)                   │
│  └── Real-time integrity verification                           │
└─────────────────────────────────────────────────────────────────┘
```

### 7.2 mTLS Implementation

**Architecture Decision**: Linkerd service mesh mTLS (ADR-006)

**Rationale**: Linkerd provides automatic mTLS with low operational overhead for our scale, simplifies certificate rotation, and enforces zero-trust by default.

```yaml
# cert-manager ClusterIssuer (Internal CA)
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: intellifin-ca-issuer
spec:
  ca:
    secretName: intellifin-ca-secret  # Root CA certificate

---
# Certificate for Admin Service
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: admin-service-cert
  namespace: admin
spec:
  secretName: admin-service-tls
  duration: 720h  # 30 days
  renewBefore: 168h  # Renew 7 days before expiry
  isCA: false
  privateKey:
    algorithm: RSA
    size: 2048
  usages:
    - digital signature
    - key encipherment
    - server auth
    - client auth
  dnsNames:
    - admin-service.admin.svc.cluster.local
    - admin-service
  issuerRef:
    name: intellifin-ca-issuer
    kind: ClusterIssuer
```

**HttpClient Configuration** (C#):
```csharp
public static IHttpClientBuilder ConfigureMTLS(
    this IHttpClientBuilder builder,
    IConfiguration configuration)
{
    return builder.ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = 
                (message, cert, chain, errors) =>
                {
                    // Validate server cert against internal CA
                    return ValidateServerCertificate(cert, chain);
                }
        };

        // Load client certificate from Kubernetes secret
        var certPath = "/etc/certs/tls.crt";
        var keyPath = "/etc/certs/tls.key";
        
        var cert = X509Certificate2.CreateFromPemFile(certPath, keyPath);
        handler.ClientCertificates.Add(cert);

        return handler;
    });
}
```

### 7.3 NetworkPolicy Examples

**Default Deny Policy** (applied to all namespaces):
```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: default-deny-all
spec:
  podSelector: {}
  policyTypes:
  - Ingress
  - Egress
```

**Admin Service Policy**:
```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: admin-service-policy
  namespace: admin
spec:
  podSelector:
    matchLabels:
      app: admin-service
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: api-gateway
    - namespaceSelector:
        matchLabels:
          name: business-services
    ports:
    - protocol: TCP
      port: 8080
  egress:
  - to:
    - namespaceSelector:
        matchLabels:
          name: identity  # Keycloak
    ports:
    - protocol: TCP
      port: 8443
  - to:
    - namespaceSelector:
        matchLabels:
          name: workflows  # Camunda
    ports:
    - protocol: TCP
      port: 8080
  - to:
    - namespaceSelector:
        matchLabels:
          name: vault
    ports:
    - protocol: TCP
      port: 8200
  - to:
    - podSelector:
        matchLabels:
          app: postgresql  # Admin DB
    ports:
    - protocol: TCP
      port: 5432
  - to:
    - podSelector:
        matchLabels:
          app: sql-server  # Admin DB
    ports:
    - protocol: TCP
      port: 1433
```

### 7.4 Secrets Management with Vault

**Vault Integration Architecture**:
```
┌──────────────────────────────────────────────────────────────────┐
│                    Kubernetes Pod (Service)                       │
│                                                                    │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │  Application Container                                       │ │
│  │  (reads secrets from /vault/secrets/)                       │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                                                                    │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │  Vault Agent Sidecar (Init Container + Sidecar)             │ │
│  │  1. Authenticate with Kubernetes SA token                   │ │
│  │  2. Fetch secrets from Vault                                │ │
│  │  3. Write to shared volume /vault/secrets/                  │ │
│  │  4. Refresh on lease expiration                             │ │
│  └─────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
                              │
                              │ Vault API (8200)
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│                    HashiCorp Vault                                │
│                                                                    │
│  Secrets Engines:                                                 │
│  ├── KV v2 (static secrets)                                       │
│  │   └── intellifin/api-keys/* (external integrations)           │
│  ├── Database (dynamic credentials)                               │
│  │   ├── SQL Server roles (admin-service-db, loan-service-db)    │
│  │   └── PostgreSQL roles (keycloak-db)                          │
│  └── SSH (JIT infrastructure access)                              │
│      └── CA signing + dynamic Unix users                          │
│                                                                    │
│  Auth Methods:                                                    │
│  └── Kubernetes (service account token validation)                │
└──────────────────────────────────────────────────────────────────┘
```

**Vault Configuration Example**:
```hcl
# Database secrets engine for SQL Server
path "database/creds/admin-service-db" {
  capabilities = ["read"]
}

# Policy for Admin Service
path "intellifin/data/admin-service/*" {
  capabilities = ["read"]
}

# SSH secrets engine for PAM
path "ssh/sign/bastion-role" {
  capabilities = ["create", "update"]
}
```

**Pod Annotation for Vault Injection**:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: admin-service
spec:
  template:
    metadata:
      annotations:
        vault.hashicorp.com/agent-inject: "true"
        vault.hashicorp.com/role: "admin-service"
        vault.hashicorp.com/agent-inject-secret-db-creds: "database/creds/admin-service-db"
        vault.hashicorp.com/agent-inject-template-db-creds: |
          {{- with secret "database/creds/admin-service-db" -}}
          export DB_USERNAME="{{ .Data.username }}"
          export DB_PASSWORD="{{ .Data.password }}"
          {{- end }}
    spec:
      serviceAccountName: admin-service
      containers:
      - name: admin-service
        image: admin-service:latest
        command: ["/bin/sh", "-c"]
        args:
          - source /vault/secrets/db-creds && ./app
```

---

## 8. Observability Architecture

### 8.1 Observability Pillars

**Three Pillars of Observability**:

1. **Traces** (Jaeger) - Distributed request tracing
2. **Metrics** (Prometheus) - Time-series system metrics
3. **Logs** (Loki) - Centralized structured logs

**Unified by**: OpenTelemetry SDK + Correlation IDs

```
┌──────────────────────────────────────────────────────────────────┐
│                      APPLICATION LAYER                            │
│  All IntelliFin microservices instrumented with OpenTelemetry    │
└────────────┬──────────────┬──────────────┬──────────────────────┘
             │              │              │
             │ OTLP/gRPC    │ OTLP/gRPC    │ OTLP/gRPC
             │ (traces)     │ (metrics)    │ (logs)
             │              │              │
┌────────────▼──────────────▼──────────────▼──────────────────────┐
│              OpenTelemetry Collector (DaemonSet)                  │
│  • Protocol translation • Batching • PII redaction               │
└────────────┬──────────────┬──────────────┬──────────────────────┘
             │              │              │
┌────────────▼────────┐ ┌──▼───────────┐ ┌▼────────────────────┐
│      Jaeger         │ │  Prometheus  │ │       Loki          │
│  (Cassandra/ES)     │ │  (TSDB)      │ │  (MinIO/S3)         │
│  Retention: 7 days  │ │  30 days     │ │  Retention: 90 days │
└────────────┬────────┘ └──┬───────────┘ └┬────────────────────┘
             │              │              │
             └──────────────┴──────────────┘
                            │
┌───────────────────────────▼──────────────────────────────────────┐
│                         Grafana                                   │
│  Unified query interface (TraceQL, PromQL, LogQL)                │
│  Dashboards: BoZ Compliance, Cost-Performance, SLA, Alerts       │
└──────────────────────────────────────────────────────────────────┘
```

### 8.2 Correlation ID Flow

**W3C Trace Context Standard**:
```
traceparent: 00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01
             │  │                                │                │
             │  └─ trace-id (128-bit)           └─ span-id       └─ flags
             └─ version
```

**Implementation**:
```csharp
// Middleware to generate/propagate correlation ID
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract or generate correlation ID
        if (!context.Request.Headers.TryGetValue("traceparent", out var traceParent))
        {
            traceParent = Activity.Current?.Id 
                ?? $"00-{Guid.NewGuid():N}{Guid.NewGuid():N}-{Guid.NewGuid():N}-01";
        }

        // Add to response headers for client tracking
        context.Response.Headers.Add("traceparent", traceParent);

        // Add to HttpContext for audit logging
        context.Items["CorrelationId"] = ExtractTraceId(traceParent);

        await _next(context);
    }

    private string ExtractTraceId(string traceParent)
    {
        // Extract trace-id from W3C traceparent format
        var parts = traceParent.ToString().Split('-');
        return parts.Length >= 3 ? parts[1] : Guid.NewGuid().ToString("N");
    }
}

// Usage in audit event
public class AuditEvent
{
    public string CorrelationId { get; set; }  // Populated from HttpContext.Items["CorrelationId"]
    // ... other properties
}
```

### 8.3 Alert Rules (Prometheus Alertmanager)

```yaml
groups:
  - name: intellifin_critical_alerts
    interval: 30s
    rules:
      # Keycloak down
      - alert: KeycloakDown
        expr: up{job="keycloak"} == 0
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Keycloak is down (instance {{ $labels.instance }})"
          description: "Keycloak has been down for more than 5 minutes. Authentication is impacted."
          runbook_url: "https://wiki.intellifin.local/runbooks/keycloak-down"

      # Audit chain break
      - alert: AuditChainBroken
        expr: intellifin_audit_chain_integrity_status == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Tamper-evident audit chain broken"
          description: "Integrity verification detected a break in the audit hash chain. Possible tampering."
          runbook_url: "https://wiki.intellifin.local/runbooks/audit-chain-break"

      # mTLS failure
      - alert: MTLSHandshakeFailure
        expr: rate(intellifin_mtls_handshake_failures_total[5m]) > 10
        for: 5m
        labels:
          severity: high
        annotations:
          summary: "High rate of mTLS handshake failures"
          description: "{{ $value }} mTLS handshake failures per second. Check certificate rotation."

      # High error rate
      - alert: HighErrorRate
        expr: (rate(http_server_requests_seconds_count{status=~"5.."}[5m]) / rate(http_server_requests_seconds_count[5m])) > 0.05
        for: 5m
        labels:
          severity: high
        annotations:
          summary: "High error rate on {{ $labels.service }}"
          description: "Error rate is {{ $value | humanizePercentage }}. Threshold is 5%."

      # Vault unavailable
      - alert: VaultUnavailable
        expr: up{job="vault"} == 0
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "Vault is unavailable"
          description: "Vault has been down for 2 minutes. Secret rotation and dynamic credentials impacted."

      # Database connection pool exhaustion
      - alert: DatabasePoolExhausted
        expr: hikaricp_connections_active / hikaricp_connections_max > 0.9
        for: 5m
        labels:
          severity: high
        annotations:
          summary: "Database connection pool near exhaustion on {{ $labels.service }}"
          description: "Connection pool utilization is {{ $value | humanizePercentage }}. Consider scaling."
```

---

## 9. Deployment Architecture

### 9.1 Kubernetes Namespace Strategy

```yaml
# Namespace segregation by concern
namespaces:
  - name: api-gateway         # API Gateway + Ingress
  - name: admin               # Admin Service + Keycloak + PostgreSQL
  - name: business-services   # Loan, Client, Financial, Collections services
  - name: identity            # Legacy IdentityService (transitional)
  - name: workflows           # Camunda (may move to admin namespace)
  - name: vault               # HashiCorp Vault
  - name: observability       # Jaeger, Loki, Prometheus, Grafana, OTEL Collector
  - name: storage             # MinIO, Redis, RabbitMQ
```

### 9.2 Helm Chart Structure

```
helm/
├── admin-service/
│   ├── Chart.yaml
│   ├── values.yaml
│   ├── values-dev.yaml
│   ├── values-prod.yaml
│   └── templates/
│       ├── deployment.yaml
│       ├── service.yaml
│       ├── configmap.yaml
│       ├── secret.yaml  # Placeholder - actual secrets in Vault
│       ├── serviceaccount.yaml
│       ├── networkpolicy.yaml
│       └── hpa.yaml  # Horizontal Pod Autoscaler
├── keycloak/
│   ├── Chart.yaml
│   ├── values.yaml
│   └── templates/
│       ├── statefulset.yaml
│       ├── service.yaml
│       ├── postgresql.yaml
│       └── ingress.yaml
└── observability/
    ├── Chart.yaml
    └── templates/
        ├── jaeger.yaml
        ├── loki.yaml
        ├── prometheus.yaml
        ├── grafana.yaml
        └── otel-collector.yaml
```

**Admin Service Helm Values** (values-prod.yaml):
```yaml
replicaCount: 3

image:
  repository: harbor.intellifin.zm/admin-service
  tag: "1.0.0"
  pullPolicy: IfNotPresent

service:
  type: ClusterIP
  port: 8080

ingress:
  enabled: true
  className: nginx
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
  hosts:
    - host: admin.intellifin.zm
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: admin-service-tls
      hosts:
        - admin.intellifin.zm

resources:
  requests:
    cpu: 500m
    memory: 512Mi
  limits:
    cpu: 2000m
    memory: 2Gi

autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 10
  targetCPUUtilizationPercentage: 70
  targetMemoryUtilizationPercentage: 80

vault:
  enabled: true
  role: admin-service
  secretPath: intellifin/admin-service

observability:
  enabled: true
  otlpEndpoint: "http://otel-collector.observability.svc.cluster.local:4317"

database:
  host: sqlserver.storage.svc.cluster.local
  port: 1433
  name: IntelliFin_AdminService
  # credentials injected from Vault

keycloak:
  url: https://keycloak.intellifin.zm
  realm: intellifin
  # client secret injected from Vault

networkPolicy:
  enabled: true
  policyTypes:
    - Ingress
    - Egress
  ingress:
    - from:
      - namespaceSelector:
          matchLabels:
            name: api-gateway
      - namespaceSelector:
          matchLabels:
            name: business-services
  egress:
    - to:
      - namespaceSelector:
          matchLabels:
            name: admin  # Keycloak, PostgreSQL
    - to:
      - namespaceSelector:
          matchLabels:
            name: workflows  # Camunda
    - to:
      - namespaceSelector:
          matchLabels:
            name: vault
    - to:
      - namespaceSelector:
          matchLabels:
            name: storage  # MinIO, SQL Server
```

### 9.3 GitOps with ArgoCD

**ArgoCD Application Manifest**:
```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: admin-service
  namespace: argocd
spec:
  project: intellifin
  source:
    repoURL: https://github.com/intellifin/k8s-config.git
    targetRevision: main
    path: helm/admin-service
    helm:
      valueFiles:
        - values-prod.yaml
  destination:
    server: https://kubernetes.default.svc
    namespace: admin
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
    syncOptions:
      - CreateNamespace=true
    retry:
      limit: 3
      backoff:
        duration: 5s
        factor: 2
        maxDuration: 3m
  revisionHistoryLimit: 10
```

**Deployment Workflow**:
```
Developer              Git Repository          ArgoCD            Kubernetes
    │                        │                     │                  │
    │ 1. Update Helm chart   │                     │                  │
    ├───────────────────────►│                     │                  │
    │                        │                     │                  │
    │ 2. Create PR           │                     │                  │
    ├───────────────────────►│                     │                  │
    │                        │                     │                  │
    │ 3. Manager approves    │                     │                  │
    ├───────────────────────►│                     │                  │
    │                        │                     │                  │
    │ 4. Merge to main       │                     │                  │
    │                        │ 5. Detect change    │                  │
    │                        │◄────────────────────┤                  │
    │                        │                     │                  │
    │                        │ 6. Sync (kubectl)   │                  │
    │                        │                     ├─────────────────►│
    │                        │                     │                  │
    │                        │ 7. Health check     │ 8. Pods ready    │
    │                        │                     │◄─────────────────┤
    │                        │                     │                  │
    │                        │ 9. Success/Failure  │                  │
    │                        │◄────────────────────┤                  │
    │ 10. Notification       │                     │                  │
    │◄───────────────────────┤                     │                  │
```

---

## 10. Architectural Decision Records (ADRs)

### ADR-001: Identity Provider Selection - Keycloak

**Status**: Accepted  
**Date**: 2025-10-11  
**Deciders**: Technical Architecture Team, Security Lead

**Context**:
IntelliFin requires a self-hosted Identity Provider (IdP) to replace embedded ASP.NET Core Identity, supporting OIDC/OAuth2, federation (Azure AD B2C), and Zambian data sovereignty requirements.

**Options Considered**:
1. **Keycloak** (Open source, Red Hat sponsored)
2. **IdentityServer4/Duende IdentityServer** (.NET-based commercial)
3. **Auth0** (SaaS - rejected due to data sovereignty)
4. **Okta** (SaaS - rejected due to data sovereignty)

**Decision**: Keycloak

**Rationale**:
- ✅ Self-hosted (Zambian data sovereignty compliant)
- ✅ Mature OIDC/OAuth2 implementation (10+ years)
- ✅ Built-in identity federation (Azure AD B2C brokering)
- ✅ Comprehensive Admin REST API for user/role management
- ✅ Free and open source (no licensing costs vs. Duende)
- ✅ Large community and enterprise adoption (Red Hat SSO is Keycloak)
- ✅ Token rotation support (refresh token families)
- ✅ MFA support (TOTP, WebAuthn)

**Consequences**:
- ➕ Reduces vendor lock-in vs. commercial IdP
- ➕ PostgreSQL database required (acceptable - already have DBA expertise)
- ➖ Java-based (different from .NET stack, but isolated service)
- ➖ Requires separate infrastructure (adds operational complexity)

**Implementation Notes**:
- Deploy to `admin` namespace with dedicated PostgreSQL
- Configure realm `intellifin` with custom theme
- Use Keycloak Admin API from Admin Service for user management
- Enable refresh token rotation for security

---

### ADR-002: Distributed Tracing - Jaeger

**Status**: Accepted  
**Date**: 2025-10-11

**Context**:
IntelliFin requires distributed tracing for production debugging across 9+ microservices, with in-country deployment (Zambia) for data sovereignty.

**Options Considered**:
1. **Jaeger** (CNCF graduated, OpenTelemetry native)
2. **Zipkin** (Original distributed tracing, mature)
3. **AWS X-Ray** (SaaS - rejected due to data sovereignty)
4. **Datadog APM** (SaaS - rejected due to data sovereignty)
5. **Elastic APM** (Self-hosted, but heavier infrastructure)

**Decision**: Jaeger

**Rationale**:
- ✅ OpenTelemetry native (OTLP protocol support)
- ✅ Self-hosted on-premises (Zambian data sovereignty)
- ✅ CNCF graduated project (production-ready)
- ✅ Storage options: Cassandra, Elasticsearch, or in-memory
- ✅ Lightweight for 9-service deployment
- ✅ Good Grafana integration (via Tempo or Jaeger plugin)

**Consequences**:
- ➕ Future-proof with OpenTelemetry standard
- ➕ Storage backend flexibility (can start with Elasticsearch)
- ➖ Requires storage infrastructure (Elasticsearch cluster recommended for production)
- ➖ 7-day retention limit to control storage costs

**Implementation Notes**:
- Deploy Jaeger all-in-one for dev/staging
- Production: Jaeger Collector + Elasticsearch backend
- OTLP gRPC endpoint: 14250
- Retention: 7 days (configurable in Elasticsearch ILM policy)

---

### ADR-003: Centralized Logging - Loki

**Status**: Accepted  
**Date**: 2025-10-11

**Context**:
IntelliFin requires centralized logging for 9+ microservices, with cost-effective storage and Grafana integration for unified observability.

**Options Considered**:
1. **Loki** (Grafana Labs, designed for Kubernetes)
2. **ELK Stack** (Elasticsearch + Logstash + Kibana - industry standard but heavier)
3. **Fluentd + Elasticsearch** (CNCF, heavy infrastructure)
4. **CloudWatch Logs** (AWS SaaS - rejected due to data sovereignty)

**Decision**: Loki

**Rationale**:
- ✅ Cost-effective: Indexes labels only, not full log content (10x cheaper than ELK)
- ✅ Grafana native integration (unified observability UI with Prometheus)
- ✅ LogQL query language similar to PromQL (easier learning curve)
- ✅ Kubernetes-native (Promtail DaemonSet for log collection)
- ✅ S3-compatible storage (MinIO backend for in-country compliance)
- ✅ Supports structured logs (JSON) and PII redaction

**Consequences**:
- ➕ Lower storage costs vs. Elasticsearch
- ➕ Unified Grafana UI for logs, metrics, traces
- ➖ Less mature than ELK (Loki is ~5 years old vs. ELK 10+ years)
- ➖ No full-text search indexing (must query with labels + LogQL)

**Implementation Notes**:
- Deploy Loki with MinIO backend for 90-day retention
- Promtail DaemonSet for log collection (scrapes stdout/stderr)
- Alternative: OpenTelemetry Collector OTLP log export
- PII redaction via Promtail pipeline stages (NRC numbers, phones)

---

### ADR-006: mTLS Implementation - Linkerd Service Mesh

**Status**: Accepted  
**Date**: 2025-10-11

**Context**:
IntelliFin requires mTLS for all service-to-service communication (zero-trust architecture). Two approaches: service mesh (Istio/Linkerd) or manual mTLS with cert-manager.

**Options Considered**:
1. **Linkerd Service Mesh** (Automatic mTLS, lightweight)
2. **Manual mTLS with cert-manager** (Kubernetes-native cert management)
3. **Istio Service Mesh** (Feature-rich but complex)

**Decision**: Linkerd service mesh

**Rationale**:
- ✅ Automatic mTLS with low operational overhead for our scale
- ✅ Simplified certificate rotation and uniform policy enforcement
- ✅ Minimal configuration in services (transparent sidecar)
- ✅ Strong zero-trust posture by default

**Consequences**:
- ➕ Transparent mTLS (no per-service HttpClient configuration)
- ➕ Consistent security policies across services
- ➖ Sidecar overhead per pod (small memory/CPU cost)
- ➖ Mesh operational layer to maintain (upgrades/monitoring)

**Implementation Notes**:
- Enable Linkerd injection for namespaces/services requiring mTLS
- Monitor mesh health via Linkerd Viz; alert on mTLS handshake errors
- Keep cert-manager for ingress TLS and PKI as needed
- Validate performance overhead (<20ms p95) during rollout

---

### ADR-007: GitOps - ArgoCD

**Status**: Accepted  
**Date**: 2025-10-11

**Context**:
IntelliFin requires declarative configuration management for Kubernetes deployments with audit trail, rollback capabilities, and policy-driven approvals.

**Options Considered**:
1. **ArgoCD** (CNCF incubating, GitOps for Kubernetes)
2. **Flux CD** (CNCF graduated, GitLab/GitHub native)
3. **Jenkins X** (CI/CD + GitOps, heavier stack)
4. **Manual kubectl/helm** (No automation, error-prone)

**Decision**: ArgoCD

**Rationale**:
- ✅ Kubernetes-native declarative sync (Git as source of truth)
- ✅ Rich UI for deployment visualization (vs. Flux CLI-only)
- ✅ Multi-cluster support (useful for DR secondary data center)
- ✅ RBAC integration with Keycloak (SSO for developers)
- ✅ Automated rollback on health check failure
- ✅ Sync waves for ordered deployments (DB migrations before app)
- ✅ Audit trail: Git commits show who deployed what, when

**Consequences**:
- ➕ Immutable infrastructure (no manual kubectl changes)
- ➕ PR-based approvals for config changes (aligns with FR17 policy-driven config)
- ➕ Easy rollback via Git revert
- ➖ Requires Git discipline (broken YAML = failed deployment)
- ➖ Secrets still in Vault (ArgoCD doesn't replace Vault, complementary)

**Implementation Notes**:
- Deploy ArgoCD to `argocd` namespace
- Configure Keycloak OIDC for SSO
- Git repository: `intellifin-k8s-config` (private GitHub repo)
- Application per microservice + observability stack
- Auto-sync enabled with 5-minute poll interval

---

### ADR-008: PAM Solution - Teleport

**Status**: Proposed (Phase 5 evaluation)  
**Date**: 2025-10-11

**Context**:
IntelliFin requires Privileged Access Management (PAM) for infrastructure access (SSH, RDP, kubectl) with JIT access, session recording, and Camunda approval workflows.

**Options Considered**:
1. **HashiCorp Boundary** (Open source, Vault integration)
2. **Teleport** (Open source, session recording built-in)
3. **StrongDM** (Commercial, easy setup)
4. **Custom bastion + Vault SSH** (Manual implementation)

**Decision**: Teleport (selected)

**Rationale**:
- ✅ Vault integration (dynamic credentials, SSH CA signing)
- ✅ JIT access model (time-bound sessions)
- ✅ Session recording support (can output to MinIO)
- ✅ API-driven (integrates with Camunda workflows)
- ✅ Open source with enterprise option (upgrade path)

**Consequences**:
- ➕ Unified HashiCorp stack (Vault + Boundary)
- ➕ Dynamic credentials (no permanent SSH keys)
- ➖ Relatively new project (1-2 years old vs. Teleport 7+ years)
- ➖ May require fallback to custom bastion if PoC fails

**Implementation Notes** (Phase 5):
- Deploy Teleport with Keycloak OIDC authentication and audit logging
- Test JIT access flow: Request → Camunda approval → Teleport credential grant (time-bound)
- Validate session recording output to MinIO (WORM retention)
- If issues arise, fallback to Boundary or custom hardened bastion

---

### ADR-010: Audit WORM Storage - MinIO Object Locking

**Status**: Accepted  
**Date**: 2025-10-11

**Context**:
Bank of Zambia requires a minimum 7-year audit retention; client requirement is 10-year retention. MinIO is already deployed for document storage and will enforce 10-year WORM retention.

**Decision**: Extend MinIO with Object Lock (WORM) for audit storage

**Rationale**:
- ✅ MinIO already in use (existing infrastructure)
- ✅ S3-compatible Object Lock (WORM compliance mode)
- ✅ 10-year retention enforcement (cannot delete before expiration)
- ✅ On-premises deployment (Zambian data sovereignty)
- ✅ Replication to secondary data center for DR
- ✅ Legal hold support (extend retention beyond 7 years if litigation)

**Alternatives Rejected**:
- ❌ Azure Blob immutable storage (cloud-based, data sovereignty issue)
- ❌ Tape backup (slow retrieval, no search capability)
- ❌ SQL Server with triggers (deletions can be circumvented by DBA)

**Consequences**:
- ➕ Regulatory compliant (BoZ audit retention)
- ➕ Tamper-proof (cannot modify or delete objects)
- ➕ Cost-effective (no additional storage system)
- ➖ No modification workflow (append-only, must design audit corrections as new events)
- ➖ Storage costs accumulate (7 years * daily audit exports = significant storage)

**Implementation Notes**:
- Create MinIO bucket `audit-logs` with Compliance mode Object Lock
- Retention: 3650 days (10 years)
- Daily export: Admin Service writes JSONL files to MinIO at midnight UTC
- Include tamper-evident hash chain in export for offline verification
- MinIO access logging enabled (audit the auditors)

---

## 11. Migration Strategy

### 11.1 Phased Migration Timeline (12 Months)

```
Month 1-2 (Foundation)
├── Week 1-2: Keycloak deployment, realm configuration, theme customization
├── Week 3-4: User migration from ASP.NET Identity to Keycloak
├── Week 5-6: API Gateway dual-token support, Admin Service scaffolding
├── Week 7-8: OpenTelemetry instrumentation, Jaeger/Prometheus/Loki deployment
└── Milestone: Observability operational, Keycloak ready for gradual adoption

Month 3-4 (Enhanced Security)
├── Week 9-10: Rotating refresh tokens, branch-scoped JWT claims
├── Week 11-12: mTLS rollout (cert-manager, 2 services pilot)
├── Week 13-14: mTLS expansion to all services
├── Week 15-16: NetworkPolicies implementation, security testing
└── Milestone: Zero-trust runtime achieved

Month 5-6 (Audit & Compliance)
├── Week 17-18: Audit centralization in Admin Service, migration from FinancialService
├── Week 19-20: Tamper-evident chain implementation, integrity verification
├── Week 21-22: MinIO WORM configuration, daily audit export automation
├── Week 23-24: Correlation ID propagation, offline CEO app audit merge
└── Milestone: Audit system BoZ-compliant

Month 7-8 (Governance & Workflows)
├── Week 25-26: JIT privilege elevation with Camunda workflows
├── Week 27-28: Step-up MFA, SoD enforcement
├── Week 29-30: Policy-driven config management, Vault secret rotation
├── Week 31-32: Quarterly access recertification workflows
└── Milestone: Governance automation operational

Month 9-10 (Zero-Trust & PAM)
├── Week 33-34: GitOps with ArgoCD, container image signing
├── Week 35-36: Bastion host deployment, PAM solution PoC
├── Week 37-38: JIT infrastructure access with Vault dynamic credentials
├── Week 39-40: SSH session recording in MinIO
└── Milestone: PAM operational, infrastructure access secured

Month 11-12 (Advanced Observability)
├── Week 41-42: BoZ compliance dashboards, cost-performance monitoring
├── Week 43-44: Automated alerting, incident response playbooks
├── Week 45-46: DR runbook automation, quarterly testing
├── Week 47-48: Final system integration testing, documentation, training
└── Milestone: Control plane fully operational, production-ready
```

### 11.2 Migration Risks and Mitigation

| Risk | Phase | Mitigation |
|------|-------|------------|
| **Keycloak user migration data loss** | 1-2 | Staging dry-run, maintain ASP.NET Identity read-only for 90 days, rollback script ready |
| **Dual-token validation breaks existing clients** | 1-2 | Extensive integration testing, 30-day transition window, gradual service migration |
| **OpenTelemetry performance overhead** | 1-2 | Adaptive sampling (10% normal, 100% errors), async exporters, load testing at 2x traffic |
| **mTLS certificate rotation downtime** | 3-4 | Rolling updates, 5-minute overlap for in-flight requests, automated rollback on health check failure |
| **Audit chain re-hash for offline merge** | 5-6 | Batch processing (max 1000 events), background job, comprehensive conflict resolution testing |
| **JIT elevation workflow delays** | 7-8 | SignalR real-time notifications, <15s approval-to-activation target (NFR11), fallback to email |
| **PAM solution PoC failure** | 9-10 | Boundary PoC in Week 35-36, fallback to Teleport or custom bastion by Week 37 |
| **Observability stack resource exhaustion** | 11-12 | Right-size Prometheus/Loki/Jaeger, retention policies (30d/90d/7d), monitor infrastructure cost |

### 11.3 Rollback Strategy

**Per-Phase Rollback Plans**:

**Phase 1 (Foundation)**:
- Keycloak: Disable realm, revert API Gateway to ASP.NET Identity-only validation
- Observability: Non-critical, can be disabled without impacting business operations
- Rollback Trigger: >10% authentication failures OR >20% latency increase
- Rollback Time: <2 hours

**Phase 2 (Enhanced Security)**:
- mTLS: Revert HttpClient configs to non-mTLS, remove NetworkPolicies
- Branch Claims: API Gateway reverts to database-query authorization
- Rollback Trigger: >20ms p95 latency increase OR widespread mTLS handshake failures
- Rollback Time: <4 hours (rolling update per service)

**Phase 3 (Audit & Compliance)**:
- Audit: Services revert to direct FinancialService audit calls (legacy path)
- Tamper-chain: Disable integrity verification, maintain event storage
- Rollback Trigger: Audit ingestion latency >500ms OR data loss detected
- Rollback Time: <3 hours

**Phase 4-6**:
- Progressive feature rollbacks via GitOps (ArgoCD revert to previous Git commit)
- Camunda workflows: Disable processes, revert to manual approvals
- PAM: Fallback to existing direct SSH access (controlled via firewall)
- Rollback Time: <1 hour per component (GitOps automation)

---

## 12. Quality Attributes

### 12.1 Performance

**Target**: Maintain existing system performance (±10%) while adding observability and security layers.

**Key Metrics**:
- API Gateway JWT validation: <50ms p95 (NFR2)
- Branch-scoped claims reduce DB queries: 80% reduction, 200ms → 120ms (NFR3)
- mTLS handshake overhead: <20ms p95 (NFR9)
- Audit event ingestion: 10,000 events/sec peak (NFR4)
- Keycloak authentication: <500ms p95 for 1,000 concurrent users (NFR10)

**Load Testing Strategy**:
```yaml
# k6 load test (example)
scenarios:
  authentication_load:
    executor: ramping-arrival-rate
    startRate: 100  # 100 auth/sec
    timeUnit: 1s
    preAllocatedVUs: 50
    maxVUs: 200
    stages:
      - duration: 5m
        target: 500  # Ramp to 500 auth/sec
      - duration: 10m
        target: 1000 # Peak: 1000 auth/sec
      - duration: 5m
        target: 100  # Ramp down
  
  api_traffic:
    executor: constant-arrival-rate
    rate: 5000  # 5000 req/sec sustained
    timeUnit: 1s
    duration: 30m
    preAllocatedVUs: 500
    maxVUs: 1000
```

### 12.2 Availability

**Target**: 99.9% monthly uptime (NFR1) = 43.8 minutes downtime per month

**High Availability Design**:
- **Admin Service**: 3 replicas, HPA (scale to 10), anti-affinity across nodes
- **Keycloak**: 2 replicas (stateless), PostgreSQL HA (streaming replication)
- **Observability**: Non-critical path (failures don't block business operations)
- **Database**: SQL Server Always On (primary + secondary replica)

**Graceful Degradation**:
- Admin Service down: Authentication continues (Keycloak direct), audit buffered in RabbitMQ
- Keycloak down: Cached JWTs valid for 15 minutes, emergency fallback to local admin
- Observability down: No impact on business operations (telemetry buffers or drops)

### 12.3 Scalability

**Horizontal Scaling**:
- Admin Service: Stateless, scale via HPA (CPU/memory thresholds)
- Keycloak: Stateless, scale via HPA (2-5 replicas typical)
- Observability: Independent scaling (Jaeger, Loki, Prometheus)

**Vertical Scaling Limits**:
- Database: SQL Server vertical scaling to 32 vCPU / 128 GB RAM (current capacity)
- MinIO: Add nodes to distributed cluster for storage expansion

**Bottleneck Analysis**:
- **Primary Bottleneck**: SQL Server (audit writes, business data)
  - Mitigation: Batch audit writes, move to RabbitMQ async, read replicas for reporting
- **Secondary Bottleneck**: Keycloak PostgreSQL (user/role lookups)
  - Mitigation: Redis caching of Keycloak Admin API responses (5-minute TTL)

### 12.4 Security

**Threat Model**:

| Threat | Mitigation | Architecture Component |
|--------|------------|----------------------|
| **Credential theft** | Rotating refresh tokens, short-lived access tokens (15 min) | Keycloak, Redis token families |
| **Man-in-the-middle** | mTLS service-to-service, TLS 1.3 external | cert-manager, Ingress TLS |
| **Lateral movement** | NetworkPolicies (default deny), namespace isolation | Kubernetes NetworkPolicy |
| **Privileged escalation** | JIT elevation with approval, automatic expiration | Admin Service, Camunda |
| **Audit tampering** | Tamper-evident chain, WORM storage (MinIO Object Lock) | Admin Service, MinIO |
| **Insider threat** | SoD enforcement, PAM session recording, audit all admin actions | Admin Service, Bastion |
| **Secret exposure** | Vault dynamic credentials, no hardcoded secrets | Vault, Kubernetes secrets injection |
| **Supply chain attack** | Container image signing (Cosign), SBOM generation (Syft), CVE scanning (Trivy) | CI/CD pipeline, admission controller |

**Security Testing**:
- Annual penetration testing (external firm)
- Quarterly vulnerability scanning (Trivy + Grype)
- Continuous dependency scanning (GitHub Dependabot)
- Security chaos engineering (simulate mTLS failures, cert expiration, Vault downtime)

### 12.5 Maintainability

**Operational Complexity Assessment**:

| Component | Operational Complexity | Mitigation |
|-----------|----------------------|------------|
| Keycloak | Medium | Runbooks for realm config, PostgreSQL backup automation, quarterly upgrade testing |
| Admin Service | Medium | Standard .NET microservice, comprehensive logging, health checks |
| Observability Stack | High | 4 systems (Jaeger, Loki, Prometheus, Grafana), retention policies, storage monitoring |
| mTLS (cert-manager) | Low | Automatic rotation, Prometheus alerts on expiration |
| Vault | Medium | Dynamic credential rotation, backup/restore procedures, unseal automation |
| GitOps (ArgoCD) | Low | Git-based rollback, UI for deployment visualization |
| PAM (Boundary/Bastion) | Medium | SSH CA automation, session recording monitoring |

**Documentation Strategy**:
- **ADRs**: Major architecture decisions (this document)
- **Runbooks**: Operational procedures (Keycloak down, audit chain break, DR failover)
- **API Docs**: OpenAPI/Swagger for all REST APIs
- **Architecture Diagrams**: Mermaid diagrams in Markdown (version-controlled)
- **Training**: 2-day workshop for DevOps team on new architecture

### 12.6 Observability

**Three Pillars Coverage**:

| Service | Traces (Jaeger) | Metrics (Prometheus) | Logs (Loki) |
|---------|-----------------|---------------------|-------------|
| Admin Service | ✅ OTLP | ✅ OTLP | ✅ OTLP |
| Keycloak | ❌ (external system) | ✅ /metrics endpoint | ✅ Promtail |
| API Gateway | ✅ OTLP | ✅ OTLP | ✅ OTLP |
| Business Services (9x) | ✅ OTLP | ✅ OTLP | ✅ OTLP |
| Observability Stack | ✅ Self-tracing | ✅ Self-metrics | ✅ Self-logging |

**Unified Query via Grafana**:
- Traces: TraceQL (Jaeger)
- Metrics: PromQL (Prometheus)
- Logs: LogQL (Loki)
- Correlation: Link traces to logs via correlation ID

**Alerting Coverage**:
- Critical: Keycloak down, audit chain break, mTLS failure, Vault unavailable
- High: High error rate, database pool exhaustion, certificate expiration
- Medium: High memory usage, slow response times, elevated latency

---

## Document Status

**Architecture Status**: ✅ Complete and Ready for Development Phase

**Next Steps**:
1. **Technical Review**: Architecture Team + Security Lead review (2-day session)
2. **Stakeholder Approval**: CTO + Compliance Officer sign-off on security/compliance architecture
3. **ADR Publication**: Publish ADRs to `docs/domains/system-administration/adrs/` folder
4. **Development Kickoff**: SM agent creates Story 1.1 (Keycloak Deployment) from PRD

**Document Location**: `docs/domains/system-administration/system-administration-control-plane-architecture.md`

**Related Documents**:
- PRD: `docs/domains/system-administration/system-administration-control-plane-prd.md`
- Brownfield Analysis: `docs/domains/system-administration/system-administration-brownfield-analysis.md`
- ADRs (to be created): `docs/domains/system-administration/adrs/adr-001-keycloak.md` (etc.)

---

**Document End**
