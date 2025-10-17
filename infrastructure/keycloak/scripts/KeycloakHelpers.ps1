<#
.SYNOPSIS
    Helper functions for Keycloak administration and configuration.

.DESCRIPTION
    Common utility functions used across Keycloak setup and management scripts.
#>

function Test-KeycloakConnection {
    <#
    .SYNOPSIS
        Tests connectivity to Keycloak server.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$KeycloakUrl,
        [switch]$SkipTls
    )

    try {
        $healthEndpoint = "$KeycloakUrl/health"
        if ($SkipTls) {
            $response = Invoke-WebRequest -Uri $healthEndpoint -Method Get -SkipCertificateCheck -ErrorAction SilentlyContinue
        } else {
            $response = Invoke-WebRequest -Uri $healthEndpoint -Method Get -ErrorAction SilentlyContinue
        }
        
        if ($response.StatusCode -eq 200) {
            return $true
        }
    }
    catch {
        # Try alternative endpoint
        try {
            $altEndpoint = "$KeycloakUrl/"
            if ($SkipTls) {
                $response = Invoke-WebRequest -Uri $altEndpoint -Method Get -SkipCertificateCheck
            } else {
                $response = Invoke-WebRequest -Uri $altEndpoint -Method Get
            }
            return $response.StatusCode -eq 200
        }
        catch {
            return $false
        }
    }
}

function ConvertTo-Base64Url {
    <#
    .SYNOPSIS
        Converts a string to Base64URL encoding (used in JWT tokens).
    #>
    param([string]$InputString)
    
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($InputString)
    $base64 = [Convert]::ToBase64String($bytes)
    $base64Url = $base64.Replace('+', '-').Replace('/', '_').TrimEnd('=')
    return $base64Url
}

function ConvertFrom-Base64Url {
    <#
    .SYNOPSIS
        Decodes a Base64URL encoded string.
    #>
    param([string]$InputString)
    
    $base64 = $InputString.Replace('-', '+').Replace('_', '/')
    $padding = 4 - ($base64.Length % 4)
    if ($padding -ne 4) {
        $base64 += '=' * $padding
    }
    
    $bytes = [Convert]::FromBase64String($base64)
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

function Get-JwtPayload {
    <#
    .SYNOPSIS
        Extracts and decodes the payload from a JWT token.
    #>
    param([string]$Token)
    
    $parts = $Token.Split('.')
    if ($parts.Count -ne 3) {
        throw "Invalid JWT token format"
    }
    
    $payload = ConvertFrom-Base64Url -InputString $parts[1]
    return $payload | ConvertFrom-Json
}

function Test-JwtExpiration {
    <#
    .SYNOPSIS
        Checks if a JWT token is expired.
    #>
    param([string]$Token)
    
    try {
        $payload = Get-JwtPayload -Token $Token
        $expirationTime = [DateTimeOffset]::FromUnixTimeSeconds($payload.exp).UtcDateTime
        $now = [DateTime]::UtcNow
        
        return $now -gt $expirationTime
    }
    catch {
        return $true
    }
}

function Format-JsonOutput {
    <#
    .SYNOPSIS
        Formats JSON for pretty printing.
    #>
    param(
        [Parameter(ValueFromPipeline = $true)]
        [object]$InputObject,
        [int]$Depth = 5
    )
    
    return $InputObject | ConvertTo-Json -Depth $Depth
}

function Write-ColorOutput {
    <#
    .SYNOPSIS
        Writes colored output to the console.
    #>
    param(
        [string]$Message,
        [ValidateSet("Info", "Success", "Warning", "Error")]
        [string]$Type = "Info"
    )
    
    $color = switch ($Type) {
        "Info"    { "Cyan" }
        "Success" { "Green" }
        "Warning" { "Yellow" }
        "Error"   { "Red" }
    }
    
    Write-Host $Message -ForegroundColor $color
}

function Invoke-WithRetry {
    <#
    .SYNOPSIS
        Invokes a script block with retry logic.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [ScriptBlock]$ScriptBlock,
        [int]$MaxRetries = 3,
        [int]$DelaySeconds = 2,
        [string]$OperationName = "Operation"
    )
    
    $attempt = 0
    $success = $false
    $lastError = $null
    
    while (-not $success -and $attempt -lt $MaxRetries) {
        $attempt++
        try {
            $result = & $ScriptBlock
            $success = $true
            return $result
        }
        catch {
            $lastError = $_
            if ($attempt -lt $MaxRetries) {
                Write-Warning "$OperationName failed (attempt $attempt/$MaxRetries). Retrying in $DelaySeconds seconds..."
                Start-Sleep -Seconds $DelaySeconds
                $DelaySeconds *= 2  # Exponential backoff
            }
        }
    }
    
    if (-not $success) {
        throw "Operation '$OperationName' failed after $MaxRetries attempts. Last error: $lastError"
    }
}

