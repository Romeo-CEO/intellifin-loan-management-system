<#
.SYNOPSIS
    Sets up Keycloak realm, clients, roles, and scopes for IntelliFin IAM integration.

.DESCRIPTION
    This script configures a Keycloak realm with all necessary components for the IntelliFin
    Loan Management System IAM enhancement:
    - Creates/updates the IntelliFin realm
    - Configures OIDC clients (identity-service, api-gateway, frontend clients)
    - Sets up realm roles and client roles
    - Configures client scopes and mappers
    - Integrates with HashiCorp Vault for secrets management

.PARAMETER KeycloakUrl
    Base URL of the Keycloak server (e.g., https://keycloak.intellifin.local:8443)

.PARAMETER AdminUsername
    Keycloak admin username (default: admin)

.PARAMETER AdminPassword
    Keycloak admin password. If not provided, will attempt to read from Vault.

.PARAMETER RealmName
    Name of the realm to create/update (default: intellifin)

.PARAMETER VaultAddr
    Vault server address for secrets retrieval

.PARAMETER SkipTls
    Skip TLS verification for development environments

.EXAMPLE
    .\Setup-KeycloakRealm.ps1 -KeycloakUrl "https://keycloak.intellifin.local:8443" -RealmName "intellifin" -SkipTls

.EXAMPLE
    .\Setup-KeycloakRealm.ps1 -KeycloakUrl "https://keycloak.intellifin.local:8443" -VaultAddr "http://vault:8200"

.NOTES
    Author: IntelliFin DevOps Team
    Version: 1.0.0
    Requires: PowerShell 7+, kcadm.sh or REST API access
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$KeycloakUrl,

    [Parameter(Mandatory = $false)]
    [string]$AdminUsername = "admin",

    [Parameter(Mandatory = $false)]
    [string]$AdminPassword,

    [Parameter(Mandatory = $false)]
    [string]$RealmName = "intellifin",

    [Parameter(Mandatory = $false)]
    [string]$VaultAddr,

    [Parameter(Mandatory = $false)]
    [switch]$SkipTls
)

# Set strict mode and error handling
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Import helper functions
. "$PSScriptRoot\KeycloakHelpers.ps1"

# Initialize logging
$LogFile = Join-Path $PSScriptRoot "..\logs\keycloak-setup-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
New-Item -ItemType Directory -Force -Path (Split-Path $LogFile) | Out-Null

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage
    Add-Content -Path $LogFile -Value $logMessage
}

function Get-VaultSecret {
    param([string]$Path)
    
    if (-not $VaultAddr) {
        throw "Vault address not provided. Use -VaultAddr parameter or set VAULT_ADDR environment variable."
    }

    Write-Log "Retrieving secret from Vault: $Path"
    
    $headers = @{
        "X-Vault-Token" = $env:VAULT_TOKEN
    }

    try {
        $response = Invoke-RestMethod -Uri "$VaultAddr/v1/$Path" -Headers $headers -Method Get
        return $response.data.data
    }
    catch {
        Write-Log "Failed to retrieve secret from Vault: $_" -Level "ERROR"
        throw
    }
}

function Initialize-KeycloakAuth {
    Write-Log "Authenticating with Keycloak..."
    
    if (-not $AdminPassword -and $VaultAddr) {
        Write-Log "Retrieving admin credentials from Vault"
        $vaultData = Get-VaultSecret -Path "secret/data/keycloak/admin"
        $script:AdminPassword = $vaultData.password
    }

    if (-not $AdminPassword) {
        $securePassword = Read-Host "Enter Keycloak admin password" -AsSecureString
        $script:AdminPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
        )
    }

    # Get admin access token
    $tokenEndpoint = "$KeycloakUrl/realms/master/protocol/openid-connect/token"
    $body = @{
        username      = $AdminUsername
        password      = $AdminPassword
        grant_type    = "password"
        client_id     = "admin-cli"
    }

    try {
        if ($SkipTls) {
            $response = Invoke-RestMethod -Uri $tokenEndpoint -Method Post -Body $body -SkipCertificateCheck
        } else {
            $response = Invoke-RestMethod -Uri $tokenEndpoint -Method Post -Body $body
        }
        
        $script:AccessToken = $response.access_token
        Write-Log "Successfully authenticated with Keycloak"
        return $true
    }
    catch {
        Write-Log "Failed to authenticate: $_" -Level "ERROR"
        throw
    }
}

