# Identity & Access Management (IAM) Enhancement PRD

## Document Information

**Project:** Intellifin Loan Management System  
**Module:** Identity & Access Management (IAM)  
**Document Type:** Product Requirements Document (PRD)  
**Version:** 1.0  
**Date:** 2025-10-15  
**Author:** PM (John)

---

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2025-10-15 | 1.0 | Initial IAM Enhancement PRD | PM (John) |
| 2025-10-15 | 0.9 | Brownfield Assessment Completed | Architect (Winston) |

---

# 1. Project Analysis and Context

## 1.1 Analysis Source

✅ **Document-project analysis available**  
- Source: `domains/identity-access-management/iam-brownfield-assessment.md`  
- Completed: 2025-10-15 by Architect (Winston)
- Scope: Identity Service, Keycloak infrastructure, API Gateway authentication

## 1.2 Current Project State

**Existing System:** Intellifin Loan Management System has **two parallel identity systems**:

### System 1: Custom JWT Identity Service (`IntelliFin.IdentityService`)
- Application-level authentication with custom JWT tokens
- Comprehensive RBAC with 80+ atomic permissions
- Redis-backed sessions, account lockout, password policies
- ASP.NET Identity framework with SQL Server storage
- JWT token generation and validation (custom implementation)
- Session management with device tracking
- Audit logging integrated with Admin Service

### System 2: Keycloak Infrastructure (Deployed but isolated)
- Version 24.0.4, 3 replicas (production-ready HA setup)
- PostgreSQL backend, IntelliFin realm configured
- OIDC endpoints available at keycloak.intellifin.local
- **NOT integrated** with Identity Service
- Used only by API Gateway for token validation (not generation)

**Critical Finding:** These systems are disconnected. Identity Service generates custom JWTs; Keycloak is deployed but only used by API Gateway for validation (not generation).

## 1.3 Available Documentation

✅ Using existing project analysis from document-project output:

**Created Documents:**
- `iam-brownfield-assessment.md` - Comprehensive 1,186-line assessment including:
  - ✅ Tech Stack Documentation
  - ✅ Source Tree/Architecture  
  - ✅ API Documentation
  - ✅ Technical Debt Documentation
  - ✅ Gap Analysis (10 categories)
  - ✅ Integration Points
  - ✅ Migration Strategy Options

## 1.4 Enhancement Scope Definition

**Enhancement Type:** ☑ Integration with New Systems + Major Feature Modification

**Enhancement Description:**  
Integrate the existing custom JWT Identity Service with the deployed Keycloak infrastructure to create a unified Identity & Access Management (IAM) module. This includes implementing OIDC flows, adding multi-tenancy support, establishing service-to-service authentication, enforcing Separation of Duties rules, and providing comprehensive identity lifecycle management.

**Impact Assessment:** ☑ **Major Impact (architectural changes required)**

This enhancement requires:
- Architectural shift from custom JWT to OIDC with Keycloak
- New tenancy model implementation
- Database schema additions
- Service-to-service authentication framework
- Policy enforcement mechanisms
- Migration strategy for existing users/tokens

## 1.5 Goals and Background Context

### Goals

- Integrate Identity Service with Keycloak as primary IdP using OIDC standard flows
- Implement multi-tenant architecture with org/tenant boundaries and user-to-tenant membership
- Establish OAuth2 client credentials flow for service-to-service authentication
- Enforce Separation of Duties (SoD) rules to prevent conflicting role assignments
- Provide self-service user provisioning with password reset and account verification
- Create baseline role templates and seed data for consistent deployments
- Expose public APIs for token introspection, permission checks, and user directory queries
- Maintain backwards compatibility with existing authentication patterns during migration

### Background Context

The Intellifin Loan Management System currently operates with a sophisticated custom JWT authentication system that provides comprehensive RBAC. However, the organization has also deployed Keycloak infrastructure to enable future federation, SSO capabilities, and industry-standard OIDC compliance. Currently, these two systems operate in parallel without integration, creating technical debt and limiting the platform's ability to scale to multi-tenant scenarios.

This enhancement addresses this architectural gap by establishing Keycloak as the authoritative identity provider while preserving the extensive permission model and business rules already implemented. The integration will enable the platform to support multiple organizational tenants, implement proper service-to-service authentication, and provide automated user lifecycle management—all critical requirements for the planned SaaS expansion and regulatory compliance improvements.

---

# 2. Requirements

## 2.1 Functional Requirements

**FR1: OIDC Integration with Keycloak**  
The Identity Service shall implement OpenID Connect (OIDC) Authorization Code flow with Keycloak as the primary identity provider, supporting standard endpoints (/authorize, /token, /userinfo, /logout) and OIDC discovery (/.well-known/openid-configuration).

**FR2: User Provisioning to Keycloak**  
The system shall automatically provision users from the existing SQL Server Identity database to Keycloak, maintaining synchronization of user profiles, roles, and custom claims (branchId, branchName, branchRegion, tenant membership).

