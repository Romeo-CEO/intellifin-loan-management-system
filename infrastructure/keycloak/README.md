# Keycloak Configuration for IntelliFin IAM

This directory contains Keycloak realm setup scripts and configuration for the IntelliFin Loan Management System Identity & Access Management (IAM) enhancement.

## Overview

The Keycloak integration provides:
- OpenID Connect (OIDC) authentication and authorization
- Multi-tenant identity management
- Role-based access control (RBAC)
- Service-to-service authentication using OAuth 2.0 client credentials
- Centralized user management and SSO
- Token introspection and validation

## Directory Structure

```
keycloak/
├── README.md                       # This file
├── scripts/
│   ├── Setup-KeycloakRealm.ps1    # Main realm setup script
│   ├── KeycloakHelpers.ps1        # Helper functions
│   └── Test-KeycloakSetup.ps1     # Validation and testing script
└── logs/                          # Script execution logs
```

## Prerequisites

- PowerShell 7+ installed
- Network access to Keycloak server
- Keycloak admin credentials
- (Optional) HashiCorp Vault for secrets management

## Configuration

### Keycloak Server

The setup scripts configure the following in Keycloak:

#### Realm Configuration
- **Realm Name**: `intellifin`
- **Display Name**: IntelliFin Loan Management System
- **Access Token Lifespan**: 15 minutes
- **SSO Session Idle Timeout**: 30 minutes
- **SSO Session Max Lifespan**: 10 hours
- **Brute Force Protection**: Enabled (5 failed attempts → 15 min lockout)
- **Password Policy**: Min 8 chars with uppercase, lowercase, digit, and special char

#### Clients

Three OIDC clients are configured:

1. **intellifin-identity-service** (Confidential)
   - Backend identity service
   - Service accounts enabled
   - Direct access grants enabled
   - Required scopes: openid, profile, email, intellifin-api, tenant

2. **intellifin-api-gateway** (Confidential)
   - API Gateway for request routing
   - Service accounts enabled
   - Standard flow only
   - Required scopes: openid, profile, email, intellifin-api

3. **intellifin-web-app** (Public)
   - Frontend web application
   - Public client (no client secret)
   - Standard flow for user authentication
   - Required scopes: openid, profile, email, intellifin-api, tenant

#### Realm Roles

The following realm roles are created:
- `system-admin` - System Administrator with full access
- `tenant-admin` - Tenant Administrator
- `branch-manager` - Branch Manager
- `loan-officer` - Loan Officer
- `credit-analyst` - Credit Analyst
- `collections-agent` - Collections Agent
- `accountant` - Accountant
- `auditor` - Auditor (read-only)
- `service-account` - Service account for inter-service communication

#### Client Scopes

Custom client scopes:
- `intellifin-api` - Access to IntelliFin API endpoints
- `tenant` - Tenant context information (tenant ID, branches)

## Setup Instructions

### 1. Initial Realm Setup

Run the setup script to create and configure the Keycloak realm:

```powershell
# For development (skip TLS verification)
.\scripts\Setup-KeycloakRealm.ps1 `
    -KeycloakUrl "https://keycloak.intellifin.local:8443" `
    -RealmName "intellifin" `
    -AdminUsername "admin" `
    -SkipTls

# For production (with Vault integration)
.\scripts\Setup-KeycloakRealm.ps1 `
    -KeycloakUrl "https://keycloak.intellifin.local:8443" `
    -RealmName "intellifin" `
    -VaultAddr "https://vault.intellifin.local:8200"
```

The script will:
1. Authenticate with Keycloak admin credentials
2. Create/update the IntelliFin realm
3. Configure realm roles
4. Create client scopes
5. Register OIDC clients
6. Generate and store client secrets (in Vault if available)

### 2. Verify Configuration

Run the test script to validate the setup:

```powershell
.\scripts\Test-KeycloakSetup.ps1 `
    -KeycloakUrl "https://keycloak.intellifin.local:8443" `
    -RealmName "intellifin" `
    -SkipTls
```

The test script validates:
- Keycloak connectivity
- Realm existence and configuration
- Client registrations
- Realm roles
- Client scopes
- OIDC discovery endpoints
- JWKS (signing keys)

### 3. Configure Application Services

Update the application configuration files with Keycloak settings:

#### IntelliFin.IdentityService (appsettings.json)

```json
{
  "Keycloak": {
    "Authority": "https://keycloak.intellifin.local:8443",
    "Realm": "intellifin",
    "ClientId": "intellifin-identity-service",
    "ClientSecret": null,  // Retrieved from Vault at runtime
    "Enabled": true,
    "DualModeEnabled": true,  // Enable during migration
    "RequireHttpsMetadata": true
  }
}
```

#### IntelliFin.ApiGateway (appsettings.json)

```json
{
  "Keycloak": {
    "Authority": "https://keycloak.intellifin.local:8443",
    "Realm": "intellifin",
    "ClientId": "intellifin-api-gateway",
    "ClientSecret": null,  // Retrieved from Vault at runtime
    "Enabled": true
  }
}
```

## Vault Integration

Client secrets are stored in HashiCorp Vault for secure management:

### Vault Paths

