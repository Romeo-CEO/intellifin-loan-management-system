# Story 6.1: Migration Orchestration and Cutover

## Story Information

**Epic:** Migration & Deployment (Epic 6)  
**Story ID:** 6.1  
**Story Name:** Migration Orchestration and Cutover  
**Priority:** Critical  
**Estimated Effort:** 8 story points (16-24 hours)  
**Dependencies:** Stories 1.1-1.6, 2.1-2.2, 3.1-3.2, 4.1-4.2 (all prior IAM stories)  
**Blocks:** Production deployment, complete IAM feature activation

---

## Story Description

As a **DevOps Engineer**, I want **automated migration scripts and orchestration tools** so that **the transition from custom JWT to Keycloak OIDC is executed safely with zero downtime and comprehensive rollback capabilities**.

### Business Value

- Enables zero-downtime migration to industry-standard OIDC authentication
- Reduces human error through automated orchestration
- Provides confidence through comprehensive health checks and rollback procedures
- Ensures business continuity during critical infrastructure transition
- Establishes repeatable deployment pattern for production and disaster recovery

### User Story

```
Given a production system running custom JWT authentication
When the migration orchestration script is executed
Then the system should transition to Keycloak OIDC in phases
And existing user sessions should remain valid
And authentication should work throughout the migration
And rollback should be possible at any phase
And all metrics should show normal operation
```

---

## Acceptance Criteria

### Functional Criteria

- [ ] **AC1:** Migration orchestration script with 7 phases:
  1. Pre-migration validation
  2. User provisioning to Keycloak (bulk sync)
  3. Dual-token validation activation in API Gateway
  4. Keycloak as primary for new logins
  5. Session migration (active users)
  6. Legacy token deprecation warning period (30 days)
  7. Legacy endpoint deactivation

- [ ] **AC2:** Health checks for each migration phase:
  - Database connectivity and schema validation
  - Keycloak API accessibility and response times
  - User provisioning success rate
  - Token validation performance (both types)
  - Session migration completion percentage
  - API Gateway routing accuracy

- [ ] **AC3:** Rollback procedures documented and tested for each phase with maximum rollback time of 5 minutes

- [ ] **AC4:** Migration monitoring dashboard deployed showing:
  - Current migration phase
  - Success/failure metrics
  - Active sessions by token type
  - Authentication success rate
  - Performance metrics (p95 latency)
  - Error rates and types

- [ ] **AC5:** Communication plan template with stakeholder notifications for each phase

### Non-Functional Criteria

- [ ] **AC6:** Migration script completes Phases 1-4 in <2 hours for production database (100K+ users)

- [ ] **AC7:** Zero authentication failures during migration (100% success rate maintained)

- [ ] **AC8:** Performance degradation <5% during migration phases

- [ ] **AC9:** Migration pausable and resumable at phase boundaries

- [ ] **AC10:** All migration actions logged with audit trail for compliance

---

## Technical Specification

### Migration Phase Architecture

```
Phase 0: Pre-Migration Validation (5 min)
├─ Database schema verification
├─ Keycloak connectivity test
├─ Backup verification
├─ Performance baseline capture
└─ Stakeholder notification

Phase 1: User Provisioning (30-60 min)
├─ Bulk user sync to Keycloak
├─ Role and permission mapping
├─ Custom claim provisioning (branch, tenant)
├─ Verification sampling (10% random check)
└─ Rollback point: Keycloak users can be deleted

Phase 2: Dual-Token Activation (5 min)
├─ API Gateway config update
├─ Keycloak JWT validation enabled
├─ Custom JWT validation retained
├─ Traffic monitoring enabled
└─ Rollback point: Revert Gateway config

Phase 3: Keycloak Primary (Zero Downtime Cutover)
├─ New logins redirect to Keycloak
├─ Existing custom JWT sessions valid
├─ Gradual user transition tracking
├─ Performance monitoring
└─ Rollback point: Redirect to legacy login

Phase 4: Session Migration (Progressive, 7 days)
├─ Active user prompt for re-authentication
├─ Session bridging for seamless UX
├─ Custom JWT expiry enforcement
└─ Rollback point: Extend JWT expiry

Phase 5: Legacy Deprecation Warning (30 days)
├─ Warning banner for custom JWT users
├─ Email notifications
├─ Usage tracking
└─ No rollback (warning only)

Phase 6: Legacy Endpoint Deactivation (Final)
├─ Custom JWT endpoints return 410 Gone
├─ Keycloak-only authentication
├─ Legacy code removal
└─ No rollback (requires full re-migration)
```