**FR3: Dual JWT Validation (Migration Period)**  
During the migration period, the API Gateway shall accept and validate both Keycloak-issued OIDC tokens and legacy custom JWT tokens to maintain backwards compatibility with existing clients.

**FR4: Multi-Tenant Architecture**  
The system shall implement a tenant entity model with Tenant, TenantUsers, and TenantBranches tables, enabling users to belong to multiple tenants with tenant-scoped role assignments.

**FR5: Tenant Context in JWT Claims**  
JWT tokens issued by Keycloak shall include tenant context (tenantId, tenantName) in addition to existing branch claims, enabling tenant-aware authorization throughout the system.

**FR6: Service-to-Service Authentication**  
The system shall support OAuth2 Client Credentials flow for service-to-service authentication, with service principals registered in Keycloak and token scopes defining permitted operations.

**FR7: Service Identity Management**  
The Identity Service shall provide APIs to register, manage, and rotate credentials for service accounts, with audit trails of all service-to-service authentication events.

**FR8: Separation of Duties (SoD) Enforcement**  
The system shall enforce pre-defined SoD rules during role assignment, preventing users from holding conflicting permissions (e.g., loans:create + loans:approve, gl:post + gl:reverse).

**FR9: SoD Violation Detection**  
The system shall detect SoD violations in real-time during role assignment and provide override workflows requiring additional approval for justified exceptions.

**FR10: Baseline Role Templates**  
The system shall seed baseline role templates (System Administrator, Loan Officer, Underwriter, Finance Manager, Collections Officer, Compliance Officer) with pre-configured permission mappings.

**FR11: Self-Service Password Reset**  
Users shall be able to initiate self-service password reset via email verification, leveraging Keycloak's password reset flows with SMTP integration.

**FR12: Account Activation Emails**  
New user accounts shall trigger automated activation emails with secure verification links, integrating with Keycloak's user registration flows.

**FR13: Token Introspection API**  
The Identity Service shall provide a token introspection endpoint (RFC 7662 compliant) enabling downstream services to validate tokens and retrieve associated claims.

**FR14: Permission Check API**  
The system shall expose an API endpoint for permission checks (`/api/auth/check-permission`) accepting userId, permission, and context (branchId, tenantId) to support fine-grained authorization.

**FR15: User Directory API**  
The system shall provide searchable user directory APIs with filtering by tenant, branch, role, and status, supporting pagination for large result sets.

**FR16: Group Management APIs**  
The system shall support group-based permission management with APIs for creating groups, assigning users to groups, and managing group permissions.

**FR17: Audit Event Storage**  
All authentication, authorization, and identity lifecycle events shall be stored locally in SQL Server (in addition to forwarding to Admin Service) for compliance queries and forensic analysis.

**FR18: Token Revocation Persistence**  
Token revocation events shall be persisted to SQL Server in addition to Redis denylist, ensuring revocation list survives Redis failures and providing historical audit trail.

**FR19: Keycloak Event Listeners**  
The system shall implement Keycloak event listeners (webhooks) to synchronize user lifecycle events (create, update, delete, login, logout) between Keycloak and SQL Server.

**FR20: Migration Scripts**  
EF Core migrations shall create all new identity tables (Tenants, TenantUsers, TenantBranches, ServiceAccounts, SoDRules, AuditEvents, TokenRevocations) with proper indexes and foreign key constraints.

## 2.2 Non-Functional Requirements

**NFR1: Authentication Performance**  
Token generation and validation operations shall complete within 200ms at 95th percentile under normal load (100 requests/second), maintaining compatibility with existing system performance.

**NFR2: High Availability**  
Identity Service shall maintain 99.9% uptime with support for horizontal scaling, leveraging existing Keycloak HA setup (3 replicas) and Redis cluster.

**NFR3: Session Capacity**  
The system shall support minimum 10,000 concurrent sessions with existing Redis infrastructure, with ability to scale horizontally by adding Redis nodes.

**NFR4: Keycloak Performance**  
Keycloak token issuance shall complete within 300ms at 95th percentile, leveraging Infinispan distributed cache and PostgreSQL read replicas.

**NFR5: Database Performance**  
Identity database queries shall not exceed 100ms at 95th percentile for user authentication flows, utilizing proper indexing on AspNetUsers, Tenants, and TenantUsers tables.

**NFR6: Security Standards**  
All identity operations shall comply with OWASP Top 10 and implement TLS 1.3 for data in transit, AES-256 for data at rest, and BCrypt (12 rounds) for password hashing.

**NFR7: Observability**  
All identity operations shall emit structured logs (Serilog), distributed traces (OpenTelemetry), and metrics (Prometheus) with correlation IDs for end-to-end request tracking.

**NFR8: Audit Compliance**  
All authentication events, authorization decisions, role changes, and permission assignments shall be immutably logged with actor, timestamp, IP address, and before/after state.

**NFR9: Scalability - Multi-Tenancy**  
The system shall support minimum 100 tenants with 1,000 users per tenant (100,000 total users) without architectural changes, with horizontal scaling path to 1M+ users.

