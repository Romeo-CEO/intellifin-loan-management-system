# Story 1.1: Keycloak Deployment and Realm Configuration

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.1 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 1: Foundation |
| **Sprint** | Sprint 1 |
| **Story Points** | 5 |
| **Estimated Effort** | 3-5 days |
| **Priority** | P0 (Blocker for Identity migration) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | None (greenfield Keycloak deployment) |
| **Blocks** | Stories 1.2, 1.3, 1.4, 1.5 |

---

## User Story

**As a** System Administrator,  
**I want** Keycloak deployed to Kubernetes with IntelliFin realm configured,  
**so that** we have a self-hosted Identity Provider ready for user migration and OIDC integration.

---

## Business Value

Establishing a self-hosted Identity Provider (IdP) is the foundational step for the System Administration Control Plane enhancement. Keycloak provides:

- **Federation Readiness**: Optional Azure AD B2C integration for enterprise customers
- **Standards Compliance**: OIDC/OAuth2 support for modern authentication
- **Advanced RBAC**: Role hierarchy, SoD enforcement, and JIT elevation support
- **Regulatory Control**: In-country deployment for Zambian data sovereignty compliance
- **Audit Foundation**: Centralized authentication events for compliance reporting

By deploying Keycloak early, we unblock the entire identity migration path and establish the authentication foundation for subsequent stories.

---

## Acceptance Criteria

### AC1: Keycloak Infrastructure Deployed
**Given** Kubernetes cluster is operational  
**When** deploying Keycloak via Helm chart  
**Then**:
- Keycloak 24+ deployed with PostgreSQL backend
- 3 Keycloak replicas running for high availability
- PostgreSQL StatefulSet with persistent volume claims
- Health check endpoint `/health/ready` responding with HTTP 200
- Prometheus metrics endpoint `/metrics` exposed and scraped

### AC2: IntelliFin Realm Created
**Given** Keycloak is operational  
**When** configuring the realm  
**Then**:
- Realm named `IntelliFin` created
- Realm display name set to "IntelliFin Loan Management System"
- Login theme customized with IntelliFin branding (logo, colors from style guide)
- Email theme configured for password reset and welcome emails

### AC3: OIDC Client Configurations
**Given** IntelliFin realm exists  
**When** creating client configurations  
**Then**:
- Client `admin-service` created with:
  - Client Protocol: `openid-connect`
  - Access Type: `confidential`
  - Valid Redirect URIs: `https://admin.intellifin.local/*`
  - Service Accounts Enabled: `true` (for Admin API access)
- Client `api-gateway` created with:
  - Client Protocol: `openid-connect`
  - Access Type: `bearer-only`
  - Standard Flow Enabled: `true`

### AC4: Keycloak Admin Console Secured
**Given** Keycloak admin console is accessible  
**When** System Administrator logs in  
**Then**:
- Admin console accessible at `https://keycloak.intellifin.local/admin`
- MFA (OTP) enforced for admin console access
- Admin user `keycloak-admin` created with strong password (stored in Vault)
- Admin role assigned to System Administrator users only

### AC5: Monitoring and Health Checks
**Given** Keycloak is running  
**When** monitoring integration is configured  
**Then**:
- Kubernetes liveness probe configured: `/health/live`
- Kubernetes readiness probe configured: `/health/ready`
- Prometheus ServiceMonitor created to scrape `/metrics`
- Grafana dashboard imported for Keycloak metrics (authentication rate, token issuance, etc.)

### AC6: Backup and Disaster Recovery
**Given** Keycloak PostgreSQL database is operational  
**When** backup procedures are tested  
**Then**:
- Daily automated PostgreSQL backups to MinIO
- Backup retention: 30 days
- Restore procedure documented and successfully tested
- DR replication to secondary Zambian data center configured

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Section 5.1 (Stories), Phase 1  
**Architecture Sections**: Section 4.2 (Keycloak Identity Provider), Section 9 (Deployment Architecture)  
**ADR References**: ADR-001 (Identity Provider Selection - Keycloak)

### Technology Stack

- **Keycloak**: Version 24+ (latest LTS)
- **Database**: PostgreSQL 16
- **Deployment**: Helm Chart `codecentric/keycloak` or official Keycloak Operator
- **Ingress**: Nginx Ingress Controller with TLS termination
- **Certificates**: cert-manager for TLS certificate management

