<#
.SYNOPSIS
    Validates Keycloak realm setup and configuration for IntelliFin IAM.

.DESCRIPTION
    This script performs comprehensive validation of the Keycloak realm configuration:
    - Tests connectivity to Keycloak server
    - Verifies realm existence and configuration
    - Validates client registrations and secrets
    - Checks realm roles and client scopes
    - Tests OIDC endpoints and discovery
    - Performs token introspection tests

.PARAMETER KeycloakUrl
    Base URL of the Keycloak server

.PARAMETER RealmName
    Name of the realm to test (default: intellifin)

.PARAMETER SkipTls
    Skip TLS verification for development environments

.EXAMPLE
    .\Test-KeycloakSetup.ps1 -KeycloakUrl "https://keycloak.intellifin.local:8443" -RealmName "intellifin" -SkipTls

.NOTES
    Author: IntelliFin DevOps Team
    Version: 1.0.0
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$KeycloakUrl,

    [Parameter(Mandatory = $false)]
    [string]$RealmName = "intellifin",

    [Parameter(Mandatory = $false)]
    [switch]$SkipTls,

    [Parameter(Mandatory = $false)]
    [string]$AdminUsername = "admin",

    [Parameter(Mandatory = $false)]
    [string]$AdminPassword
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

# Import helpers
. "$PSScriptRoot\KeycloakHelpers.ps1"

$script:TestResults = @()
$script:TestsPassed = 0
$script:TestsFailed = 0

function Write-TestResult {
    param(
        [string]$TestName,
        [bool]$Passed,
        [string]$Message = "",
        [object]$Details = $null
    )

    $result = @{
        TestName  = $TestName
        Passed    = $Passed
        Message   = $Message
        Details   = $Details
        Timestamp = Get-Date
    }

    $script:TestResults += $result

    if ($Passed) {
        $script:TestsPassed++
        Write-ColorOutput "✓ $TestName" -Type Success
    }
    else {
        $script:TestsFailed++
        Write-ColorOutput "✗ $TestName - $Message" -Type Error
    }

    if ($Details) {
        Write-Host "  Details: $($Details | ConvertTo-Json -Compress)" -ForegroundColor Gray
    }
}

function Test-KeycloakConnectivity {
    Write-Host "`n=== Testing Keycloak Connectivity ===" -ForegroundColor Cyan

    try {
        $connected = Test-KeycloakConnection -KeycloakUrl $KeycloakUrl -SkipTls:$SkipTls
        Write-TestResult -TestName "Keycloak Server Connectivity" -Passed $connected `
            -Message $(if ($connected) { "Server is reachable" } else { "Server is not reachable" })
        return $connected
    }
    catch {
        Write-TestResult -TestName "Keycloak Server Connectivity" -Passed $false `
            -Message "Connection test failed: $_"
        return $false
    }
}

function Get-AdminToken {
    Write-Host "`n=== Authenticating with Keycloak ===" -ForegroundColor Cyan

    if (-not $AdminPassword) {
        $securePassword = Read-Host "Enter Keycloak admin password" -AsSecureString
        $script:AdminPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
        )
    }

    $tokenEndpoint = "$KeycloakUrl/realms/master/protocol/openid-connect/token"
    $body = @{
        username   = $AdminUsername
        password   = $AdminPassword
        grant_type = "password"
        client_id  = "admin-cli"
    }

    try {
        if ($SkipTls) {
            $response = Invoke-RestMethod -Uri $tokenEndpoint -Method Post -Body $body -SkipCertificateCheck
        }
        else {
            $response = Invoke-RestMethod -Uri $tokenEndpoint -Method Post -Body $body
        }

        Write-TestResult -TestName "Admin Authentication" -Passed $true -Message "Successfully obtained admin token"
        return $response.access_token
    }
    catch {
        Write-TestResult -TestName "Admin Authentication" -Passed $false -Message "Failed to authenticate: $_"
        return $null
    }
}