**NFR10: Migration Zero-Downtime**  
Migration from custom JWT to Keycloak OIDC shall be executed with zero downtime using blue-green deployment and dual-token validation period.

**NFR11: Backward Compatibility Duration**  
Legacy custom JWT tokens shall remain valid for minimum 30 days during migration period to allow gradual client migration.

**NFR12: API Response Time**  
Identity Service APIs (user directory, permission checks, token introspection) shall respond within 150ms at 95th percentile.

**NFR13: Keycloak Admin API Rate Limits**  
User provisioning to Keycloak shall respect Keycloak Admin API rate limits, implementing exponential backoff and batch operations where applicable.

**NFR14: Redis Failover**  
Session management shall gracefully handle Redis failover with automatic reconnection and session recreation within 5 seconds.

**NFR15: Database Connection Pooling**  
Identity Service shall use database connection pooling with minimum 20, maximum 100 connections, integrated with Vault for dynamic credential rotation.

## 2.3 Compatibility Requirements

**CR1: Existing API Compatibility**  
All existing Identity Service REST APIs (`/api/auth/login`, `/api/auth/refresh`, `/api/users`, `/api/roles`, `/api/permissioncatalog`) shall remain functional during and after migration, returning responses in identical format.

**CR2: Database Schema Compatibility**  
New identity tables (Tenants, TenantUsers, etc.) shall be additive only; existing ASP.NET Identity tables (AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims) shall not have breaking schema changes.

**CR3: JWT Claims Backward Compatibility**  
Custom JWT tokens issued during migration period shall contain all existing claims (userId, username, email, roles, permissions, branchId, branchName, branchRegion, sessionId) in addition to new claims (tenantId, tenantName).

**CR4: API Gateway Integration Compatibility**  
API Gateway (YARP) JWT validation middleware shall support both Keycloak OIDC tokens and legacy custom JWTs with identical branch claim extraction and header forwarding behavior.

**CR5: UI/UX Consistency**  
Login flow changes (redirect to Keycloak vs. direct API call) shall be transparent to end users where possible, maintaining existing login page branding and user experience.

**CR6: Redis Data Structure Compatibility**  
New Redis data structures for Keycloak tokens shall coexist with existing session and refresh token structures, using distinct key prefixes to avoid collisions.

**CR7: Audit Event Format Compatibility**  
Audit events sent to Admin Service shall maintain existing schema, with new IAM events (tenant operations, SoD violations, service auth) added as new event types.

**CR8: Shared Library Compatibility**  
`IntelliFin.Shared.Authentication` library shall be enhanced with Keycloak integration while maintaining existing interfaces used by downstream services.

---

# 3. Technical Constraints and Integration Requirements

## 3.1 Existing Technology Stack

Based on the document-project analysis:

**Languages:**
- C# / .NET 9.0 (latest LTS)
- SQL (T-SQL for database operations)

**Frameworks:**
- ASP.NET Core 9.0 (Web API framework)
- ASP.NET Identity 9.0 (User/role management framework)
- Entity Framework Core 9.0.8 (ORM)
- MediatR 12.4.1 (CQRS pattern)
- FluentValidation 11.3.0 (Input validation)

**Database:**
- SQL Server (Azure SQL) - `LmsDbContext` (shared context)
- PostgreSQL 13+ - Keycloak backend (StatefulSet)
- Redis - Session cache, token revocation denylist (StackExchange.Redis 2.8.16)

**Authentication & Security:**
- Microsoft.AspNetCore.Authentication.JwtBearer 9.0.0
- System.IdentityModel.Tokens.Jwt 8.14.0
- BCrypt.Net-Next 4.0.3 (password hashing)
- Keycloak 24.0.4 (deployed, not integrated)

**Infrastructure:**
- Kubernetes cluster with 3+ nodes
- YARP (Yet Another Reverse Proxy) - API Gateway
- HashiCorp Vault - Dynamic database credentials
- Linkerd service mesh - mTLS, traffic management

**Observability:**
- Serilog 8.0.4 - Structured logging (Console + File)
- OpenTelemetry - Distributed tracing
- Prometheus - Metrics collection
- Application Insights - Monitoring (inferred)

**External Dependencies:**
- Redis cluster (sessions, caching)
- Admin Service (audit event sink)
- SMTP (via Keycloak: smtp.intellifin.local:587)

**Key Constraints:**
- ⚠️ **Shared DbContext:** Identity Service uses `LmsDbContext` shared with other services
- ⚠️ **No Keycloak Client Library:** Missing `IdentityModel` or `Keycloak.AuthServices` NuGet packages
- ⚠️ **Redis-Only Revocation:** Token revocation not persisted to SQL
- ✅ **Vault Integration:** Dynamic credential rotation already implemented
- ✅ **Production-Ready Keycloak:** 3 replicas, HA setup, monitoring configured

## 3.2 Integration Approach

### Database Integration Strategy

