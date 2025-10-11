# Story 1.4: Admin Microservice Scaffolding and Deployment

### Metadata
- **ID**: 1.4 | **Points**: 5 | **Effort**: 3-4 days | **Priority**: P0
- **Dependencies**: 1.1 (Keycloak), Kubernetes
- **Blocks**: 1.5, 1.14, 1.23

### User Story
**As a** System Administrator,  
**I want** Admin microservice deployed with basic health checks and API structure,  
**so that** we have the control plane orchestration hub ready for feature implementation.

### Acceptance Criteria
1. `IntelliFin.AdminService` ASP.NET Core 9 project created with Minimal APIs
2. Database context created for `IntelliFin_AdminService` database
3. Entity Framework migrations initialized
4. Docker image built with Cosign signature
5. Helm chart created for Kubernetes deployment
6. Health check endpoint operational and monitored
7. OpenAPI/Swagger documentation generated

### Database Schema
```sql
CREATE DATABASE IntelliFin_AdminService;

CREATE TABLE AuditEvents (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    EventId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Actor NVARCHAR(100) NOT NULL,
    Action NVARCHAR(50) NOT NULL,
    EntityType NVARCHAR(100),
    EntityId NVARCHAR(100),
    CorrelationId NVARCHAR(100),
    PreviousEventHash NVARCHAR(64),
    CurrentEventHash NVARCHAR(64),
    EventData NVARCHAR(MAX),
    INDEX IX_Timestamp (Timestamp),
    INDEX IX_Actor (Actor),
    INDEX IX_CorrelationId (CorrelationId)
);

CREATE TABLE UserIdMapping (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AspNetUserId NVARCHAR(450) NOT NULL UNIQUE,
    KeycloakUserId NVARCHAR(100) NOT NULL UNIQUE,
    MigrationDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    INDEX IX_AspNetUserId (AspNetUserId),
    INDEX IX_KeycloakUserId (KeycloakUserId)
);
```

### API Endpoints
- `GET /health` - Health check
- `GET /health/ready` - Readiness probe
- `GET /metrics` - Prometheus metrics
- `GET /api/admin/version` - Service version info

### Integration Verification
- **IV1**: Admin Service deployed to `admin` namespace with 3 replicas
- **IV2**: API Gateway routes `/api/admin/*` to Admin Service
- **IV3**: Admin Service connects to SQL Server and Keycloak
