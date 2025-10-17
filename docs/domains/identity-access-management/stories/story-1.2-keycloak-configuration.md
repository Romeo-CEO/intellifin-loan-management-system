# Story 1.2: Keycloak Configuration & Realm Setup

## Story Information

**Epic:** Foundation Setup (Epic 1)  
**Story ID:** 1.2  
**Story Name:** Keycloak Configuration & Realm Setup  
**Priority:** Critical  
**Estimated Effort:** 3 story points (4-6 hours)  
**Dependencies:** None (parallel to Story 1.1)  
**Blocks:** Stories 1.4, 1.5, 1.6 (all Keycloak-dependent stories)

---

## Story Description

As a **DevOps Engineer / Backend Developer**, I want to **configure Keycloak realm with proper clients, scopes, and role mappings** so that **the Identity Service can integrate with Keycloak for OIDC authentication and user provisioning**.

### Business Value

- Establishes Keycloak as the central identity provider
- Enables OIDC-compliant authentication flows
- Supports custom claims for branch and tenant context
- Provides foundation for service-to-service authentication

### User Story

```
Given the existing Keycloak 24.0.4 deployment (3 replicas)
When I configure the IntelliFin realm with OIDC clients and custom scopes
Then the Identity Service should be able to authenticate users via OIDC
And tokens should contain custom claims (branch, tenant, permissions)
And service accounts should be able to use client credentials flow
```

---

## Acceptance Criteria

### Functional Criteria

- [ ] **AC1:** IntelliFin realm created with proper settings:
  - Login theme: custom IntelliFin theme
  - Token lifespan: 1 hour (access), 30 days (refresh)
  - Session timeout: 8 hours
  - Email verification: enabled

- [ ] **AC2:** OIDC client created for Identity Service:
  - Client ID: `intellifin-identity-service`
  - Client protocol: openid-connect
  - Access type: confidential
  - Valid redirect URIs: configured for all environments
  - PKCE enabled

- [ ] **AC3:** Custom scopes created:
  - `branch-context` (includes branchId, branchName, branchRegion)
  - `tenant-context` (includes tenantId, tenantName)
  - `permissions` (includes user permissions array)

- [ ] **AC4:** Admin service account configured:
  - Client ID: `intellifin-identity-service-admin`
  - Service account enabled
  - Roles: `manage-users`, `view-users`, `manage-clients`

- [ ] **AC5:** Realm roles mapped to existing permissions (80+ atomic permissions)

### Non-Functional Criteria

- [ ] **AC6:** Configuration stored as Infrastructure-as-Code (Keycloak realm export JSON)

- [ ] **AC7:** Client secret stored in HashiCorp Vault (not in git)

- [ ] **AC8:** Configuration validated in all environments (dev, staging, prod)

- [ ] **AC9:** Keycloak Admin Console accessible with SSO

- [ ] **AC10:** Configuration documented with screenshots and CLI commands

---

## Technical Specification

### Realm Configuration

#### Realm Settings

```json
{
  "realm": "IntelliFin",
  "enabled": true,
  "displayName": "IntelliFin Loan Management System",
  "displayNameHtml": "<div class=\"kc-logo-text\"><span>IntelliFin</span></div>",
  "loginTheme": "intellifin",
  "accountTheme": "intellifin",
  "adminTheme": "keycloak",
  "emailTheme": "intellifin",
  "internationalizationEnabled": true,
  "supportedLocales": ["en", "fr"],
  "defaultLocale": "en",
  "sslRequired": "external",
  "registrationAllowed": false,
  "registrationEmailAsUsername": false,
  "rememberMe": true,
  "verifyEmail": true,
  "loginWithEmailAllowed": true,
  "duplicateEmailsAllowed": false,
  "resetPasswordAllowed": true,
  "editUsernameAllowed": false,
  "bruteForceProtected": true,
  "permanentLockout": false,
  "maxFailureWaitSeconds": 900,
  "minimumQuickLoginWaitSeconds": 60,
  "waitIncrementSeconds": 60,
  "quickLoginCheckMilliSeconds": 1000,
  "maxDeltaTimeSeconds": 43200,
  "failureFactor": 5,
  "accessTokenLifespan": 3600,
  "accessTokenLifespanForImplicitFlow": 900,
  "ssoSessionIdleTimeout": 28800,
  "ssoSessionMaxLifespan": 86400,
  "offlineSessionIdleTimeout": 2592000,
  "accessCodeLifespan": 60,
  "accessCodeLifespanUserAction": 300,
  "accessCodeLifespanLogin": 1800
}
```