- **Phase 1 - Extend LmsDbContext:** Add new tables (Tenants, TenantUsers, TenantBranches, ServiceAccounts, SoDRules, AuditEvents, TokenRevocations) to existing `LmsDbContext` via EF Core migrations
- **Phase 2 - Preserve ASP.NET Identity Tables:** Keep existing AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims without breaking changes
- **Phase 3 - Dual Storage Pattern:** Maintain user data in both SQL Server (authoritative) and Keycloak (identity provider) with sync via Keycloak Admin API
- **Phase 4 - Audit Persistence:** Store audit events locally in SQL `AuditEvents` table while continuing to forward to Admin Service
- **Phase 5 - Token Revocation Dual Write:** Write revocation events to both Redis (performance) and SQL (persistence/audit)

### API Integration Strategy

**Keycloak Integration:**
- Install `Keycloak.AuthServices.Authentication` NuGet package for OIDC client
- Configure OIDC Authorization Code flow with PKCE (Proof Key for Code Exchange)
- Register Identity Service as confidential client in Keycloak IntelliFin realm
- Use Keycloak Admin REST API for user provisioning (authentication via service account)

**API Gateway Integration:**
- Extend YARP authentication to support dual JWT validation (custom + Keycloak)
- Use JWT "iss" (issuer) claim to route to correct validation pipeline
- Maintain existing branch claim extraction and header forwarding

**Backward Compatibility:**
- Keep existing `/api/auth/login` endpoint functional during migration
- Add new `/api/auth/oidc/login` endpoint for Keycloak redirect flow
- Implement token type detection in API Gateway based on issuer

### Frontend Integration Strategy

**Migration Path:**
- Phase 1: Frontend continues using existing `/api/auth/login` (custom JWT)
- Phase 2: Frontend updated to support OIDC redirect flow to Keycloak
- Phase 3: Dual-token support allows gradual frontend migration
- Phase 4: Deprecate custom JWT endpoints after migration complete

**Session Management:**
- Maintain existing Redis session structure during migration
- Add Keycloak session tracking alongside custom sessions
- Implement session bridging for users transitioning between token types

### Testing Integration Strategy

- **Unit Tests:** Add tests for new services in existing test project
- **Integration Tests:** Test Keycloak OIDC flow with test realm, dual JWT validation, user sync
- **E2E Tests:** Create tests for complete authentication flows (both legacy and OIDC)
- **Migration Tests:** Verify existing functionality remains intact with new IAM features enabled

## 3.3 Code Organization and Standards

**File Structure Approach:**
```
IntelliFin.IdentityService/
├── Controllers/
│   ├── AuthController.cs (existing - extend)
│   ├── OidcController.cs (NEW - OIDC callbacks)
│   ├── TenantController.cs (NEW)
│   ├── ServiceAccountController.cs (NEW)
│   └── Platform/ (existing)
├── Services/
│   ├── IJwtTokenService.cs (existing - keep for migration)
│   ├── IKeycloakService.cs (NEW)
│   ├── ITenantService.cs (NEW)
│   ├── IServiceAccountService.cs (NEW)
│   ├── ISoDValidationService.cs (NEW)
│   └── KeycloakProvisioningService.cs (NEW)
├── Models/
│   ├── Domain/
│   │   ├── Tenant.cs (NEW)
│   │   ├── TenantUser.cs (NEW)
│   │   ├── ServiceAccount.cs (NEW)
│   │   └── SoDRule.cs (NEW)
│   └── DTOs/ (NEW folder)
│       ├── OidcLoginRequest.cs
│       └── TenantDto.cs
├── Data/
│   ├── Migrations/ (NEW - tenancy migrations)
│   └── Seeds/ (NEW - baseline roles)
└── Configuration/
    └── KeycloakConfiguration.cs (NEW)
```

**Naming Conventions:**
- Follow existing C# conventions (PascalCase for classes, camelCase for locals)
- Use `I` prefix for interfaces (existing pattern: `IJwtTokenService`, `IUserService`)
- Entity classes match table names (existing: `ApplicationUser`, `ApplicationRole`)
- Service suffix for business logic classes (existing: `JwtTokenService`, `UserService`)
- Configuration suffix for option classes (existing: `JwtConfiguration`, `PasswordConfiguration`)

**Coding Standards:**
- Maintain existing patterns: async/await for all I/O operations
- Use nullable reference types (`#nullable enable` - already enabled)
- Dependency injection via constructor injection (existing pattern)
- Repository pattern for data access (existing: `IUserRepository`)
- CQRS pattern with MediatR for complex operations (already in use)
- FluentValidation for request validation (already in use)