### Migration Orchestration Script

**Location:** `scripts/migration/iam-migration-orchestrator.ps1`

```powershell
<#
.SYNOPSIS
    IAM Migration Orchestrator - Custom JWT to Keycloak OIDC
.DESCRIPTION
    Orchestrates the phased migration from custom JWT to Keycloak OIDC authentication
    with health checks, rollback capabilities, and comprehensive logging.
.PARAMETER Phase
    Specific phase to execute (0-6), or "all" for complete migration
.PARAMETER Environment
    Target environment (dev, staging, production)
.PARAMETER DryRun
    Executes validation without applying changes
#>

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("0", "1", "2", "3", "4", "5", "6", "all")]
    [string]$Phase,
    
    [Parameter(Mandatory=$true)]
    [ValidateSet("dev", "staging", "production")]
    [string]$Environment,
    
    [switch]$DryRun
)

# Configuration
$config = @{
    IdentityServiceUrl = "https://identity.$Environment.intellifin.local"
    KeycloakUrl = "https://keycloak.$Environment.intellifin.local"
    ApiGatewayUrl = "https://api.$Environment.intellifin.local"
    MonitoringDashboardUrl = "https://grafana.$Environment.intellifin.local/d/iam-migration"
    
    HealthCheckTimeout = 30 # seconds
    RollbackTimeout = 300 # seconds (5 minutes)
    
    NotificationWebhook = $env:TEAMS_WEBHOOK_URL
}

# Logging
$logFile = "iam-migration-$Environment-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Level] $Message"
    Write-Host $logEntry
    Add-Content -Path $logFile -Value $logEntry
}

# Health check functions
function Test-DatabaseHealth {
    Write-Log "Checking database connectivity..."
    $response = Invoke-RestMethod -Uri "$($config.IdentityServiceUrl)/health/db" -Method Get
    return $response.status -eq "healthy"
}

function Test-KeycloakHealth {
    Write-Log "Checking Keycloak availability..."
    try {
        $response = Invoke-RestMethod -Uri "$($config.KeycloakUrl)/health/ready" -Method Get -TimeoutSec 5
        return $response.status -eq "UP"
    }
    catch {
        Write-Log "Keycloak health check failed: $_" -Level "ERROR"
        return $false
    }
}

function Test-ApiGatewayHealth {
    Write-Log "Checking API Gateway health..."
    $response = Invoke-RestMethod -Uri "$($config.ApiGatewayUrl)/health" -Method Get
    return $response.status -eq "healthy"
}

# Phase 0: Pre-Migration Validation
function Invoke-Phase0-Validation {
    Write-Log "=== PHASE 0: Pre-Migration Validation ===" -Level "INFO"
    
    # Health checks
    $dbHealthy = Test-DatabaseHealth
    $keycloakHealthy = Test-KeycloakHealth
    $gatewayHealthy = Test-ApiGatewayHealth
    
    if (-not ($dbHealthy -and $keycloakHealthy -and $gatewayHealthy)) {
        Write-Log "Health checks failed. Aborting migration." -Level "ERROR"
        return $false
    }
    
    # Verify schema
    Write-Log "Verifying database schema..."
    $schemaCheck = Invoke-RestMethod -Uri "$($config.IdentityServiceUrl)/api/platform/migration/verify-schema" -Method Get
    if (-not $schemaCheck.valid) {
        Write-Log "Schema validation failed: $($schemaCheck.errors)" -Level "ERROR"
        return $false
    }
    
    # Capture baseline metrics
    Write-Log "Capturing baseline performance metrics..."
    $baseline = Invoke-RestMethod -Uri "$($config.IdentityServiceUrl)/api/platform/migration/baseline" -Method Post
    Write-Log "Baseline captured: Auth p95=$($baseline.authP95)ms, Success rate=$($baseline.successRate)%"
    
    # Backup verification
    Write-Log "Verifying backups..."
    # TODO: Integrate with backup system verification
    
    # Send notification
    Send-Notification -Phase 0 -Status "Completed" -Message "Pre-migration validation passed"
    
    return $true
}

# Phase 1: User Provisioning
function Invoke-Phase1-UserProvisioning {
    Write-Log "=== PHASE 1: User Provisioning to Keycloak ===" -Level "INFO"
    
    if ($DryRun) {
        Write-Log "DRY RUN: Would provision users to Keycloak" -Level "WARN"
        return $true
    }
    
    # Get user count
    $userCount = (Invoke-RestMethod -Uri "$($config.IdentityServiceUrl)/api/platform/migration/user-count" -Method Get).count
    Write-Log "Total users to provision: $userCount"
    
    # Bulk provisioning with progress tracking
    Write-Log "Starting bulk user provisioning..."
    $provisionResponse = Invoke-RestMethod `
        -Uri "$($config.IdentityServiceUrl)/api/platform/migration/provision-all" `
        -Method Post `
        -Body (@{ batchSize = 100; dryRun = $false } | ConvertTo-Json) `
        -ContentType "application/json"
    
    Write-Log "Provisioning completed: $($provisionResponse.provisioned) users, $($provisionResponse.failed) failures"
    
    if ($provisionResponse.failed -gt 0) {
        Write-Log "Some users failed to provision. Check logs." -Level "WARN"
    }
    
    # Verification sampling
    Write-Log "Verifying provisioned users (10% random sample)..."
    $verifyResponse = Invoke-RestMethod `
        -Uri "$($config.IdentityServiceUrl)/api/platform/migration/verify-provision" `
        -Method Post `
        -Body (@{ sampleSize = [math]::Max(10, $userCount * 0.1) } | ConvertTo-Json) `
        -ContentType "application/json"
    
    Write-Log "Verification: $($verifyResponse.matched)/$($verifyResponse.sampled) users match"
    
    if ($verifyResponse.matched / $verifyResponse.sampled -lt 0.95) {
        Write-Log "Verification failed. Less than 95% match rate." -Level "ERROR"
        return $false
    }
    
    Send-Notification -Phase 1 -Status "Completed" -Message "$($provisionResponse.provisioned) users provisioned"
    
    return $true
}

# Phase 2: Dual-Token Activation
function Invoke-Phase2-DualTokenActivation {
    Write-Log "=== PHASE 2: Dual-Token Validation Activation ===" -Level "INFO"
    
    if ($DryRun) {
        Write-Log "DRY RUN: Would activate dual-token validation" -Level "WARN"
        return $true
    }
    
    # Update API Gateway configuration
    Write-Log "Updating API Gateway configuration..."
    $gatewayConfig = @{
        enableKeycloakValidation = $true
        enableCustomJwtValidation = $true
        preferKeycloak = $false # Still using custom as primary
    }
    
    $response = Invoke-RestMethod `
        -Uri "$($config.ApiGatewayUrl)/api/config/auth" `
        -Method Put `
        -Body ($gatewayConfig | ConvertTo-Json) `
        -ContentType "application/json"
    
    Write-Log "API Gateway configuration updated"
    
    # Wait for config propagation
    Start-Sleep -Seconds 10
    
    # Test both token types
    Write-Log "Testing dual-token validation..."
    $customJwtTest = Test-CustomJwtValidation
    $keycloakJwtTest = Test-KeycloakJwtValidation
    
    if (-not ($customJwtTest -and $keycloakJwtTest)) {
        Write-Log "Dual-token validation test failed" -Level "ERROR"
        Invoke-Rollback -Phase 2
        return $false
    }
    
    Send-Notification -Phase 2 -Status "Completed" -Message "Dual-token validation active"
    
    return $true
}

# Phase 3: Keycloak Primary
function Invoke-Phase3-KeycloakPrimary {
    Write-Log "=== PHASE 3: Keycloak as Primary IdP ===" -Level "INFO"
    
    if ($DryRun) {
        Write-Log "DRY RUN: Would set Keycloak as primary" -Level "WARN"
        return $true
    }
    
    # Update Identity Service configuration
    Write-Log "Setting Keycloak as primary identity provider..."
    $identityConfig = @{
        enableKeycloakIntegration = $true
        useKeycloakForNewLogins = $true
        maintainCustomJwtSessions = $true
    }
    
    $response = Invoke-RestMethod `
        -Uri "$($config.IdentityServiceUrl)/api/config/auth" `
        -Method Put `
        -Body ($identityConfig | ConvertTo-Json) `
        -ContentType "application/json"
    
    Write-Log "Identity Service updated - Keycloak is now primary"
    
    # Monitor authentication metrics
    Write-Log "Monitoring authentication metrics for 5 minutes..."
    for ($i = 0; $i -lt 5; $i++) {
        Start-Sleep -Seconds 60
        $metrics = Invoke-RestMethod -Uri "$($config.IdentityServiceUrl)/api/platform/migration/metrics" -Method Get
        Write-Log "Minute $($i+1): Success rate=$($metrics.successRate)%, Custom JWT=$($metrics.customJwtCount), Keycloak=$($metrics.keycloakCount)"
        
        if ($metrics.successRate -lt 99.5) {
            Write-Log "Success rate dropped below threshold" -Level "ERROR"
            Invoke-Rollback -Phase 3
            return $false
        }
    }
    
    Send-Notification -Phase 3 -Status "Completed" -Message "Keycloak is now primary IdP"
    
    return $true
}

# Phase 4-6 implementations...
# (Abbreviated for brevity - similar structure)

# Rollback function
function Invoke-Rollback {
    param([int]$Phase)
    
    Write-Log "=== INITIATING ROLLBACK FROM PHASE $Phase ===" -Level "WARN"
    
    switch ($Phase) {
        2 {
            Write-Log "Rolling back dual-token activation..."
            $gatewayConfig = @{
                enableKeycloakValidation = $false
                enableCustomJwtValidation = $true
            }
            Invoke-RestMethod -Uri "$($config.ApiGatewayUrl)/api/config/auth" -Method Put -Body ($gatewayConfig | ConvertTo-Json) -ContentType "application/json"
        }
        3 {
            Write-Log "Rolling back to custom JWT primary..."
            $identityConfig = @{
                useKeycloakForNewLogins = $false
            }
            Invoke-RestMethod -Uri "$($config.IdentityServiceUrl)/api/config/auth" -Method Put -Body ($identityConfig | ConvertTo-Json) -ContentType "application/json"
        }
    }
    
    Send-Notification -Phase $Phase -Status "Rolled Back" -Message "Migration rolled back from phase $Phase"
}

# Notification function
function Send-Notification {
    param([int]$Phase, [string]$Status, [string]$Message)
    
    $payload = @{
        title = "IAM Migration - $Environment"
        text = "Phase $Phase $Status: $Message"
        timestamp = (Get-Date).ToString("o")
    } | ConvertTo-Json
    
    if ($config.NotificationWebhook) {
        Invoke-RestMethod -Uri $config.NotificationWebhook -Method Post -Body $payload -ContentType "application/json"
    }
}

# Main orchestration
Write-Log "Starting IAM Migration Orchestration - Environment: $Environment, Phase: $Phase, DryRun: $DryRun"

if ($Phase -eq "all") {
    $phases = 0..6
} else {
    $phases = @([int]$Phase)
}

$overallSuccess = $true

foreach ($p in $phases) {
    $success = switch ($p) {
        0 { Invoke-Phase0-Validation }
        1 { Invoke-Phase1-UserProvisioning }
        2 { Invoke-Phase2-DualTokenActivation }
        3 { Invoke-Phase3-KeycloakPrimary }
        default { Write-Log "Phase $p not implemented yet" -Level "WARN"; $true }
    }
    
    if (-not $success) {
        Write-Log "Phase $p failed. Migration stopped." -Level "ERROR"
        $overallSuccess = $false
        break
    }
    
    Write-Log "Phase $p completed successfully"
}

if ($overallSuccess) {
    Write-Log "=== MIGRATION COMPLETED SUCCESSFULLY ===" -Level "INFO"
} else {
    Write-Log "=== MIGRATION FAILED ===" -Level "ERROR"
}

Write-Log "Migration log saved to: $logFile"
```