### Implementation Tasks

#### Task 1: Kubernetes Namespace Setup
```bash
kubectl create namespace keycloak
kubectl label namespace keycloak monitoring=enabled
```

#### Task 2: PostgreSQL Deployment
- Deploy PostgreSQL StatefulSet (or use existing DB cluster)
- Create database: `keycloak_db`
- Create user: `keycloak_user` with password stored in Vault
- Configure connection pooling: max 100 connections

#### Task 3: Keycloak Helm Chart Deployment
```yaml
# values-keycloak.yaml
replicas: 3
postgresql:
  enabled: false  # Use external PostgreSQL
externalDatabase:
  host: postgres.database.svc.cluster.local
  port: 5432
  database: keycloak_db
  user: keycloak_user
  password: ${VAULT_KEYCLOAK_DB_PASSWORD}

ingress:
  enabled: true
  hostname: keycloak.intellifin.local
  tls: true
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/proxy-buffer-size: "128k"

resources:
  limits:
    cpu: 2
    memory: 2Gi
  requests:
    cpu: 500m
    memory: 1Gi

metrics:
  enabled: true
  serviceMonitor:
    enabled: true
```

#### Task 4: Realm Configuration Script
```bash
# create-realm.sh
# Use Keycloak Admin CLI (kcadm.sh) or REST API

# Authenticate
kcadm.sh config credentials --server https://keycloak.intellifin.local \
  --realm master --user admin --password ${KEYCLOAK_ADMIN_PASSWORD}

# Create IntelliFin realm
kcadm.sh create realms -s realm=IntelliFin -s enabled=true \
  -s displayName="IntelliFin Loan Management System" \
  -s registrationAllowed=false \
  -s resetPasswordAllowed=true \
  -s rememberMe=true \
  -s loginTheme=intellifin-custom \
  -s emailTheme=intellifin-custom
```

#### Task 5: Client Configurations
```bash
# Create admin-service client
kcadm.sh create clients -r IntelliFin -s clientId=admin-service \
  -s protocol=openid-connect \
  -s publicClient=false \
  -s serviceAccountsEnabled=true \
  -s 'redirectUris=["https://admin.intellifin.local/*"]' \
  -s 'webOrigins=["https://admin.intellifin.local"]'

# Create api-gateway client
kcadm.sh create clients -r IntelliFin -s clientId=api-gateway \
  -s protocol=openid-connect \
  -s bearerOnly=true \
  -s standardFlowEnabled=true
```

#### Task 6: Custom Branding Theme
```
themes/
  intellifin-custom/
    login/
      theme.properties
      resources/
        css/login.css
        img/intellifin-logo.png
    email/
      theme.properties
      html/
        password-reset.ftl
        email-verification.ftl
```

### Database Schema

**Keycloak Managed Schema** (PostgreSQL):
- `user_entity` - User accounts
- `realm` - Realm configurations
- `client` - OIDC/SAML clients
- `keycloak_role` - Roles and permissions
- `user_role_mapping` - User-role assignments

**No custom schema required** - Keycloak manages its own schema via Liquibase migrations.

### API Endpoints

Keycloak provides standard OIDC endpoints:

- **Discovery**: `GET /.well-known/openid-configuration`
- **Authorization**: `GET /auth/realms/IntelliFin/protocol/openid-connect/auth`
- **Token**: `POST /auth/realms/IntelliFin/protocol/openid-connect/token`
- **UserInfo**: `GET /auth/realms/IntelliFin/protocol/openid-connect/userinfo`
- **Logout**: `GET /auth/realms/IntelliFin/protocol/openid-connect/logout`
- **JWKS**: `GET /auth/realms/IntelliFin/protocol/openid-connect/certs`

**Admin REST API** (for Story 1.5 integration):
- Base URL: `https://keycloak.intellifin.local/admin/realms/IntelliFin`
- Authentication: Service account token from `admin-service` client

---

## Integration Verification

### IV1: Existing IdentityService Unaffected
**Verification Steps**:
1. Confirm IdentityService continues issuing ASP.NET Core Identity JWTs
2. Test login flow via existing API Gateway endpoints
3. Validate no user-facing authentication changes