- Admin credentials: `/vault/secrets/keycloak-admin-credentials.json`
- Identity Service client secret: `/secret/data/keycloak/clients/identity-service`
- API Gateway client secret: `/secret/data/keycloak/clients/api-gateway`

### Retrieve Secrets

```powershell
# Set Vault token
$env:VAULT_TOKEN = "your-vault-token"

# Retrieve identity service secret
$secret = Invoke-RestMethod `
    -Uri "https://vault.intellifin.local:8200/v1/secret/data/keycloak/clients/identity-service" `
    -Headers @{ "X-Vault-Token" = $env:VAULT_TOKEN }

$clientSecret = $secret.data.data.client_secret
```

## Testing OIDC Flows

### 1. Discovery Endpoint

```powershell
Invoke-RestMethod -Uri "https://keycloak.intellifin.local:8443/realms/intellifin/.well-known/openid-configuration"
```

### 2. Client Credentials Flow (Service-to-Service)

```powershell
$body = @{
    grant_type    = "client_credentials"
    client_id     = "intellifin-identity-service"
    client_secret = $clientSecret
    scope         = "intellifin-api"
}

$response = Invoke-RestMethod `
    -Uri "https://keycloak.intellifin.local:8443/realms/intellifin/protocol/openid-connect/token" `
    -Method Post `
    -Body $body

$accessToken = $response.access_token
```

### 3. Password Grant (User Authentication)

```powershell
$body = @{
    grant_type    = "password"
    client_id     = "intellifin-identity-service"
    client_secret = $clientSecret
    username      = "user@example.com"
    password      = "UserPassword123!"
    scope         = "openid profile email intellifin-api tenant"
}

$response = Invoke-RestMethod `
    -Uri "https://keycloak.intellifin.local:8443/realms/intellifin/protocol/openid-connect/token" `
    -Method Post `
    -Body $body

$accessToken = $response.access_token
$refreshToken = $response.refresh_token
```

### 4. Token Introspection

```powershell
$body = @{
    token         = $accessToken
    client_id     = "intellifin-identity-service"
    client_secret = $clientSecret
}

$introspection = Invoke-RestMethod `
    -Uri "https://keycloak.intellifin.local:8443/realms/intellifin/protocol/openid-connect/token/introspect" `
    -Method Post `
    -Body $body

Write-Host "Token active: $($introspection.active)"
Write-Host "User: $($introspection.username)"
Write-Host "Roles: $($introspection.realm_access.roles -join ', ')"
```

## Troubleshooting

### Common Issues

#### 1. TLS Certificate Errors

For development environments with self-signed certificates:
- Use `-SkipTls` parameter in PowerShell scripts
- Add certificate to trusted root store, or
- Configure application to skip certificate validation (dev only)

#### 2. Authentication Failures

- Verify admin credentials are correct
- Check Keycloak service is running
- Verify network connectivity and firewall rules
- Check Keycloak logs: `docker logs keycloak` (if using Docker)

#### 3. Client Secret Retrieval Failures

- Verify Vault is accessible
- Check VAULT_TOKEN environment variable is set
- Ensure Vault paths exist and contain expected secrets
- Verify Vault policies allow read access

#### 4. Token Validation Errors

- Verify OIDC discovery endpoint is accessible
- Check token hasn't expired
- Ensure client ID and audience match
- Verify issuer URL matches Keycloak realm URL

### Logs

Script execution logs are saved in `logs/` directory:
- `keycloak-setup-YYYYMMDD-HHmmss.log` - Setup script logs
- `keycloak-test-results-YYYYMMDD-HHmmss.json` - Test results in JSON format

## Security Considerations

1. **Client Secrets**: Never commit client secrets to source control. Use Vault or environment variables.

2. **TLS/HTTPS**: Always use HTTPS in production. The `-SkipTls` parameter is for development only.

3. **Token Lifespans**: Configure appropriate token lifespans based on security requirements:
   - Shorter access tokens (15 min) for better security
   - Longer refresh tokens (30 days) for user convenience
   - Balance security and user experience

4. **Password Policies**: Enforce strong password policies in Keycloak realm configuration.

5. **Brute Force Protection**: Enable and configure brute force detection to prevent credential stuffing attacks.

6. **Audit Logging**: Enable Keycloak event logging for security auditing and compliance.

## Next Steps

After Keycloak configuration:

1. **Story 1.3**: Install and configure OIDC client libraries in IdentityService
2. **Story 1.4**: Implement dual-mode JWT validation (custom + Keycloak)
3. **Story 1.5**: Implement user provisioning to sync users to Keycloak
4. **Story 1.6**: Integrate API Gateway with Keycloak token validation

## References

- [Keycloak Documentation](https://www.keycloak.org/documentation)
- [OpenID Connect Specification](https://openid.net/specs/openid-connect-core-1_0.html)
- [OAuth 2.0 RFC 6749](https://tools.ietf.org/html/rfc6749)
- [HashiCorp Vault Documentation](https://www.vaultproject.io/docs)

## Support

For issues or questions:
- Review Keycloak logs
- Check IntelliFin IAM Enhancement PRD: `domains/identity-access-management/iam-enhancement-prd.md`
- Check Architecture Document: `domains/identity-access-management/iam-enhancement-architecture.md`
- Contact DevOps Team