function Export-KeycloakConfiguration {
    <#
    .SYNOPSIS
        Exports Keycloak realm configuration to JSON file.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$KeycloakUrl,
        [Parameter(Mandatory = $true)]
        [string]$AccessToken,
        [Parameter(Mandatory = $true)]
        [string]$RealmName,
        [Parameter(Mandatory = $true)]
        [string]$OutputPath,
        [switch]$SkipTls
    )
    
    $headers = @{
        "Authorization" = "Bearer $AccessToken"
        "Content-Type"  = "application/json"
    }
    
    $uri = "$KeycloakUrl/admin/realms/$RealmName"
    
    try {
        if ($SkipTls) {
            $realmConfig = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get -SkipCertificateCheck
        } else {
            $realmConfig = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
        }
        
        $realmConfig | ConvertTo-Json -Depth 20 | Set-Content -Path $OutputPath
        Write-ColorOutput "Realm configuration exported to: $OutputPath" -Type Success
        return $true
    }
    catch {
        Write-ColorOutput "Failed to export realm configuration: $_" -Type Error
        return $false
    }
}

function Get-KeycloakClients {
    <#
    .SYNOPSIS
        Retrieves all clients in a realm.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$KeycloakUrl,
        [Parameter(Mandatory = $true)]
        [string]$AccessToken,
        [Parameter(Mandatory = $true)]
        [string]$RealmName,
        [switch]$SkipTls
    )
    
    $headers = @{
        "Authorization" = "Bearer $AccessToken"
    }
    
    $uri = "$KeycloakUrl/admin/realms/$RealmName/clients"
    
    try {
        if ($SkipTls) {
            return Invoke-RestMethod -Uri $uri -Headers $headers -Method Get -SkipCertificateCheck
        } else {
            return Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
        }
    }
    catch {
        Write-Warning "Failed to retrieve clients: $_"
        return @()
    }
}

function Get-KeycloakRealmRoles {
    <#
    .SYNOPSIS
        Retrieves all realm roles.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$KeycloakUrl,
        [Parameter(Mandatory = $true)]
        [string]$AccessToken,
        [Parameter(Mandatory = $true)]
        [string]$RealmName,
        [switch]$SkipTls
    )
    
    $headers = @{
        "Authorization" = "Bearer $AccessToken"
    }
    
    $uri = "$KeycloakUrl/admin/realms/$RealmName/roles"
    
    try {
        if ($SkipTls) {
            return Invoke-RestMethod -Uri $uri -Headers $headers -Method Get -SkipCertificateCheck
        } else {
            return Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
        }
    }
    catch {
        Write-Warning "Failed to retrieve realm roles: $_"
        return @()
    }
}

function Test-KeycloakRealmExists {
    <#
    .SYNOPSIS
        Checks if a realm exists.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$KeycloakUrl,
        [Parameter(Mandatory = $true)]
        [string]$AccessToken,
        [Parameter(Mandatory = $true)]
        [string]$RealmName,
        [switch]$SkipTls
    )
    
    $headers = @{
        "Authorization" = "Bearer $AccessToken"
    }
    
    $uri = "$KeycloakUrl/admin/realms/$RealmName"
    
    try {
        if ($SkipTls) {
            $response = Invoke-WebRequest -Uri $uri -Headers $headers -Method Get -SkipCertificateCheck -ErrorAction Stop
        } else {
            $response = Invoke-WebRequest -Uri $uri -Headers $headers -Method Get -ErrorAction Stop
        }
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

# Export functions
Export-ModuleMember -Function @(
    'Test-KeycloakConnection',
    'ConvertTo-Base64Url',
    'ConvertFrom-Base64Url',
    'Get-JwtPayload',
    'Test-JwtExpiration',
    'Format-JsonOutput',
    'Write-ColorOutput',
    'Invoke-WithRetry',
    'Export-KeycloakConfiguration',
    'Get-KeycloakClients',
    'Get-KeycloakRealmRoles',
    'Test-KeycloakRealmExists'
)