### Client Configuration: intellifin-identity-service

#### Client Settings

```json
{
  "clientId": "intellifin-identity-service",
  "name": "IntelliFin Identity Service",
  "description": "Main OIDC client for user authentication",
  "enabled": true,
  "protocol": "openid-connect",
  "clientAuthenticatorType": "client-secret",
  "secret": "{{VAULT_PATH}}/keycloak/clients/identity-service/secret",
  "publicClient": false,
  "standardFlowEnabled": true,
  "implicitFlowEnabled": false,
  "directAccessGrantsEnabled": false,
  "serviceAccountsEnabled": false,
  "authorizationServicesEnabled": false,
  "redirectUris": [
    "https://identity.intellifin.local/api/auth/oidc/callback",
    "https://identity-dev.intellifin.local/api/auth/oidc/callback",
    "https://identity-staging.intellifin.local/api/auth/oidc/callback",
    "http://localhost:5001/api/auth/oidc/callback"
  ],
  "webOrigins": [
    "https://intellifin.local",
    "https://dev.intellifin.local",
    "https://staging.intellifin.local",
    "http://localhost:3000"
  ],
  "attributes": {
    "pkce.code.challenge.method": "S256",
    "post.logout.redirect.uris": "+",
    "backchannel.logout.session.required": "true",
    "backchannel.logout.revoke.offline.tokens": "true"
  },
  "defaultClientScopes": [
    "openid",
    "profile",
    "email",
    "roles",
    "web-origins",
    "branch-context",
    "tenant-context",
    "permissions"
  ],
  "optionalClientScopes": [
    "address",
    "phone",
    "offline_access",
    "microprofile-jwt"
  ]
}
```

### Client Configuration: intellifin-identity-service-admin

#### Admin Client Settings (Service Account)

```json
{
  "clientId": "intellifin-identity-service-admin",
  "name": "IntelliFin Identity Service Admin",
  "description": "Service account for user provisioning and management",
  "enabled": true,
  "protocol": "openid-connect",
  "clientAuthenticatorType": "client-secret",
  "secret": "{{VAULT_PATH}}/keycloak/clients/identity-service-admin/secret",
  "publicClient": false,
  "standardFlowEnabled": false,
  "implicitFlowEnabled": false,
  "directAccessGrantsEnabled": false,
  "serviceAccountsEnabled": true,
  "authorizationServicesEnabled": false,
  "attributes": {
    "use.refresh.tokens": "false"
  },
  "defaultClientScopes": [
    "openid",
    "profile",
    "email"
  ]
}
```

**Service Account Roles:**
- `realm-management/manage-users`
- `realm-management/view-users`
- `realm-management/manage-clients`
- `realm-management/view-clients`

### Custom Scopes Configuration

#### Scope 1: branch-context

```json
{
  "name": "branch-context",
  "description": "Branch context for user (branchId, branchName, branchRegion)",
  "protocol": "openid-connect",
  "attributes": {
    "include.in.token.scope": "true",
    "display.on.consent.screen": "false"
  },
  "protocolMappers": [
    {
      "name": "branchId",
      "protocol": "openid-connect",
      "protocolMapper": "oidc-usermodel-attribute-mapper",
      "consentRequired": false,
      "config": {
        "userinfo.token.claim": "true",
        "user.attribute": "branchId",
        "id.token.claim": "true",
        "access.token.claim": "true",
        "claim.name": "branchId",
        "jsonType.label": "String"
      }
    },
    {
      "name": "branchName",
      "protocol": "openid-connect",
      "protocolMapper": "oidc-usermodel-attribute-mapper",
      "consentRequired": false,
      "config": {
        "userinfo.token.claim": "true",
        "user.attribute": "branchName",
        "id.token.claim": "true",
        "access.token.claim": "true",
        "claim.name": "branchName",
        "jsonType.label": "String"
      }
    },
    {
      "name": "branchRegion",
      "protocol": "openid-connect",
      "protocolMapper": "oidc-usermodel-attribute-mapper",
      "consentRequired": false,
      "config": {
        "userinfo.token.claim": "true",
        "user.attribute": "branchRegion",
        "id.token.claim": "true",
        "access.token.claim": "true",
        "claim.name": "branchRegion",
        "jsonType.label": "String"
      }
    }
  ]
}
```

#### Scope 2: tenant-context

