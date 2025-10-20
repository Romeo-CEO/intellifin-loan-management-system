param(
    [string]$KeycloakUrl = "https://keycloak.dev.intellifin.local",
    [string]$AdminUsername = "admin",
    [string]$AdminPassword,
    [string]$Realm = "IntelliFin"
)

Write-Host "Configuring SMTP settings for Keycloak realm: $Realm" -ForegroundColor Green

# Authenticate with Keycloak Admin API
Write-Host "Authenticating with Keycloak..." -ForegroundColor Cyan
$authResponse = Invoke-RestMethod `
    -Uri "$KeycloakUrl/realms/master/protocol/openid-connect/token" `
    -Method Post `
    -Body @{
        username = $AdminUsername
        password = $AdminPassword
        grant_type = "password"
        client_id = "admin-cli"
    }

$token = $authResponse.access_token
Write-Host "Authentication successful" -ForegroundColor Green

# Get current realm configuration
Write-Host "Retrieving realm configuration..." -ForegroundColor Cyan
$realmConfig = Invoke-RestMethod `
    -Uri "$KeycloakUrl/admin/realms/$Realm" `
    -Method Get `
    -Headers @{ Authorization = "Bearer $token" }

# Update SMTP configuration
Write-Host "Updating SMTP configuration..." -ForegroundColor Cyan
$realmConfig.smtpServer = @{
    host = "smtp.intellifin.local"
    port = "587"
    from = "noreply@intellifin.local"
    fromDisplayName = "IntelliFin Loan Management"
    replyTo = "support@intellifin.local"
    starttls = "true"
    auth = "true"
    user = $env:SMTP_USERNAME
    password = $env:SMTP_PASSWORD
}

# Update realm
Invoke-RestMethod `
    -Uri "$KeycloakUrl/admin/realms/$Realm" `
    -Method Put `
    -Headers @{ 
        Authorization = "Bearer $token"
        "Content-Type" = "application/json"
    } `
    -Body ($realmConfig | ConvertTo-Json -Depth 10)

Write-Host "SMTP configuration updated successfully for realm $Realm" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration Details:" -ForegroundColor Yellow
Write-Host "  SMTP Host: smtp.intellifin.local:587" -ForegroundColor White
Write-Host "  From: noreply@intellifin.local" -ForegroundColor White
Write-Host "  Display Name: IntelliFin Loan Management" -ForegroundColor White
Write-Host "  STARTTLS: Enabled" -ForegroundColor White
Write-Host "  Auth: Enabled" -ForegroundColor White