**Documentation Standards:**
- XML documentation comments for public APIs (/// <summary>)
- README updates for new configuration options
- OpenAPI/Swagger documentation for new endpoints
- Architecture decision records (ADRs) for major choices

## 3.4 Deployment and Operations

**Build Process Integration:**
- No changes to existing .NET build pipeline
- Add Keycloak realm export/import to CI/CD for environment provisioning
- Include EF Core migration execution in deployment scripts
- Add health checks for Keycloak connectivity

**Deployment Strategy:**
- **Blue-Green Deployment:** Deploy new IAM-enhanced version alongside existing, with gradual traffic cutover
- **Feature Flags:** Use configuration toggles to enable/disable Keycloak integration
- **Rollback Plan:** Maintain ability to revert to custom JWT-only mode if issues arise
- **Migration Phases:**
  1. Deploy with dual-token support enabled, Keycloak as secondary
  2. Sync existing users to Keycloak (background job)
  3. Enable Keycloak as primary for new logins
  4. Gradual migration of existing sessions
  5. Deprecate custom JWT after 30-day compatibility window

**Monitoring and Logging:**
- Extend existing Serilog logging with Keycloak operation events
- Add OpenTelemetry spans for Keycloak Admin API calls
- Create Prometheus metrics:
  - `identity_token_generation_duration_seconds` (by token type)
  - `identity_authentication_attempts_total` (by result)
  - `identity_user_provisioning_duration_seconds`
  - `identity_sod_violations_total`
- Alert on:
  - Keycloak connectivity failures
  - Authentication failure rate > 10%
  - User provisioning failures
  - SoD rule violations

**Configuration Management:**
- Keycloak connection settings in `appsettings.json`
- Feature flags for gradual rollout
- Store Keycloak client secret in HashiCorp Vault
- Externalize SoD rules to configuration (JSON)

## 3.5 Risk Assessment and Mitigation

**Technical Risks:**

**Risk 1: Keycloak Integration Complexity**
- **Impact:** High - Core authentication affected
- **Probability:** Medium
- **Mitigation:** POC in dev, comprehensive tests, dual-token fallback, reference documentation

**Risk 2: User Sync Data Loss**
- **Impact:** Critical - User data integrity
- **Probability:** Low
- **Mitigation:** Idempotent provisioning, full backup, dry-run sync, manual verification

**Risk 3: Performance Degradation**
- **Impact:** High - User experience affected
- **Probability:** Medium
- **Mitigation:** Load testing, caching, monitoring, query optimization

**Integration Risks:**

**Risk 4: API Gateway Dual Validation Issues**
- **Impact:** High - All service access affected
- **Probability:** Medium
- **Mitigation:** Thorough testing, canary deployment, rollback capability, monitoring

**Risk 5: Shared DbContext Conflicts**
- **Impact:** Medium - Schema migration issues
- **Probability:** Medium
- **Mitigation:** Careful migration design, isolated testing, team coordination

**Deployment Risks:**

**Risk 6: Migration Downtime**
- **Impact:** Critical - Service unavailable
- **Probability:** Low (with mitigation)
- **Mitigation:** Blue-green deployment, no breaking changes, dual-token support, staging rehearsal

**Risk 7: Client Migration Coordination**
- **Impact:** Medium - Some clients may break
- **Probability:** Medium
- **Mitigation:** 30-day compatibility window, early communication, migration guides, usage monitoring

**Risk 8: Keycloak Infrastructure Failure**
- **Impact:** Critical - No authentication possible
- **Probability:** Low (HA configured)
- **Mitigation:** 3-replica HA, failover testing, circuit breaker, rollback plan

---

# 4. Epic Structure

**Epic Structure Decision:** **Single Comprehensive Epic**

**Reasoning:**
This IAM enhancement represents a cohesive architectural change with tightly coupled components. Breaking into multiple epics would create artificial boundaries and increase integration complexity.

**Rationale:**
1. **Shared Foundation:** All features depend on core Keycloak integration
2. **Migration Coordination:** Migration needs orchestration as one effort
3. **Database Schema Dependencies:** Tables are interconnected
4. **Brownfield Best Practice:** Fewer epic boundaries reduce integration risk
5. **Assessment Recommendation:** Major impact enhancement suited to single epic

**Alternative Considered:** Multiple epics (OIDC Integration, Tenancy, Service Auth) - **Rejected** due to artificial boundaries, coordination overhead, and complicates testing.

---

# 5. Epic 1: Identity & Access Management (IAM) - Keycloak Integration & Multi-Tenancy

## Epic Goal

Transform the Intellifin Identity Service from a custom JWT authentication system to an industry-standard OIDC-based IAM solution powered by Keycloak, while adding multi-tenant support, service-to-service authentication, and Separation of Duties enforcement. The transformation will be executed with zero downtime through a phased migration approach that maintains full backward compatibility with existing systems.

## Integration Requirements

- Integrate with deployed Keycloak infrastructure (v24.0.4, 3 replicas) in IntelliFin realm
- Maintain compatibility with existing API Gateway (YARP) JWT validation
- Preserve all existing Identity Service REST API contracts
- Extend existing LmsDbContext with new identity tables via EF Core migrations
- Sync with existing Redis session management
- Continue forwarding audit events to Admin Service
- Maintain existing branch-based data isolation while adding tenant layer
- Support existing custom JWT tokens during 30-day migration window

## Epic Success Metrics

**Business Metrics:**
1. Authentication Performance: 95th percentile ≤ 200ms
2. System Availability: 99.9% uptime during and after migration
3. Migration Success Rate: 100% users provisioned with zero data loss
4. Security Compliance: Zero critical vulnerabilities (penetration test)
5. Developer Satisfaction: NPS ≥ 8 from development teams

**Technical Metrics:**
1. Zero Downtime: No auth service outages during migration
2. Backward Compatibility: 100% existing API contracts functional
3. Token Validation: <50ms difference between token types
4. Audit Coverage: 100% of auth events logged
5. Test Coverage: ≥80% for new IAM services

**User Experience Metrics:**
1. Login Success Rate: ≥99.5% successful attempts
2. Self-Service Adoption: ≥70% password resets via self-service within 3 months
3. User Errors: <1% auth errors from system issues

**Operational Metrics:**
1. MTTD: Issues detected within 2 minutes
2. MTTR: Rollback within 5 minutes
3. Support Ticket Reduction: 50% reduction within 6 months

---

## Stories

### Story 1.1: Database Schema Extensions for IAM Enhancement

**As a** System Architect,  
**I want** to extend the Identity database with new tables for tenancy, service accounts, SoD rules, and audit persistence,  
**so that** the IAM enhancement has the necessary data structures without disrupting existing identity tables.

**Acceptance Criteria:**

1. EF Core migration creates tables: Tenants, TenantUsers, TenantBranches, ServiceAccounts, ServiceCredentials, SoDRules, AuditEvents, TokenRevocations with proper constraints
2. Indexes created: IX_TenantUsers_UserId, IX_ServiceAccounts_ClientId, IX_AuditEvents_Timestamp, IX_AuditEvents_ActorId, IX_TokenRevocations_TokenId
3. Foreign key relationships established
4. Migration includes rollback script
5. Migration tested in isolated database

**Integration Verification:**
- IV1: Existing Identity Service unit tests pass
- IV2: AspNetUsers, AspNetRoles tables unchanged
- IV3: Existing auth flow functions normally
- IV4: User/role APIs return data correctly
- IV5: LmsDbContext accessible by other services

**Success Metrics:**
- ✅ All 8 tables created with zero migration errors
- ✅ Migration executes in <30 seconds
- ✅ Rollback completes in <10 seconds
- ✅ Zero impact to existing table query performance
- ✅ Schema passes EF Core model validation

---

### Story 1.2: Baseline Role Templates and Seed Data

**As a** System Administrator,  
**I want** baseline role templates and default SoD rules seeded during deployment,  
**so that** consistent role definitions are available across all environments.

**Acceptance Criteria:**

1. Seed roles: System Administrator, Loan Officer, Underwriter, Finance Manager, Collections Officer, Compliance Officer
2. Permission-role mappings in AspNetRoleClaims
3. Default SoD rules: sod-loan-approval, sod-gl-posting, sod-client-approval, sod-payment-reconciliation
4. Idempotent seed data
5. Audit trail for seed data

**Integration Verification:**
- IV1: Existing roles not modified/deleted
- IV2: Existing users retain role assignments
- IV3: Existing permission checks functional
- IV4: No changes to active sessions
- IV5: Existing authorization policies effective

**Success Metrics:**
- ✅ 6 baseline roles with correct permissions
- ✅ 4 SoD rules seeded and active
- ✅ Seed data idempotent (run 3x)
- ✅ Execution time <10 seconds
- ✅ Roles appear correctly in admin UI

---

### Story 1.3: Keycloak Client Registration and Configuration

**As a** System Integrator,  
**I want** the Identity Service registered as an OIDC client in Keycloak,  
**so that** the service can authenticate users via OIDC Authorization Code flow.

**Acceptance Criteria:**

1. Client `intellifin-identity-service` created with confidential access, standard flow enabled
2. Client secret stored in Vault
3. Service account with manage-users, view-users, manage-clients roles
4. Custom client scopes: branch-context, tenant-context, permissions
5. Configuration documented

**Integration Verification:**
- IV1: Existing Keycloak admin user functional
- IV2: API Gateway validation works
- IV3: Realm export includes new client
- IV4: No disruption to Keycloak infrastructure
- IV5: Service account can call Admin API

**Success Metrics:**
- ✅ Client visible in Keycloak admin console
- ✅ Service account authenticates successfully
- ✅ Client secret retrieved from Vault
- ✅ Redirect URIs tested
- ✅ Custom scopes created

---

### Story 1.4: OIDC Client Library Integration

**As a** Backend Developer,  
**I want** OIDC client libraries integrated into Identity Service,  
**so that** the service can initiate Authorization Code flows.

**Acceptance Criteria:**

1. NuGet packages installed: Keycloak.AuthServices.Authentication, Keycloak.AuthServices.Sdk, IdentityModel
2. KeycloakConfiguration class created
3. Services registered: IKeycloakService, IKeycloakUserProvisioningService
4. OidcController with /api/auth/oidc/login, /callback, /logout
5. Feature flag: EnableKeycloakIntegration (default: false)

**Integration Verification:**
- IV1: Existing /api/auth/login unchanged
- IV2: Custom JWT generation works
- IV3: No breaking changes to auth middleware
- IV4: Service starts with integration disabled
- IV5: Existing auth tests pass

**Success Metrics:**
- ✅ No dependency conflicts
- ✅ Service starts without errors
- ✅ OIDC login returns redirect
- ✅ Callback exchanges code for token
- ✅ Unit tests pass

---

### Story 1.5: Dual JWT Validation in API Gateway

**As a** API Gateway Operator,  
**I want** the Gateway to validate both custom and Keycloak tokens,  
**so that** migration can proceed gradually.

**Acceptance Criteria:**

1. Two JWT Bearer schemes: CustomJwt, KeycloakJwt
2. Issuer-based routing logic
3. Branch claim extraction for both types
4. Request headers populated identically
5. Metrics: apigateway_token_validation_total

**Integration Verification:**
- IV1: Existing custom JWT tokens work
- IV2: Backend services receive identical headers
- IV3: Test Keycloak token validates
- IV4: Performance within SLA
- IV5: Fallback to custom JWT

**Success Metrics:**
- ✅ Both token types validate
- ✅ Token type identified correctly
- ✅ Branch claims extracted identically
- ✅ Performance <150ms at p95
- ✅ Load test: 1000 req/sec

---

### Story 1.6: User Provisioning to Keycloak

**As a** System Administrator,  
**I want** existing users automatically provisioned to Keycloak,  
**so that** users can authenticate via OIDC without manual setup.

**Acceptance Criteria:**

1. ProvisionUserAsync, SyncUserAsync, ProvisionAllUsersAsync methods
2. User data synced: username, email, name, branch attributes, roles
3. Idempotent provisioning logic
4. Bulk migration endpoint with dry-run mode
5. Audit logging for provisioning

**Integration Verification:**
- IV1: Existing users log in with custom JWT
- IV2: No AspNetUsers data modifications
- IV3: Provisioning failures don't break auth
- IV4: Existing sessions remain active
- IV5: Sample users verified in Keycloak

**Success Metrics:**
- ✅ Dry-run validates all users
- ✅ 100% active users provisioned
- ✅ 10 random users match
- ✅ Rate: ≥50 users/minute
- ✅ Error handling logs failures

---

### Story 1.7: Multi-Tenancy: Tenant Management APIs

**As a** System Administrator,  
**I want** APIs to create and manage tenants,  
**so that** the platform supports multiple organizations.

**Acceptance Criteria:**

1. TenantController with CRUD endpoints
2. User-tenant assignment APIs
3. Branch-tenant association APIs
4. Authorization: Requires platform:tenants_manage
5. Data validation and audit logging

**Integration Verification:**
- IV1: Users without tenants can authenticate
- IV2: Branch-based authorization works
- IV3: Non-tenant features functional
- IV4: Backward compatible user queries
- IV5: Default tenant for existing users

**Success Metrics:**
- ✅ All 5 CRUD endpoints functional
- ✅ User-tenant assignment works
- ✅ Non-admins receive 403
- ✅ Response time ≤100ms at p95
- ✅ Duplicate codes rejected

---

### Story 1.8: Tenant Context in JWT Claims

**As a** Backend Developer,  
**I want** tenant information in JWT claims,  
**so that** services can enforce tenant-based authorization.

**Acceptance Criteria:**

1. Protocol mapper for tenantId, tenantName
2. User provisioning syncs tenant membership
3. API Gateway extracts X-Tenant-Id, X-Tenant-Name headers
4. Multi-tenant users get default tenant
5. /api/auth/switch-tenant endpoint

**Integration Verification:**
- IV1: Branch claims still present
- IV2: Non-tenant services unaffected
- IV3: Existing authz checks don't break
- IV4: Token size <8KB
- IV5: Users without tenants authenticate

**Success Metrics:**
- ✅ Tokens include tenant claims
- ✅ Gateway forwards tenant headers
- ✅ Switch-tenant returns new token
- ✅ Token size within limits
- ✅ Claims match database

---

### Story 1.9: Service-to-Service Authentication

**As a** Backend Service,  
**I want** OAuth2 Client Credentials flow,  
**so that** service-to-service calls are authenticated.

**Acceptance Criteria:**

1. ServiceAccountController with CRUD endpoints
2. Registration creates SQL record + Keycloak client
3. /api/auth/service-token endpoint
4. Service token scopes defined
5. Audit logging for service auth

**Integration Verification:**
- IV1: Existing service calls work
- IV2: User authentication unaffected
- IV3: Service tokens distinguishable
- IV4: Gateway validates service tokens
- IV5: Token generation <100ms

**Success Metrics:**
- ✅ Registration creates both records
- ✅ Client credentials grant works
- ✅ Service token validated
- ✅ Secrets stored as hash
- ✅ All attempts logged

---

### Story 1.10: Separation of Duties (SoD) Enforcement

**As a** Compliance Officer,  
**I want** automated SoD violation prevention,  
**so that** policies are enforced.

**Acceptance Criteria:**

1. SoDValidationService methods
2. Validation integrated into role assignment
3. SoD override workflow
4. Violation detection scan
5. Metrics: identity_sod_violations_total

**Integration Verification:**
- IV1: Non-conflicting roles assignable
- IV2: Existing assignments unaffected
- IV3: Can disable via feature flag
- IV4: Performance impact <50ms
- IV5: Admin UI functional

**Success Metrics:**
- ✅ Conflicts blocked with details
- ✅ Returns 409 for violations
- ✅ Override requires permission
- ✅ Scan generates report
- ✅ 100% conflicts detected

---

### Story 1.11: Token Introspection and Permission Check APIs

**As a** Downstream Service Developer,  
**I want** APIs to introspect tokens and check permissions,  
**so that** my service can make authorization decisions.

**Acceptance Criteria:**

1. /api/auth/introspect endpoint (RFC 7662)
2. /api/auth/check-permission endpoint
3. User directory API with pagination
4. Group membership CRUD APIs
5. Caching for permission checks (5-min TTL)

**Integration Verification:**
- IV1: Existing authz checks work
- IV2: New APIs don't interfere
- IV3: Performance <150ms uncached
- IV4: Works for both token types
- IV5: Directory respects access control

**Success Metrics:**
- ✅ Introspection returns correct data
- ✅ Permission check accurate
- ✅ Directory paginated
- ✅ ≥90% cache hit rate
- ✅ Rate limiting enforced

---

### Story 1.12: Migration Orchestration and Cutover

**As a** DevOps Engineer,  
**I want** automated migration scripts,  
**so that** transition to Keycloak is safe.

**Acceptance Criteria:**

1. Migration orchestration script with 7 phases
2. Health checks for each phase
3. Rollback procedures documented
4. Monitoring dashboard deployed
5. Communication plan template

**Integration Verification:**
- IV1: Rollback works at any phase
- IV2: Zero downtime during phases
- IV3: No authentication failures
- IV4: All integrations functional
- IV5: Migration pausable/resumable

**Success Metrics:**
- ✅ Script executes without errors
- ✅ Health checks pass
- ✅ Rollback completes in 5 min
- ✅ Zero auth errors
- ✅ Dashboard shows all metrics

---

### Story 1.13: Self-Service Password Reset and Account Management

**As a** End User,  
**I want** self-service account management,  
**so that** I don't need support for routine operations.

**Acceptance Criteria:**

1. Password reset flow via Keycloak
2. Account verification emails
3. Account management endpoints: /api/auth/me
4. Keycloak event listener for sync
5. Email templates with branding

**Integration Verification:**
- IV1: Existing password change works
- IV2: Manual resets by admins work
- IV3: Password policies enforced
- IV4: Profile updates unaffected
- IV5: Email delivery monitored

**Success Metrics:**
- ✅ Reset email sent and received
- ✅ Link expires in 24 hours
- ✅ Verification email on creation
- ✅ Profile syncs to Keycloak
- ✅ ≥95% email delivery rate

---

## Story Dependencies

**Parallel Stories:**
- Stories 1-2 can run in parallel

**Sequential Dependencies:**
- Story 4 requires Story 3
- Story 5 requires Stories 3-4
- Story 6 requires Stories 1, 4
- Story 8 requires Story 7
- Story 12 requires Stories 1-11
- Story 13 requires Stories 3-6

## Estimated Timeline

- Foundation (Stories 1-2): 1 week
- Keycloak Integration (Stories 3-6): 2-3 weeks
- Multi-Tenancy & Features (Stories 7-11): 3-4 weeks
- Migration & Cutover (Story 12): 1 week + 30-day window
- Self-Service (Story 13): 1 week

**Total: 8-10 weeks development + 30-day migration period**

---

# 6. Appendix

## 6.1 Key Deliverables

1. Enhanced Identity Service with Keycloak integration
2. Multi-tenant database schema and APIs
3. Dual JWT validation in API Gateway
4. Service-to-service authentication framework
5. SoD enforcement engine
6. User provisioning and sync automation
7. Migration orchestration scripts
8. Monitoring dashboards and alerts
9. Updated API documentation
10. Architecture decision records (ADRs)

## 6.2 References

- IAM Brownfield Assessment: `domains/identity-access-management/iam-brownfield-assessment.md`
- Keycloak Infrastructure: `infra/keycloak/`
- System Architecture: `docs/architecture/system-architecture.md`
- Tech Stack: `docs/architecture/tech-stack.md`

## 6.3 Glossary

- **OIDC:** OpenID Connect - Authentication layer on top of OAuth2
- **SoD:** Separation of Duties - Security principle preventing conflicting permissions
- **RBAC:** Role-Based Access Control - Authorization model based on roles
- **IdP:** Identity Provider - System that creates, maintains, and manages identity information
- **YARP:** Yet Another Reverse Proxy - Microsoft's reverse proxy toolkit
- **PKCE:** Proof Key for Code Exchange - OAuth2 security extension

---

**END OF PRD**