---

## Implementation Steps

### Step 1: Create Migration API Endpoints

**Location:** `IntelliFin.IdentityService/Controllers/Platform/MigrationController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.IdentityService.Controllers.Platform;

[ApiController]
[Route("api/platform/migration")]
[Authorize(Policy = "RequireSystemAdmin")]
public class MigrationController : ControllerBase
{
    private readonly IMigrationOrchestrationService _migrationService;

    [HttpGet("verify-schema")]
    public async Task<IActionResult> VerifySchema()
    {
        var result = await _migrationService.VerifyDatabaseSchemaAsync();
        return Ok(result);
    }

    [HttpPost("baseline")]
    public async Task<IActionResult> CaptureBaseline()
    {
        var baseline = await _migrationService.CapturePerformanceBaselineAsync();
        return Ok(baseline);
    }

    [HttpGet("user-count")]
    public async Task<IActionResult> GetUserCount()
    {
        var count = await _migrationService.GetActiveUserCountAsync();
        return Ok(new { count });
    }

    [HttpPost("provision-all")]
    public async Task<IActionResult> ProvisionAllUsers([FromBody] BulkProvisionRequest request)
    {
        var result = await _migrationService.BulkProvisionUsersAsync(request.BatchSize, request.DryRun);
        return Ok(result);
    }

    [HttpPost("verify-provision")]
    public async Task<IActionResult> VerifyProvisioning([FromBody] VerificationRequest request)
    {
        var result = await _migrationService.VerifyProvisioningSampleAsync(request.SampleSize);
        return Ok(result);
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMigrationMetrics()
    {
        var metrics = await _migrationService.GetCurrentMetricsAsync();
        return Ok(metrics);
    }
}
```