```json
{
  "name": "tenant-context",
  "description": "Tenant context for multi-tenancy (tenantId, tenantName)",
  "protocol": "openid-connect",
  "attributes": {
    "include.in.token.scope": "true",
    "display.on.consent.screen": "false"
  },
  "protocolMappers": [
    {
      "name": "tenantId",
      "protocol": "openid-connect",
      "protocolMapper": "oidc-usermodel-attribute-mapper",
      "consentRequired": false,
      "config": {
        "userinfo.token.claim": "true",
        "user.attribute": "tenantId",
        "id.token.claim": "true",
        "access.token.claim": "true",
        "claim.name": "tenantId",
        "jsonType.label": "String"
      }
    },
    {
      "name": "tenantName",
      "protocol": "openid-connect",
      "protocolMapper": "oidc-usermodel-attribute-mapper",
      "consentRequired": false,
      "config": {
        "userinfo.token.claim": "true",
        "user.attribute": "tenantName",
        "id.token.claim": "true",
        "access.token.claim": "true",
        "claim.name": "tenantName",
        "jsonType.label": "String"
      }
    }
  ]
}
```

#### Scope 3: permissions

```json
{
  "name": "permissions",
  "description": "User permissions array (atomic permissions)",
  "protocol": "openid-connect",
  "attributes": {
    "include.in.token.scope": "true",
    "display.on.consent.screen": "false"
  },
  "protocolMappers": [
    {
      "name": "permissions",
      "protocol": "openid-connect",
      "protocolMapper": "oidc-usermodel-attribute-mapper",
      "consentRequired": false,
      "config": {
        "userinfo.token.claim": "true",
        "user.attribute": "permissions",
        "id.token.claim": "true",
        "access.token.claim": "true",
        "claim.name": "permissions",
        "jsonType.label": "JSON",
        "multivalued": "true"
      }
    }
  ]
}
```

---

## Implementation Steps

### Step 1: Export Current Realm (Baseline)

**Command (via Keycloak CLI):**

```bash
# Connect to Keycloak pod
kubectl exec -it keycloak-0 -n keycloak -- /bin/bash

# Export existing realm (if any)
/opt/keycloak/bin/kc.sh export --dir /tmp/export --realm IntelliFin --users skip

# Copy export to local
kubectl cp keycloak/keycloak-0:/tmp/export/IntelliFin-realm.json ./infra/keycloak/realm-baseline.json
```

### Step 2: Create Realm Configuration File

**Location:** `infra/keycloak/realm-config.json`

**Content:** (Use realm configuration JSON from Technical Specification)

### Step 3: Apply Realm Configuration

**Option A: Keycloak Admin UI**

1. Navigate to Keycloak Admin Console: `https://keycloak.intellifin.local`
2. Login with admin credentials
3. Select "Add Realm" → Import → Upload `realm-config.json`
4. Review and click "Create"

**Option B: Keycloak Admin REST API**

```bash
# Get admin token
ADMIN_TOKEN=$(curl -s -X POST "https://keycloak.intellifin.local/realms/master/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=admin" \
  -d "password=${KEYCLOAK_ADMIN_PASSWORD}" \
  -d "grant_type=password" \
  -d "client_id=admin-cli" | jq -r '.access_token')

# Create realm
curl -X POST "https://keycloak.intellifin.local/admin/realms" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d @infra/keycloak/realm-config.json
```

**Option C: Terraform (Recommended for Production)**

```hcl
# infra/terraform/keycloak/realm.tf
resource "keycloak_realm" "intellifin" {
  realm             = "IntelliFin"
  enabled           = true
  display_name      = "IntelliFin Loan Management System"
  
  login_theme       = "intellifin"
  account_theme     = "intellifin"
  
  ssl_required      = "external"
  
  registration_allowed        = false
  remember_me                 = true
  verify_email               = true
  login_with_email_allowed   = true
  duplicate_emails_allowed   = false
  reset_password_allowed     = true
  edit_username_allowed      = false
  
  brute_force_protected      = true
  max_failure_wait_seconds   = 900
  failure_factor             = 5
  
  access_token_lifespan               = 3600
  sso_session_idle_timeout            = 28800
  sso_session_max_lifespan            = 86400
  offline_session_idle_timeout        = 2592000
}
```

### Step 4: Create OIDC Clients

**Identity Service Client:**