function Invoke-KeycloakApi {
    param(
        [string]$Endpoint,
        [string]$Method = "GET",
        [object]$Body,
        [switch]$NoRealm
    )

    $baseUrl = if ($NoRealm) { "$KeycloakUrl/admin" } else { "$KeycloakUrl/admin/realms/$RealmName" }
    $uri = "$baseUrl$Endpoint"

    $headers = @{
        "Authorization" = "Bearer $AccessToken"
        "Content-Type"  = "application/json"
    }

    $params = @{
        Uri     = $uri
        Method  = $Method
        Headers = $headers
    }

    if ($Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 10)
    }

    if ($SkipTls) {
        $params.SkipCertificateCheck = $true
    }

    try {
        return Invoke-RestMethod @params
    }
    catch {
        Write-Log "API call failed: $Method $uri - $_" -Level "ERROR"
        throw
    }
}

function New-KeycloakRealm {
    Write-Log "Creating/updating realm: $RealmName"

    $realmConfig = @{
        realm                   = $RealmName
        enabled                 = $true
        displayName             = "IntelliFin Loan Management System"
        displayNameHtml         = "<b>IntelliFin LMS</b>"
        accessTokenLifespan     = 900  # 15 minutes
        accessTokenLifespanForImplicitFlow = 900
        ssoSessionIdleTimeout   = 1800  # 30 minutes
        ssoSessionMaxLifespan   = 36000  # 10 hours
        offlineSessionIdleTimeout = 2592000  # 30 days
        accessCodeLifespan      = 60
        accessCodeLifespanUserAction = 300
        registrationAllowed     = $false
        registrationEmailAsUsername = $true
        editUsernameAllowed     = $false
        resetPasswordAllowed    = $true
        rememberMe              = $true
        verifyEmail             = $true
        loginWithEmailAllowed   = $true
        duplicateEmailsAllowed  = $false
        bruteForceProtected     = $true
        permanentLockout        = $false
        maxFailureWaitSeconds   = 900
        minimumQuickLoginWaitSeconds = 60
        waitIncrementSeconds    = 60
        quickLoginCheckMilliSeconds = 1000
        maxDeltaTimeSeconds     = 43200
        failureFactor           = 5
        passwordPolicy          = "length(8) and upperCase(1) and lowerCase(1) and digits(1) and specialChars(1) and notUsername"
        internationalizationEnabled = $true
        supportedLocales        = @("en", "es", "fr")
        defaultLocale           = "en"
    }

    try {
        # Check if realm exists
        $existingRealm = Invoke-KeycloakApi -Endpoint "" -NoRealm
        if ($existingRealm.realm -contains $RealmName) {
            Write-Log "Realm $RealmName already exists, updating..."
            Invoke-KeycloakApi -Endpoint "" -Method PUT -Body $realmConfig | Out-Null
        }
    }
    catch {
        Write-Log "Creating new realm $RealmName..."
        Invoke-KeycloakApi -Endpoint "" -Method POST -Body $realmConfig -NoRealm | Out-Null
    }

    Write-Log "Realm $RealmName configured successfully"
}

function New-KeycloakClient {
    param(
        [string]$ClientId,
        [string]$Name,
        [string]$Description,
        [string[]]$RedirectUris,
        [string[]]$WebOrigins,
        [bool]$PublicClient = $false,
        [bool]$ServiceAccountsEnabled = $false,
        [bool]$DirectAccessGrantsEnabled = $true,
        [string[]]$DefaultScopes
    )

    Write-Log "Creating/updating client: $ClientId"

    $clientConfig = @{
        clientId                  = $ClientId
        name                      = $Name
        description               = $Description
        enabled                   = $true
        publicClient              = $PublicClient
        protocol                  = "openid-connect"
        standardFlowEnabled       = $true
        implicitFlowEnabled       = $false
        directAccessGrantsEnabled = $DirectAccessGrantsEnabled
        serviceAccountsEnabled    = $ServiceAccountsEnabled
        authorizationServicesEnabled = $false
        redirectUris              = $RedirectUris
        webOrigins                = $WebOrigins
        bearerOnly                = $false
        consentRequired           = $false
        fullScopeAllowed          = $false
        attributes                = @{
            "access.token.lifespan"                    = "900"
            "client.session.idle.timeout"              = "1800"
            "client.session.max.lifespan"              = "36000"
            "backchannel.logout.session.required"      = "true"
            "backchannel.logout.revoke.offline.tokens" = "true"
        }
    }

    if ($DefaultScopes) {
        $clientConfig.defaultClientScopes = $DefaultScopes
        $clientConfig.optionalClientScopes = @("address", "phone", "offline_access")
    }

    try {
        $existingClients = Invoke-KeycloakApi -Endpoint "/clients?clientId=$ClientId"
        if ($existingClients.Count -gt 0) {
            $clientUuid = $existingClients[0].id
            Write-Log "Client $ClientId exists, updating..."
            Invoke-KeycloakApi -Endpoint "/clients/$clientUuid" -Method PUT -Body $clientConfig | Out-Null
            
            # Return client secret if confidential
            if (-not $PublicClient) {
                $secret = Invoke-KeycloakApi -Endpoint "/clients/$clientUuid/client-secret"
                return $secret.value
            }
        }
    }
    catch {
        Write-Log "Creating new client $ClientId..."
        Invoke-KeycloakApi -Endpoint "/clients" -Method POST -Body $clientConfig | Out-Null
        
        # Retrieve and return client secret
        if (-not $PublicClient) {
            Start-Sleep -Seconds 1
            $clients = Invoke-KeycloakApi -Endpoint "/clients?clientId=$ClientId"
            $clientUuid = $clients[0].id
            $secret = Invoke-KeycloakApi -Endpoint "/clients/$clientUuid/client-secret"
            return $secret.value
        }
    }

    Write-Log "Client $ClientId configured successfully"
}