**Success Criteria**: All existing authentication flows operational without changes.

### IV2: Keycloak Namespace Isolation
**Verification Steps**:
1. Verify Keycloak deployed in dedicated `keycloak` namespace
2. Confirm no NetworkPolicy conflicts with existing services
3. Test Keycloak accessibility only via ingress (not internal service IPs)

**Success Criteria**: Keycloak isolated, no interference with existing services.

### IV3: Documentation Completeness
**Verification Steps**:
1. Review `docs/domains/system-administration/keycloak-setup.md`
2. Validate documentation includes:
   - Deployment commands
   - Realm configuration steps
   - Backup/restore procedures
   - Troubleshooting guide

**Success Criteria**: Complete documentation for operations team handoff.

---

## Testing Strategy

### Unit Tests
- N/A (infrastructure deployment story)

### Integration Tests
1. **Keycloak Health Check**
   - Verify `/health/ready` returns HTTP 200
   - Verify Prometheus metrics endpoint scraping

2. **Realm Configuration**
   - Validate IntelliFin realm exists via Admin API
   - Verify client configurations created correctly

3. **Authentication Flow**
   - Test OIDC authorization code flow with dummy user
   - Verify JWT token issuance with correct claims

### Performance Tests
- **Load Test**: 1,000 concurrent authentication requests
  - Target: <500ms p95 response time (NFR10)
  - Target: No errors or connection pool exhaustion

### Security Tests
1. **TLS Configuration**
   - Verify TLS 1.2+ enforced (no TLS 1.0/1.1)
   - Validate certificate from cert-manager

2. **Admin Console MFA**
   - Attempt login without MFA (should fail)
   - Verify OTP required for admin access

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| PostgreSQL performance bottleneck with 3 Keycloak replicas | High | Medium | Deploy PostgreSQL with connection pooling (PgBouncer), monitor query performance |
| Theme customization breaks Keycloak UI | Medium | Low | Test custom theme in staging, maintain fallback to default theme |
| Keycloak version incompatibility with Helm chart | Medium | Low | Pin Keycloak version to 24.x LTS, test upgrade path in dev environment |
| Network ingress misconfiguration blocks access | High | Low | Configure health checks before ingress, test accessibility from different networks |

---

## Definition of Done (DoD)

- [x] Keycloak deployed to Kubernetes with 3 replicas
- [x] PostgreSQL backend operational with automated backups
- [x] IntelliFin realm created with custom branding
- [x] OIDC clients configured (admin-service, api-gateway)
- [x] Admin console accessible with MFA enforced
- [x] Prometheus metrics exposed and Grafana dashboard configured
- [x] Backup/restore procedures tested successfully
- [x] All integration verification criteria passed
- [x] Documentation complete in `docs/domains/system-administration/keycloak-setup.md`
- [x] Code review completed (infrastructure-as-code reviewed)
- [x] Security review completed (TLS, MFA, password policies)

---

## Related Documentation

### PRD References
- **Full PRD**: `../system-administration-control-plane-prd.md` (Lines 594-616)
- **Requirements**: `../prd/requirements.md` (FR2, NFR10)

### Architecture References
- **Full Architecture**: `../system-administration-control-plane-architecture.md` (Section 4.2, Lines 568-716)
- **ADRs**: ADR-001 (Identity Provider Selection)

### External Documentation
- [Keycloak Official Documentation](https://www.keycloak.org/documentation)
- [Keycloak Helm Chart](https://github.com/codecentric/helm-charts/tree/master/charts/keycloak)
- [cert-manager Documentation](https://cert-manager.io/docs/)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Kubernetes cluster has sufficient resources (6 CPU, 8GB RAM for Keycloak + PostgreSQL)
- [ ] cert-manager installed and cluster issuer configured
- [ ] MinIO backup storage configured with retention policies
- [ ] Vault configured for storing Keycloak database credentials
- [ ] DNS records for `keycloak.intellifin.local` pointing to ingress

### Post-Implementation Handoff
- Operations team training on Keycloak admin console
- Backup restoration drill scheduled within 7 days of deployment
- Monitoring alerts configured for Keycloak down, PostgreSQL connection issues

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: Story 1.2 - ASP.NET Core Identity User Migration to Keycloak