### Step 2: Create Migration Monitoring Dashboard

**Location:** `infra/monitoring/grafana-dashboards/iam-migration-dashboard.json`

Dashboard panels:
- Current migration phase indicator
- Authentication success rate (realtime)
- Token type distribution (custom JWT vs Keycloak)
- API Gateway performance (p50, p95, p99 latency)
- Error rate by authentication type
- Active sessions by token type
- User provisioning progress

### Step 3: Create Rollback Runbooks

**Location:** `docs/operations/iam-migration-rollback-runbook.md`

Detailed procedures for:
- Phase 2 rollback (disable Keycloak validation)
- Phase 3 rollback (revert to custom JWT primary)
- Phase 4 rollback (extend JWT expiry)
- Database restoration procedures
- Keycloak user cleanup

---

## Testing Requirements

### Pre-Production Testing

**Staging Environment Full Migration Test:**

```powershell
# Execute complete migration in staging
.\scripts\migration\iam-migration-orchestrator.ps1 -Phase all -Environment staging

# Verify all phases completed
# Test authentication with both token types
# Verify user provisioning accuracy
# Measure performance impact
```

### Rollback Testing

```powershell
# Test rollback from Phase 2
.\scripts\migration\iam-migration-orchestrator.ps1 -Phase 2 -Environment staging
# Execute rollback
.\scripts\migration\iam-migration-rollback.ps1 -Phase 2 -Environment staging

# Verify custom JWT still works
# Verify Keycloak validation disabled
```