function New-KeycloakRealmRole {
    param(
        [string]$RoleName,
        [string]$Description
    )

    Write-Log "Creating realm role: $RoleName"

    $roleConfig = @{
        name        = $RoleName
        description = $Description
        composite   = $false
    }

    try {
        $existingRole = Invoke-KeycloakApi -Endpoint "/roles/$RoleName"
        Write-Log "Role $RoleName already exists"
    }
    catch {
        Invoke-KeycloakApi -Endpoint "/roles" -Method POST -Body $roleConfig | Out-Null
        Write-Log "Role $RoleName created successfully"
    }
}

function New-KeycloakClientScope {
    param(
        [string]$ScopeName,
        [string]$Description,
        [string]$Protocol = "openid-connect"
    )

    Write-Log "Creating client scope: $ScopeName"

    $scopeConfig = @{
        name        = $ScopeName
        description = $Description
        protocol    = $Protocol
        attributes  = @{
            "include.in.token.scope" = "true"
            "display.on.consent.screen" = "true"
        }
    }

    try {
        $existingScopes = Invoke-KeycloakApi -Endpoint "/client-scopes"
        $existing = $existingScopes | Where-Object { $_.name -eq $ScopeName }
        if ($existing) {
            Write-Log "Client scope $ScopeName already exists"
            return $existing.id
        }
    }
    catch {
        # Scope doesn't exist, create it
    }

    Invoke-KeycloakApi -Endpoint "/client-scopes" -Method POST -Body $scopeConfig | Out-Null
    Write-Log "Client scope $ScopeName created successfully"
    
    # Retrieve and return scope ID
    $scopes = Invoke-KeycloakApi -Endpoint "/client-scopes"
    $scope = $scopes | Where-Object { $_.name -eq $ScopeName }
    return $scope.id
}