```bash
# Create client via REST API
CLIENT_ID="intellifin-identity-service"
CLIENT_SECRET=$(openssl rand -base64 32)

# Store secret in Vault first
vault kv put secret/keycloak/clients/${CLIENT_ID}/secret value="${CLIENT_SECRET}"

# Create client in Keycloak
curl -X POST "https://keycloak.intellifin.local/admin/realms/IntelliFin/clients" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "'${CLIENT_ID}'",
    "name": "IntelliFin Identity Service",
    "enabled": true,
    "protocol": "openid-connect",
    "publicClient": false,
    "standardFlowEnabled": true,
    "secret": "'${CLIENT_SECRET}'",
    "redirectUris": ["https://identity.intellifin.local/api/auth/oidc/callback"],
    "webOrigins": ["https://intellifin.local"],
    "attributes": {
      "pkce.code.challenge.method": "S256"
    }
  }'
```

**Admin Service Account Client:**

```bash
# Create admin client
ADMIN_CLIENT_ID="intellifin-identity-service-admin"
ADMIN_CLIENT_SECRET=$(openssl rand -base64 32)

# Store in Vault
vault kv put secret/keycloak/clients/${ADMIN_CLIENT_ID}/secret value="${ADMIN_CLIENT_SECRET}"

# Create client
curl -X POST "https://keycloak.intellifin.local/admin/realms/IntelliFin/clients" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "'${ADMIN_CLIENT_ID}'",
    "serviceAccountsEnabled": true,
    "standardFlowEnabled": false,
    "secret": "'${ADMIN_CLIENT_SECRET}'"
  }'

# Get service account user ID
SERVICE_ACCOUNT_USER=$(curl -s "https://keycloak.intellifin.local/admin/realms/IntelliFin/clients/${CLIENT_UUID}/service-account-user" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | jq -r '.id')

# Assign realm-management roles
REALM_MGMT_CLIENT=$(curl -s "https://keycloak.intellifin.local/admin/realms/IntelliFin/clients?clientId=realm-management" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | jq -r '.[0].id')

curl -X POST "https://keycloak.intellifin.local/admin/realms/IntelliFin/users/${SERVICE_ACCOUNT_USER}/role-mappings/clients/${REALM_MGMT_CLIENT}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '[
    {"id": "<manage-users-role-id>", "name": "manage-users"},
    {"id": "<view-users-role-id>", "name": "view-users"},
    {"id": "<manage-clients-role-id>", "name": "manage-clients"}
  ]'
```

### Step 5: Create Custom Scopes

**Branch Context Scope:**

```bash
curl -X POST "https://keycloak.intellifin.local/admin/realms/IntelliFin/client-scopes" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d @infra/keycloak/scopes/branch-context.json
```

**Tenant Context Scope:**

```bash
curl -X POST "https://keycloak.intellifin.local/admin/realms/IntelliFin/client-scopes" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d @infra/keycloak/scopes/tenant-context.json
```

**Permissions Scope:**

```bash
curl -X POST "https://keycloak.intellifin.local/admin/realms/IntelliFin/client-scopes" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d @infra/keycloak/scopes/permissions.json
```

### Step 6: Assign Scopes to Client