### Load Testing During Migration

```bash
# Simulate production load during migration phases
artillery run tests/load/auth-migration-load-test.yml --environment staging

# Success criteria:
# - 99.5% success rate maintained
# - p95 latency < 200ms
# - No 5xx errors
```

---

## Integration Verification

### Checkpoint 1: Phase 0 - Pre-Migration Health

**Verification:**
- All health checks pass
- Database schema valid
- Backups verified and recent
- Baseline metrics captured

### Checkpoint 2: Phase 1 - User Provisioning Complete

**Verification:**
- 100% active users provisioned to Keycloak
- Random sample validation >99% match
- Role mappings accurate
- Custom claims present

### Checkpoint 3: Phase 2 - Dual-Token Validation Active

**Verification:**
- Custom JWT validates successfully
- Keycloak JWT validates successfully
- API Gateway routes both correctly
- No increase in authentication failures

### Checkpoint 4: Phase 3 - Keycloak Primary

**Verification:**
- New logins use Keycloak
- Existing sessions remain valid
- Success rate >99.5%
- Performance degradation <5%

### Checkpoint 5: Complete Migration

**Verification:**
- All users transitioned to Keycloak
- Legacy endpoints disabled
- Monitoring shows Keycloak-only traffic
- No reported authentication issues

---