# Main execution
try {
    Write-Log "=== Starting Keycloak Realm Setup for IntelliFin IAM ==="
    Write-Log "Keycloak URL: $KeycloakUrl"
    Write-Log "Realm: $RealmName"

    # Authenticate
    Initialize-KeycloakAuth

    # Create/update realm
    New-KeycloakRealm

    # Create realm roles
    Write-Log "=== Creating Realm Roles ==="
    $realmRoles = @(
        @{ Name = "system-admin"; Description = "System Administrator with full access" }
        @{ Name = "tenant-admin"; Description = "Tenant Administrator" }
        @{ Name = "branch-manager"; Description = "Branch Manager" }
        @{ Name = "loan-officer"; Description = "Loan Officer" }
        @{ Name = "credit-analyst"; Description = "Credit Analyst" }
        @{ Name = "collections-agent"; Description = "Collections Agent" }
        @{ Name = "accountant"; Description = "Accountant" }
        @{ Name = "auditor"; Description = "Auditor (read-only)" }
        @{ Name = "service-account"; Description = "Service account for inter-service communication" }
    )

    foreach ($role in $realmRoles) {
        New-KeycloakRealmRole -RoleName $role.Name -Description $role.Description
    }

    # Create client scopes
    Write-Log "=== Creating Client Scopes ==="
    $intellifinApiScope = New-KeycloakClientScope `
        -ScopeName "intellifin-api" `
        -Description "Access to IntelliFin API endpoints"

    $tenantScope = New-KeycloakClientScope `
        -ScopeName "tenant" `
        -Description "Tenant context information"

    # Create clients
    Write-Log "=== Creating Clients ==="
    
    # Identity Service client (confidential)
    $identitySecret = New-KeycloakClient `
        -ClientId "intellifin-identity-service" `
        -Name "IntelliFin Identity Service" `
        -Description "Backend identity service for user management" `
        -RedirectUris @("https://identity.intellifin.local/*", "http://localhost:5001/*") `
        -WebOrigins @("https://identity.intellifin.local", "http://localhost:5001") `
        -PublicClient $false `
        -ServiceAccountsEnabled $true `
        -DirectAccessGrantsEnabled $true `
        -DefaultScopes @("openid", "profile", "email", "intellifin-api", "tenant")

    # API Gateway client (confidential)
    $gatewaySecret = New-KeycloakClient `
        -ClientId "intellifin-api-gateway" `
        -Name "IntelliFin API Gateway" `
        -Description "API Gateway for request routing and authentication" `
        -RedirectUris @("https://api.intellifin.local/*", "http://localhost:5000/*") `
        -WebOrigins @("https://api.intellifin.local", "http://localhost:5000") `
        -PublicClient $false `
        -ServiceAccountsEnabled $true `
        -DirectAccessGrantsEnabled $false `
        -DefaultScopes @("openid", "profile", "email", "intellifin-api")

    # Frontend Web Application (public)
    New-KeycloakClient `
        -ClientId "intellifin-web-app" `
        -Name "IntelliFin Web Application" `
        -Description "Frontend web application for end users" `
        -RedirectUris @("https://app.intellifin.local/*", "http://localhost:3000/*") `
        -WebOrigins @("https://app.intellifin.local", "http://localhost:3000") `
        -PublicClient $true `
        -ServiceAccountsEnabled $false `
        -DirectAccessGrantsEnabled $false `
        -DefaultScopes @("openid", "profile", "email", "intellifin-api", "tenant")

    # Store secrets in Vault if available
    if ($VaultAddr -and $identitySecret) {
        Write-Log "Storing client secrets in Vault..."
        
        $identitySecretData = @{
            data = @{
                client_id     = "intellifin-identity-service"
                client_secret = $identitySecret
            }
        }

        $gatewaySecretData = @{
            data = @{
                client_id     = "intellifin-api-gateway"
                client_secret = $gatewaySecret
            }
        }

        try {
            Invoke-RestMethod `
                -Uri "$VaultAddr/v1/secret/data/keycloak/clients/identity-service" `
                -Method POST `
                -Headers @{ "X-Vault-Token" = $env:VAULT_TOKEN } `
                -Body ($identitySecretData | ConvertTo-Json) `
                -ContentType "application/json" | Out-Null

            Invoke-RestMethod `
                -Uri "$VaultAddr/v1/secret/data/keycloak/clients/api-gateway" `
                -Method POST `
                -Headers @{ "X-Vault-Token" = $env:VAULT_TOKEN } `
                -Body ($gatewaySecretData | ConvertTo-Json) `
                -ContentType "application/json" | Out-Null

            Write-Log "Client secrets stored in Vault successfully"
        }
        catch {
            Write-Log "Failed to store secrets in Vault: $_" -Level "WARNING"
            Write-Log "Identity Service Secret: $identitySecret" -Level "WARNING"
            Write-Log "API Gateway Secret: $gatewaySecret" -Level "WARNING"
        }
    }
    else {
        Write-Log "=== Client Secrets (store these securely) ===" -Level "WARNING"
        if ($identitySecret) {
            Write-Log "Identity Service Secret: $identitySecret" -Level "WARNING"
        }
        if ($gatewaySecret) {
            Write-Log "API Gateway Secret: $gatewaySecret" -Level "WARNING"
        }
    }

    Write-Log "=== Keycloak Realm Setup Completed Successfully ===" -Level "INFO"
    Write-Log "Log file: $LogFile"

    return @{
        Success              = $true
        RealmName            = $RealmName
        IdentityClientId     = "intellifin-identity-service"
        IdentityClientSecret = $identitySecret
        GatewayClientId      = "intellifin-api-gateway"
        GatewayClientSecret  = $gatewaySecret
        LogFile              = $LogFile
    }
}
catch {
    Write-Log "=== Keycloak Realm Setup Failed ===" -Level "ERROR"
    Write-Log "Error: $_" -Level "ERROR"
    Write-Log "Stack Trace: $($_.ScriptStackTrace)" -Level "ERROR"
    throw
}