```bash
# Get client UUID
CLIENT_UUID=$(curl -s "https://keycloak.intellifin.local/admin/realms/IntelliFin/clients?clientId=intellifin-identity-service" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | jq -r '.[0].id')

# Get scope UUIDs
BRANCH_SCOPE_UUID=$(curl -s "https://keycloak.intellifin.local/admin/realms/IntelliFin/client-scopes" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | jq -r '.[] | select(.name=="branch-context") | .id')

TENANT_SCOPE_UUID=$(curl -s "https://keycloak.intellifin.local/admin/realms/IntelliFin/client-scopes" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | jq -r '.[] | select(.name=="tenant-context") | .id')

PERMISSIONS_SCOPE_UUID=$(curl -s "https://keycloak.intellifin.local/admin/realms/IntelliFin/client-scopes" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | jq -r '.[] | select(.name=="permissions") | .id')

# Assign as default scopes
curl -X PUT "https://keycloak.intellifin.local/admin/realms/IntelliFin/clients/${CLIENT_UUID}/default-client-scopes/${BRANCH_SCOPE_UUID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"

curl -X PUT "https://keycloak.intellifin.local/admin/realms/IntelliFin/clients/${CLIENT_UUID}/default-client-scopes/${TENANT_SCOPE_UUID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"

curl -X PUT "https://keycloak.intellifin.local/admin/realms/IntelliFin/clients/${CLIENT_UUID}/default-client-scopes/${PERMISSIONS_SCOPE_UUID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

### Step 7: Verify Configuration

**Test OIDC Discovery:**

```bash
curl -s "https://keycloak.intellifin.local/realms/IntelliFin/.well-known/openid-configuration" | jq
```

**Expected Output:**
```json
{
  "issuer": "https://keycloak.intellifin.local/realms/IntelliFin",
  "authorization_endpoint": "https://keycloak.intellifin.local/realms/IntelliFin/protocol/openid-connect/auth",
  "token_endpoint": "https://keycloak.intellifin.local/realms/IntelliFin/protocol/openid-connect/token",
  "userinfo_endpoint": "https://keycloak.intellifin.local/realms/IntelliFin/protocol/openid-connect/userinfo",
  "end_session_endpoint": "https://keycloak.intellifin.local/realms/IntelliFin/protocol/openid-connect/logout",
  "jwks_uri": "https://keycloak.intellifin.local/realms/IntelliFin/protocol/openid-connect/certs",
  "introspection_endpoint": "https://keycloak.intellifin.local/realms/IntelliFin/protocol/openid-connect/token/introspect",
  "scopes_supported": [
    "openid",
    "profile",
    "email",
    "branch-context",
    "tenant-context",
    "permissions",
    "offline_access"
  ]
}
```

**Test Client Credentials Flow (Admin Client):**

```bash
# Get token for admin service account
curl -X POST "https://keycloak.intellifin.local/realms/IntelliFin/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=intellifin-identity-service-admin" \
  -d "client_secret=${ADMIN_CLIENT_SECRET}" \
  -d "grant_type=client_credentials" | jq

# Expected: Valid access token with realm-management roles
```

---

## Testing Requirements

### Configuration Validation Tests

**Test 1: Realm Exists and Enabled**

```bash
# Verify realm
REALM_INFO=$(curl -s "https://keycloak.intellifin.local/admin/realms/IntelliFin" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}")

echo $REALM_INFO | jq '.enabled'
# Expected: true
```

**Test 2: OIDC Client Configured**

```bash
# Verify client
curl -s "https://keycloak.intellifin.local/admin/realms/IntelliFin/clients?clientId=intellifin-identity-service" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | jq '.[0] | {clientId, enabled, standardFlowEnabled, publicClient}'

# Expected: {"clientId": "intellifin-identity-service", "enabled": true, "standardFlowEnabled": true, "publicClient": false}
```

**Test 3: Custom Scopes Exist**

```bash
# Verify scopes
curl -s "https://keycloak.intellifin.local/admin/realms/IntelliFin/client-scopes" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | jq '[.[] | select(.name | IN("branch-context", "tenant-context", "permissions")) | .name]'

# Expected: ["branch-context", "tenant-context", "permissions"]
```

**Test 4: Admin Service Account Has Roles**

```bash
# Get service account roles
SERVICE_ACCOUNT_USER=$(curl -s "https://keycloak.intellifin.local/admin/realms/IntelliFin/clients/${ADMIN_CLIENT_UUID}/service-account-user" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | jq -r '.id')

curl -s "https://keycloak.intellifin.local/admin/realms/IntelliFin/users/${SERVICE_ACCOUNT_USER}/role-mappings" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | jq '.clientMappings["realm-management"].mappings[].name'

# Expected: "manage-users", "view-users", "manage-clients"
```

### Integration Tests

**Test Authorization Code Flow (Manual):**

1. Navigate to: `https://keycloak.intellifin.local/realms/IntelliFin/protocol/openid-connect/auth?client_id=intellifin-identity-service&redirect_uri=https://identity.intellifin.local/api/auth/oidc/callback&response_type=code&scope=openid%20profile%20email%20branch-context%20tenant-context%20permissions&state=test123&code_challenge=CHALLENGE&code_challenge_method=S256`
2. Login with test user credentials
3. Verify redirect to callback URL with authorization code
4. Exchange code for tokens (via Identity Service)
5. Decode access token and verify custom claims present

**Test Token Claims:**

```bash
# Decode access token (after OIDC flow)
echo $ACCESS_TOKEN | cut -d'.' -f2 | base64 -d | jq

# Verify claims present:
# - iss: "https://keycloak.intellifin.local/realms/IntelliFin"
# - sub: user ID
# - branchId, branchName, branchRegion (branch-context scope)
# - tenantId, tenantName (tenant-context scope)
# - permissions: [] (permissions scope)
```