function Test-RealmExists {
    param([string]$AccessToken)

    Write-Host "`n=== Testing Realm Configuration ===" -ForegroundColor Cyan

    $headers = @{
        "Authorization" = "Bearer $AccessToken"
    }

    $uri = "$KeycloakUrl/admin/realms/$RealmName"

    try {
        if ($SkipTls) {
            $realm = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get -SkipCertificateCheck
        }
        else {
            $realm = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
        }

        Write-TestResult -TestName "Realm Exists" -Passed $true -Message "Realm '$RealmName' found" -Details $realm.displayName

        # Test realm configuration
        $configTests = @{
            "Realm Enabled"            = $realm.enabled -eq $true
            "Email Login Allowed"      = $realm.loginWithEmailAllowed -eq $true
            "Brute Force Protected"    = $realm.bruteForceProtected -eq $true
            "Email Verification"       = $realm.verifyEmail -eq $true
            "Remember Me Enabled"      = $realm.rememberMe -eq $true
            "Password Reset Allowed"   = $realm.resetPasswordAllowed -eq $true
        }

        foreach ($test in $configTests.GetEnumerator()) {
            Write-TestResult -TestName "Realm Config: $($test.Key)" -Passed $test.Value `
                -Message $(if ($test.Value) { "Configured correctly" } else { "Not configured as expected" })
        }

        return $realm
    }
    catch {
        Write-TestResult -TestName "Realm Exists" -Passed $false -Message "Realm not found or inaccessible: $_"
        return $null
    }
}

function Test-ClientsConfiguration {
    param([string]$AccessToken)

    Write-Host "`n=== Testing Client Configuration ===" -ForegroundColor Cyan

    $expectedClients = @(
        "intellifin-identity-service",
        "intellifin-api-gateway",
        "intellifin-web-app"
    )

    try {
        $clients = Get-KeycloakClients -KeycloakUrl $KeycloakUrl -AccessToken $AccessToken `
            -RealmName $RealmName -SkipTls:$SkipTls

        foreach ($expectedClientId in $expectedClients) {
            $client = $clients | Where-Object { $_.clientId -eq $expectedClientId }

            if ($client) {
                Write-TestResult -TestName "Client Exists: $expectedClientId" -Passed $true

                # Test client configuration
                $isPublic = $client.publicClient
                $expectedPublic = ($expectedClientId -eq "intellifin-web-app")

                Write-TestResult -TestName "Client Type: $expectedClientId" `
                    -Passed ($isPublic -eq $expectedPublic) `
                    -Message $(if ($isPublic -eq $expectedPublic) { 
                        if ($expectedPublic) { "Public client" } else { "Confidential client" } 
                    } else { "Client type mismatch" })

                Write-TestResult -TestName "Client Enabled: $expectedClientId" `
                    -Passed $client.enabled `
                    -Message $(if ($client.enabled) { "Enabled" } else { "Disabled" })

                Write-TestResult -TestName "OIDC Protocol: $expectedClientId" `
                    -Passed ($client.protocol -eq "openid-connect") `
                    -Message "Protocol: $($client.protocol)"
            }
            else {
                Write-TestResult -TestName "Client Exists: $expectedClientId" -Passed $false `
                    -Message "Client not found"
            }
        }

        return $clients
    }
    catch {
        Write-TestResult -TestName "Client Configuration Test" -Passed $false `
            -Message "Failed to retrieve clients: $_"
        return @()
    }
}

function Test-RealmRoles {
    param([string]$AccessToken)

    Write-Host "`n=== Testing Realm Roles ===" -ForegroundColor Cyan

    $expectedRoles = @(
        "system-admin",
        "tenant-admin",
        "branch-manager",
        "loan-officer",
        "credit-analyst",
        "collections-agent",
        "accountant",
        "auditor",
        "service-account"
    )

    try {
        $roles = Get-KeycloakRealmRoles -KeycloakUrl $KeycloakUrl -AccessToken $AccessToken `
            -RealmName $RealmName -SkipTls:$SkipTls

        $roleNames = $roles | Select-Object -ExpandProperty name

        foreach ($expectedRole in $expectedRoles) {
            $exists = $roleNames -contains $expectedRole
            Write-TestResult -TestName "Realm Role: $expectedRole" -Passed $exists `
                -Message $(if ($exists) { "Role configured" } else { "Role missing" })
        }

        return $roles
    }
    catch {
        Write-TestResult -TestName "Realm Roles Test" -Passed $false `
            -Message "Failed to retrieve roles: $_"
        return @()
    }
}

function Test-ClientScopes {
    param([string]$AccessToken)

    Write-Host "`n=== Testing Client Scopes ===" -ForegroundColor Cyan

    $headers = @{
        "Authorization" = "Bearer $AccessToken"
    }

    $uri = "$KeycloakUrl/admin/realms/$RealmName/client-scopes"

    try {
        if ($SkipTls) {
            $scopes = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get -SkipCertificateCheck
        }
        else {
            $scopes = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
        }

        $expectedScopes = @("intellifin-api", "tenant")
        $scopeNames = $scopes | Select-Object -ExpandProperty name

        foreach ($expectedScope in $expectedScopes) {
            $exists = $scopeNames -contains $expectedScope
            Write-TestResult -TestName "Client Scope: $expectedScope" -Passed $exists `
                -Message $(if ($exists) { "Scope configured" } else { "Scope missing" })
        }

        return $scopes
    }
    catch {
        Write-TestResult -TestName "Client Scopes Test" -Passed $false `
            -Message "Failed to retrieve client scopes: $_"
        return @()
    }
}

function Test-OidcDiscovery {
    Write-Host "`n=== Testing OIDC Discovery Endpoint ===" -ForegroundColor Cyan

    $discoveryUrl = "$KeycloakUrl/realms/$RealmName/.well-known/openid-configuration"

    try {
        if ($SkipTls) {
            $discovery = Invoke-RestMethod -Uri $discoveryUrl -Method Get -SkipCertificateCheck
        }
        else {
            $discovery = Invoke-RestMethod -Uri $discoveryUrl -Method Get
        }

        Write-TestResult -TestName "OIDC Discovery Endpoint" -Passed $true `
            -Message "Discovery document retrieved successfully"

        # Validate critical endpoints
        $endpointTests = @{
            "Issuer"              = $discovery.issuer
            "Authorization Endpoint" = $discovery.authorization_endpoint
            "Token Endpoint"      = $discovery.token_endpoint
            "UserInfo Endpoint"   = $discovery.userinfo_endpoint
            "JWKS URI"            = $discovery.jwks_uri
            "Introspection Endpoint" = $discovery.introspection_endpoint
        }

        foreach ($endpoint in $endpointTests.GetEnumerator()) {
            $exists = -not [string]::IsNullOrWhiteSpace($endpoint.Value)
            Write-TestResult -TestName "OIDC Endpoint: $($endpoint.Key)" -Passed $exists `
                -Message $(if ($exists) { $endpoint.Value } else { "Endpoint not configured" })
        }

        # Test supported grant types
        $requiredGrants = @("authorization_code", "refresh_token", "client_credentials", "password")
        foreach ($grant in $requiredGrants) {
            $supported = $discovery.grant_types_supported -contains $grant
            Write-TestResult -TestName "Grant Type Supported: $grant" -Passed $supported `
                -Message $(if ($supported) { "Supported" } else { "Not supported" })
        }

        return $discovery
    }
    catch {
        Write-TestResult -TestName "OIDC Discovery Endpoint" -Passed $false `
            -Message "Failed to retrieve discovery document: $_"
        return $null
    }
}

function Test-JwksEndpoint {
    Write-Host "`n=== Testing JWKS Endpoint ===" -ForegroundColor Cyan

    $jwksUrl = "$KeycloakUrl/realms/$RealmName/protocol/openid-connect/certs"

    try {
        if ($SkipTls) {
            $jwks = Invoke-RestMethod -Uri $jwksUrl -Method Get -SkipCertificateCheck
        }
        else {
            $jwks = Invoke-RestMethod -Uri $jwksUrl -Method Get
        }

        $keyCount = $jwks.keys.Count
        $passed = $keyCount -gt 0

        Write-TestResult -TestName "JWKS Endpoint" -Passed $passed `
            -Message "Found $keyCount signing key(s)" `
            -Details $jwks.keys[0].kid

        return $jwks
    }
    catch {
        Write-TestResult -TestName "JWKS Endpoint" -Passed $false `
            -Message "Failed to retrieve JWKS: $_"
        return $null
    }
}

# Main execution
try {
    Write-Host "`n=============================================" -ForegroundColor Yellow
    Write-Host "Keycloak Setup Validation for IntelliFin IAM" -ForegroundColor Yellow
    Write-Host "=============================================" -ForegroundColor Yellow
    Write-Host "Keycloak URL: $KeycloakUrl"
    Write-Host "Realm: $RealmName"
    Write-Host "=============================================" -ForegroundColor Yellow

    # Test connectivity
    $connected = Test-KeycloakConnectivity
    if (-not $connected) {
        Write-Host "`nCannot proceed without connectivity. Exiting..." -ForegroundColor Red
        exit 1
    }

    # Authenticate
    $accessToken = Get-AdminToken
    if (-not $accessToken) {
        Write-Host "`nAuthentication failed. Cannot proceed with tests." -ForegroundColor Red
        exit 1
    }

    # Run tests
    $realm = Test-RealmExists -AccessToken $accessToken
    if ($realm) {
        Test-ClientsConfiguration -AccessToken $accessToken
        Test-RealmRoles -AccessToken $accessToken
        Test-ClientScopes -AccessToken $accessToken
    }

    Test-OidcDiscovery
    Test-JwksEndpoint

    # Summary
    Write-Host "`n=============================================" -ForegroundColor Yellow
    Write-Host "Test Summary" -ForegroundColor Yellow
    Write-Host "=============================================" -ForegroundColor Yellow
    Write-Host "Total Tests: $($script:TestsPassed + $script:TestsFailed)" -ForegroundColor White
    Write-ColorOutput "Passed: $script:TestsPassed" -Type Success
    Write-ColorOutput "Failed: $script:TestsFailed" -Type $(if ($script:TestsFailed -eq 0) { "Success" } else { "Error" })

    $passRate = [math]::Round(($script:TestsPassed / ($script:TestsPassed + $script:TestsFailed)) * 100, 2)
    Write-Host "Pass Rate: $passRate%" -ForegroundColor $(if ($passRate -ge 90) { "Green" } elseif ($passRate -ge 70) { "Yellow" } else { "Red" })

    # Export results
    $reportPath = Join-Path $PSScriptRoot "..\logs\keycloak-test-results-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    $script:TestResults | ConvertTo-Json -Depth 5 | Set-Content -Path $reportPath
    Write-Host "`nDetailed test results saved to: $reportPath" -ForegroundColor Cyan

    if ($script:TestsFailed -eq 0) {
        Write-Host "`n✓ All tests passed! Keycloak setup is valid." -ForegroundColor Green
        exit 0
    }
    else {
        Write-Host "`n⚠ Some tests failed. Please review the results above." -ForegroundColor Yellow
        exit 1
    }
}
catch {
    Write-Host "`n✗ Test execution failed: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}