## Definition of Done

- [ ] Migration orchestration script created and tested
- [ ] All 7 phases implemented with health checks
- [ ] Rollback procedures documented and tested for each phase
- [ ] Migration monitoring dashboard deployed
- [ ] Communication plan template created
- [ ] Staging environment migration completed successfully
- [ ] Load testing passed during migration
- [ ] Rollback tested and verified (Phases 2-4)
- [ ] Operations team trained on migration procedures
- [ ] Production migration playbook approved
- [ ] Code review completed
- [ ] All documentation updated

---

## Dependencies

**Upstream Dependencies:**
- All Stories 1.1-1.6 (Foundation & Keycloak)
- All Stories 2.1-2.2 (Tenancy)
- All Stories 3.1-3.2 (Service Auth)
- All Stories 4.1-4.2 (SoD & APIs)
- Story 1.7 (Baseline Roles)

**Downstream Dependencies:**
- Production deployment approval
- Story 6.2 (Self-Service Password Reset) - requires Keycloak primary

---

## Notes for Developers

### Critical Success Factors

1. **Zero Downtime**: Dual-token validation is key - test thoroughly
2. **Monitoring**: Watch success rates in realtime during each phase
3. **Communication**: Notify stakeholders at each phase boundary
4. **Rollback Readiness**: Have rollback commands ready before starting each phase

### Common Issues

**Issue 1:** Keycloak performance degradation during bulk provisioning
- **Solution:** Use batch provisioning with rate limiting (100 users/batch, 500ms delay)

**Issue 2:** Custom JWT sessions lost during API Gateway restart
- **Solution:** Use rolling restart, drain connections first

**Issue 3:** Users confused by login redirect change
- **Solution:** Add transitional messaging on login page

### Production Migration Timeline

**Recommended Schedule:**
- **Week 1:** Staging migration and validation
- **Week 2:** Load testing and rollback drills
- **Week 3:** Production Phase 0-2 (off-hours)
- **Week 4:** Production Phase 3 (low-traffic window)
- **Weeks 5-8:** Phase 4 (gradual session migration)
- **Week 12:** Phase 5 (deprecation warnings)
- **Week 16:** Phase 6 (legacy deactivation)

---

**END OF STORY 6.1**