---

## Integration Verification

### Checkpoint 1: Realm Configuration Valid

**Verification:**
```bash
curl -s "https://keycloak.intellifin.local/realms/IntelliFin/.well-known/openid-configuration" | jq '.issuer'
```

**Success Criteria:** Returns `"https://keycloak.intellifin.local/realms/IntelliFin"`

### Checkpoint 2: Client Secrets in Vault

**Verification:**
```bash
vault kv get secret/keycloak/clients/intellifin-identity-service/secret
vault kv get secret/keycloak/clients/intellifin-identity-service-admin/secret
```

**Success Criteria:** Both secrets retrieved successfully (not shown in output)

### Checkpoint 3: Custom Scopes Assigned

**Verification:**
```bash
curl -s "https://keycloak.intellifin.local/admin/realms/IntelliFin/clients/${CLIENT_UUID}/default-client-scopes" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | jq '[.[] | .name] | sort'
```

**Success Criteria:** Includes `["branch-context", "tenant-context", "permissions"]`

### Checkpoint 4: Admin Client Can Manage Users

**Verification:**
```bash
# Get admin token
ADMIN_TOKEN=$(curl -s -X POST "https://keycloak.intellifin.local/realms/IntelliFin/protocol/openid-connect/token" \
  -d "client_id=intellifin-identity-service-admin" \
  -d "client_secret=${ADMIN_CLIENT_SECRET}" \
  -d "grant_type=client_credentials" | jq -r '.access_token')

# Test user management
curl -s "https://keycloak.intellifin.local/admin/realms/IntelliFin/users?max=1" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | jq 'length'
```

**Success Criteria:** Returns user count (no 403 Forbidden)

### Checkpoint 5: Configuration Exported

**Verification:**
```bash
ls -lh infra/keycloak/realm-config.json
```

**Success Criteria:** File exists and >50KB (complete realm export)

---

## Rollback Plan

### Remove Realm

```bash
# Via REST API
curl -X DELETE "https://keycloak.intellifin.local/admin/realms/IntelliFin" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"

# Via Admin UI
# Navigate to Realm Settings → Delete Realm
```

### Remove Vault Secrets

```bash
vault kv delete secret/keycloak/clients/intellifin-identity-service/secret
vault kv delete secret/keycloak/clients/intellifin-identity-service-admin/secret
```

---

## Definition of Done

- [ ] IntelliFin realm created with correct settings
- [ ] OIDC client `intellifin-identity-service` configured with PKCE
- [ ] Admin service account `intellifin-identity-service-admin` configured with roles
- [ ] Custom scopes (branch-context, tenant-context, permissions) created
- [ ] All client secrets stored in HashiCorp Vault
- [ ] Realm configuration exported to `infra/keycloak/realm-config.json`
- [ ] OIDC discovery endpoint returns valid configuration
- [ ] Admin service account can query users via Admin API
- [ ] All 5 integration verification checkpoints pass
- [ ] Configuration documented with screenshots
- [ ] PR merged to `feature/iam-enhancement` branch

---

## Dependencies

**Upstream Dependencies:** None

**Downstream Dependencies:**
- Story 1.4 (API Gateway) - needs OIDC endpoints
- Story 1.5 (User Provisioning) - needs Admin API client
- Story 1.6 (OIDC Flow) - needs OIDC client configuration

---

## Notes for Developers

### Client Secret Rotation

```bash
# Generate new secret
NEW_SECRET=$(openssl rand -base64 32)

# Update in Keycloak
curl -X PUT "https://keycloak.intellifin.local/admin/realms/IntelliFin/clients/${CLIENT_UUID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"secret": "'${NEW_SECRET}'"}'

# Update in Vault
vault kv put secret/keycloak/clients/intellifin-identity-service/secret value="${NEW_SECRET}"
```

### Theme Customization

Custom IntelliFin theme located at:
- `infra/keycloak/themes/intellifin/login/`
- `infra/keycloak/themes/intellifin/account/`
- `infra/keycloak/themes/intellifin/email/`

Deploy theme:
```bash
kubectl cp infra/keycloak/themes/intellifin keycloak/keycloak-0:/opt/keycloak/themes/
kubectl exec -it keycloak-0 -n keycloak -- /bin/bash -c "chown -R keycloak:keycloak /opt/keycloak/themes/intellifin"
kubectl rollout restart statefulset/keycloak -n keycloak
```

---

**END OF STORY 1.2**
