param(
    [string]$ThemeDir = "/opt/keycloak/themes/intellifin-theme/email",
    [string]$SourceDir = "../../templates/keycloak/email",
    [string]$Namespace = "identity",
    [string]$DeploymentName = "keycloak"
)

Write-Host "Deploying IntelliFin email themes to Keycloak" -ForegroundColor Green
Write-Host "Source: $SourceDir" -ForegroundColor Cyan
Write-Host "Target: $ThemeDir" -ForegroundColor Cyan

# Ensure source directory exists
if (-not (Test-Path $SourceDir)) {
    Write-Error "Source directory not found: $SourceDir"
    exit 1
}

# Note: In a production environment, you would copy files to the Keycloak pod
# For local development, you might mount the theme directory as a volume

Write-Host "`nCreating theme structure..." -ForegroundColor Cyan

# Create theme directories (example for local deployment)
$localThemeDir = "$PSScriptRoot/../../deployment/keycloak/themes/intellifin-theme/email"
New-Item -ItemType Directory -Force -Path "$localThemeDir/html" | Out-Null
New-Item -ItemType Directory -Force -Path "$localThemeDir/text" | Out-Null

# Copy templates
Write-Host "Copying HTML templates..." -ForegroundColor Cyan
Copy-Item "$SourceDir/html/*.ftl" -Destination "$localThemeDir/html/" -Force

Write-Host "Copying theme properties..." -ForegroundColor Cyan
Copy-Item "$SourceDir/theme.properties" -Destination "$localThemeDir/" -Force

Write-Host "`nEmail templates deployed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. For Kubernetes deployment:" -ForegroundColor White
Write-Host "     kubectl cp $localThemeDir <pod-name>:$ThemeDir -n $Namespace" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. Restart Keycloak to load new theme:" -ForegroundColor White
Write-Host "     kubectl rollout restart statefulset $DeploymentName -n $Namespace" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Verify theme is available in Keycloak Admin Console:" -ForegroundColor White
Write-Host "     Realm Settings > Themes > Email Theme = intellifin-theme" -ForegroundColor Gray
Write-Host ""
Write-Host "Templates created:" -ForegroundColor Yellow
Write-Host "  - password-reset.ftl" -ForegroundColor White
Write-Host "  - email-verification.ftl" -ForegroundColor White
Write-Host "  - theme.properties" -ForegroundColor White
