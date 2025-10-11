# Story 1.22: Policy-Driven Configuration Management

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.22 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 4: Governance & Workflows |
| **Sprint** | Sprint 7 |
| **Story Points** | 13 |
| **Estimated Effort** | 7-10 days |
| **Priority** | P1 (High) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Story 1.19 (Camunda workflows), Kubernetes ConfigMaps, Story 1.25 (GitOps for full automation) |
| **Blocks** | Story 1.25 (ArgoCD integration) |

---

## User Story

**As a** System Administrator,  
**I want** sensitive configuration changes to require policy validation and approval,  
**so that** we prevent unauthorized or risky config changes that could impact production.

---

## Business Value

Policy-driven configuration management addresses critical operational risks:

- **Change Control**: Prevents unauthorized or accidental configuration changes that could disrupt service
- **Audit Trail**: Complete visibility into what changed, when, who approved it, and why
- **Risk Mitigation**: Sensitive configurations (security, financial thresholds) require approval before deployment
- **Rollback Safety**: Git-based versioning enables quick rollback to previous working configurations
- **Compliance**: Meets change management and audit requirements for financial services

This story establishes governance over infrastructure configuration, a critical operational control.

---

## Acceptance Criteria

### AC1: Configuration Policy Schema Defined
**Given** Configuration policies need to be managed  
**When** defining policy schema  
**Then**:
- Policy schema includes:
  - `configKey`: Configuration parameter name (e.g., `jwt.expiry.minutes`)
  - `requiresApproval`: Boolean flag indicating if approval needed
  - `approvalWorkflow`: Camunda process ID for approval
  - `sensitivity`: Low, Medium, High, Critical
  - `allowedValues`: Optional regex or list of valid values
  - `description`: Human-readable description of config parameter
- Policies stored in database table `ConfigurationPolicies`
- Policies configurable via Admin API and Admin UI
- Policy validation occurs before any config change

### AC2: Configuration Change Request API
**Given** Admin needs to change configuration  
**When** submitting config change request  
**Then**:
- POST `/api/admin/config/change-request` endpoint accepts:
  - `configKey`, `newValue`, `justification`, `category` (Application, Infrastructure, Security)
- Policy validation performed against `ConfigurationPolicies`
- If `requiresApproval: true`:
  - Camunda workflow triggered
  - HTTP 202 Accepted returned with change request ID
  - Config change status: `Pending`
- If `requiresApproval: false`:
  - Config change applied immediately
  - HTTP 200 OK returned
  - Config change status: `Applied`

### AC3: Camunda Approval Workflow
**Given** Config change requires approval  
**When** Camunda workflow executes  
**Then**:
- BPMN process `config-change-approval.bpmn` deployed
- Process includes: Start Event → Validate Policy → Manager Approval Task → Apply Config → End Event
- Manager receives notification with change details and risk assessment
- Manager can approve/reject via Admin UI
- Upon approval:
  - Config applied to Kubernetes ConfigMap
  - ArgoCD sync triggered (if available)
  - Change request status updated: `Approved` → `Applied`
- Upon rejection:
  - Change request status: `Rejected`
  - Notification sent to requester with rejection reason

### AC4: Kubernetes ConfigMap Integration
**Given** Config change is approved  
**When** applying configuration to Kubernetes  
**Then**:
- Kubernetes API client integrated in Admin Service
- Config changes written to appropriate ConfigMap:
  - Application configs: `<service-name>-config` ConfigMap
  - Infrastructure configs: `infrastructure-config` ConfigMap
  - Security configs: `security-config` ConfigMap
- ConfigMap update triggers pod rolling restart (Kubernetes native behavior)
- Config change history tracked in Git repository `intellifin-k8s-config`
- Git commit includes: timestamp, change author, approval metadata, justification

### AC5: Git-Based Configuration Versioning
**Given** Configuration changes need version control  
**When** config change is applied  
**Then**:
- Git repository `intellifin-k8s-config` cloned/synced by Admin Service
- Config change written to YAML file in Git repo
- Git commit created with message:
  - `[ConfigChange] <configKey>: <oldValue> -> <newValue> (Approved by <approver>)`
- Git push to remote repository
- Commit SHA stored in `ConfigurationChanges` database table
- Git history provides complete audit trail

### AC6: Configuration Rollback API
**Given** Config change causes issues  
**When** requesting rollback  
**Then**:
- POST `/api/admin/config/rollback` endpoint accepts `changeRequestId` or `gitCommitSha`
- Rollback retrieves previous config value from Git history
- Rollback requires MFA authentication
- Rollback creates new change request (expedited approval if critical)
- Rollback applies config to Kubernetes ConfigMap
- Rollback logged in audit trail
- Admin UI shows rollback history

### AC7: Admin UI Configuration Management Interface
**Given** Admin UI provides config management  
**When** admin navigates to configuration page  
**Then**:
- UI displays all configurable parameters grouped by category
- Current value, policy (approval required?), sensitivity level shown
- "Change Configuration" button triggers change request modal
- Modal includes:
  - Config key (read-only)
  - Current value (read-only)
  - New value (input field with validation)
  - Justification (required, min 20 chars)
  - Risk indicator (auto-calculated based on policy)
- Pending change requests list with status
- Config change history with drill-down to Git commits

### AC8: Configuration Change Audit Trail
**Given** Config changes occur  
**When** audit events are logged  
**Then**:
- Audit events logged for all config activities:
  - `ConfigChangeRequested`: Change request submitted
  - `ConfigChangeApproved`: Manager approved change
  - `ConfigChangeRejected`: Manager rejected change
  - `ConfigChangeApplied`: Config deployed to Kubernetes
  - `ConfigChangeRolledBack`: Config reverted to previous version
  - `ConfigPolicyUpdated`: Configuration policy modified
- All audit events include: correlation ID, config key, old value, new value, approver, justification
- Sensitive config values redacted in audit logs (e.g., passwords, secrets)
- Audit events queryable via Admin Service audit API

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Lines 1160-1184 (Story 1.22), Phase 4 Overview  
**Architecture Sections**: Section 4.1 (Admin Service), Section 9 (Kubernetes Infrastructure), Section 5 (Camunda Workflows)  
**Requirements**: FR8 (Configuration management), NFR12 (Config deployment <5 minutes)

### Technology Stack

- **Workflow Engine**: Camunda 8 (Zeebe)
- **Configuration Storage**: Kubernetes ConfigMaps
- **Version Control**: Git (Azure DevOps, GitHub, GitLab)
- **Kubernetes Client**: KubernetesClient (C# library)
- **Git Client**: LibGit2Sharp (C# Git library)
- **Database**: SQL Server 2022 (Admin Service database)
- **Frontend**: React with configuration management UI

### Database Schema

```sql
-- Admin Service Database

CREATE TABLE ConfigurationPolicies (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ConfigKey NVARCHAR(200) NOT NULL UNIQUE,
    Category NVARCHAR(50) NOT NULL,  -- Application, Infrastructure, Security
    RequiresApproval BIT NOT NULL DEFAULT 0,
    ApprovalWorkflow NVARCHAR(100),  -- Camunda process ID
    Sensitivity NVARCHAR(20) NOT NULL,  -- Low, Medium, High, Critical
    AllowedValuesRegex NVARCHAR(500),  -- Regex for validation
    AllowedValuesList NVARCHAR(MAX),  -- JSON array of allowed values
    Description NVARCHAR(1000),
    CurrentValue NVARCHAR(MAX),
    KubernetesNamespace NVARCHAR(100),
    KubernetesConfigMap NVARCHAR(100),
    ConfigMapKey NVARCHAR(200),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy NVARCHAR(100),
    
    INDEX IX_ConfigKey (ConfigKey),
    INDEX IX_Category (Category),
    INDEX IX_Sensitivity (Sensitivity)
);

-- Seed configuration policies
INSERT INTO ConfigurationPolicies (ConfigKey, Category, RequiresApproval, Sensitivity, Description, KubernetesNamespace, KubernetesConfigMap, ConfigMapKey)
VALUES 
    ('jwt.expiry.minutes', 'Security', 1, 'High', 'JWT token expiration time in minutes', 'default', 'identity-service-config', 'JwtExpiryMinutes'),
    ('jwt.refresh.expiry.days', 'Security', 1, 'High', 'Refresh token expiration time in days', 'default', 'identity-service-config', 'RefreshExpiryDays'),
    ('loan.approval.threshold', 'Application', 1, 'Critical', 'Loan amount requiring senior approval', 'default', 'loan-service-config', 'ApprovalThreshold'),
    ('audit.retention.days', 'Security', 1, 'High', 'Audit log retention period in days', 'default', 'admin-service-config', 'AuditRetentionDays'),
    ('api.rate.limit.requests', 'Infrastructure', 0, 'Medium', 'API rate limit requests per minute', 'default', 'api-gateway-config', 'RateLimitRequests'),
    ('logging.level', 'Application', 0, 'Low', 'Application logging level (Debug, Info, Warning, Error)', 'default', 'api-gateway-config', 'LogLevel'),
    ('mfa.required.threshold', 'Security', 1, 'Critical', 'Transaction amount requiring MFA', 'default', 'identity-service-config', 'MfaThreshold'),
    ('database.connection.timeout', 'Infrastructure', 0, 'Medium', 'Database connection timeout in seconds', 'default', 'loan-service-config', 'DbConnectionTimeout');

CREATE TABLE ConfigurationChanges (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ChangeRequestId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    ConfigKey NVARCHAR(200) NOT NULL,
    OldValue NVARCHAR(MAX),
    NewValue NVARCHAR(MAX),
    Justification NVARCHAR(1000) NOT NULL,
    Category NVARCHAR(50) NOT NULL,
    Status NVARCHAR(50) NOT NULL,  -- Pending, Approved, Rejected, Applied, Failed, RolledBack
    Sensitivity NVARCHAR(20) NOT NULL,
    
    RequestedBy NVARCHAR(100) NOT NULL,
    RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ApprovedBy NVARCHAR(100) NULL,
    ApprovedAt DATETIME2 NULL,
    RejectedBy NVARCHAR(100) NULL,
    RejectedAt DATETIME2 NULL,
    RejectionReason NVARCHAR(500) NULL,
    AppliedAt DATETIME2 NULL,
    
    GitCommitSha NVARCHAR(100),  -- Git commit hash after applying change
    GitRepository NVARCHAR(200) DEFAULT 'intellifin-k8s-config',
    GitBranch NVARCHAR(100) DEFAULT 'main',
    
    KubernetesNamespace NVARCHAR(100),
    KubernetesConfigMap NVARCHAR(100),
    ConfigMapKey NVARCHAR(200),
    
    CamundaProcessInstanceId NVARCHAR(100),
    CorrelationId NVARCHAR(100),
    
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_ConfigKey (ConfigKey),
    INDEX IX_Status (Status),
    INDEX IX_RequestedAt (RequestedAt DESC),
    INDEX IX_ChangeRequestId (ChangeRequestId)
);

CREATE TABLE ConfigurationRollbacks (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    RollbackId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    OriginalChangeRequestId UNIQUEIDENTIFIER NOT NULL,
    NewChangeRequestId UNIQUEIDENTIFIER NOT NULL,  -- Rollback creates new change request
    ConfigKey NVARCHAR(200) NOT NULL,
    RolledBackValue NVARCHAR(MAX),  -- Value being rolled back to
    Reason NVARCHAR(500) NOT NULL,
    
    RolledBackBy NVARCHAR(100) NOT NULL,
    RolledBackAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_OriginalChangeRequestId (OriginalChangeRequestId),
    INDEX IX_NewChangeRequestId (NewChangeRequestId)
);

-- View for pending config changes
CREATE VIEW vw_PendingConfigChanges AS
SELECT 
    ChangeRequestId,
    ConfigKey,
    OldValue,
    NewValue,
    Justification,
    Sensitivity,
    RequestedBy,
    RequestedAt,
    DATEDIFF(HOUR, RequestedAt, GETUTCDATE()) AS HoursPending
FROM ConfigurationChanges
WHERE Status = 'Pending'
ORDER BY RequestedAt;
GO
```

### API Endpoints

#### Configuration Management API

```csharp
// Controllers/ConfigurationController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IntelliFin.Admin.Services;
using IntelliFin.Admin.Models;
using System.Security.Claims;

namespace IntelliFin.Admin.Controllers
{
    [ApiController]
    [Route("api/admin/config")]
    [Authorize(Roles = "System Administrator")]
    public class ConfigurationController : ControllerBase
    {
        private readonly IConfigurationManagementService _configService;
        private readonly ILogger<ConfigurationController> _logger;

        public ConfigurationController(
            IConfigurationManagementService configService,
            ILogger<ConfigurationController> logger)
        {
            _configService = configService;
            _logger = logger;
        }

        /// <summary>
        /// Get all configuration policies
        /// </summary>
        [HttpGet("policies")]
        [ProducesResponseType(typeof(List<ConfigurationPolicyDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPolicies(
            [FromQuery] string? category = null,
            CancellationToken cancellationToken = default)
        {
            var policies = await _configService.GetPoliciesAsync(category, cancellationToken);
            return Ok(policies);
        }

        /// <summary>
        /// Get current configuration values
        /// </summary>
        [HttpGet("values")]
        [ProducesResponseType(typeof(List<ConfigurationValueDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetValues(
            [FromQuery] string? category = null,
            CancellationToken cancellationToken = default)
        {
            var values = await _configService.GetCurrentValuesAsync(category, cancellationToken);
            return Ok(values);
        }

        /// <summary>
        /// Request configuration change
        /// </summary>
        [HttpPost("change-request")]
        [RequiresMfa(TimeoutMinutes = 15)]
        [ProducesResponseType(typeof(ConfigChangeResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ConfigChangeResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RequestChange(
            [FromBody] ConfigChangeRequest request,
            CancellationToken cancellationToken)
        {
            var requestorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var requestorName = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation(
                "Config change request: ConfigKey={ConfigKey}, Requestor={RequestorId}",
                request.ConfigKey, requestorId);

            try
            {
                var response = await _configService.RequestChangeAsync(
                    request,
                    requestorId,
                    requestorName,
                    cancellationToken);

                if (response.RequiresApproval)
                {
                    return AcceptedAtAction(
                        nameof(GetChangeRequestStatus),
                        new { changeRequestId = response.ChangeRequestId },
                        response);
                }
                else
                {
                    return Ok(response);
                }
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Message, validationErrors = ex.Errors });
            }
        }

        /// <summary>
        /// Get change request status
        /// </summary>
        [HttpGet("change-requests/{changeRequestId}")]
        [ProducesResponseType(typeof(ConfigChangeStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetChangeRequestStatus(
            Guid changeRequestId,
            CancellationToken cancellationToken)
        {
            var status = await _configService.GetChangeRequestStatusAsync(changeRequestId, cancellationToken);
            
            if (status == null)
                return NotFound();

            return Ok(status);
        }

        /// <summary>
        /// List change requests
        /// </summary>
        [HttpGet("change-requests")]
        [ProducesResponseType(typeof(PagedResult<ConfigChangeSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListChangeRequests(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            var result = await _configService.ListChangeRequestsAsync(
                status,
                page,
                pageSize,
                cancellationToken);

            return Ok(result);
        }

        /// <summary>
        /// Approve config change (Manager only)
        /// </summary>
        [HttpPost("change-requests/{changeRequestId}/approve")]
        [Authorize(Roles = "System Administrator,Manager")]
        [RequiresMfa(TimeoutMinutes = 15)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApproveChange(
            Guid changeRequestId,
            [FromBody] ConfigChangeApprovalDto approval,
            CancellationToken cancellationToken)
        {
            var approverId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var approverName = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation(
                "Config change approval: ChangeRequestId={ChangeRequestId}, Approver={ApproverId}",
                changeRequestId, approverId);

            try
            {
                await _configService.ApproveChangeAsync(
                    changeRequestId,
                    approverId,
                    approverName,
                    approval.Comments,
                    cancellationToken);

                return Ok(new { message = "Configuration change approved and applied successfully" });
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Reject config change (Manager only)
        /// </summary>
        [HttpPost("change-requests/{changeRequestId}/reject")]
        [Authorize(Roles = "System Administrator,Manager")]
        [RequiresMfa(TimeoutMinutes = 15)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RejectChange(
            Guid changeRequestId,
            [FromBody] ConfigChangeRejectionDto rejection,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(rejection.Reason) || rejection.Reason.Length < 20)
                return BadRequest(new { error = "Rejection reason must be at least 20 characters" });

            var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var reviewerName = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation(
                "Config change rejection: ChangeRequestId={ChangeRequestId}, Reviewer={ReviewerId}",
                changeRequestId, reviewerId);

            try
            {
                await _configService.RejectChangeAsync(
                    changeRequestId,
                    reviewerId,
                    reviewerName,
                    rejection.Reason,
                    cancellationToken);

                return Ok(new { message = "Configuration change rejected successfully" });
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Rollback configuration change
        /// </summary>
        [HttpPost("rollback")]
        [RequiresMfa(TimeoutMinutes = 15)]
        [ProducesResponseType(typeof(ConfigRollbackResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RollbackChange(
            [FromBody] ConfigRollbackRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 20)
                return BadRequest(new { error = "Rollback reason must be at least 20 characters" });

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var adminName = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogWarning(
                "Config rollback request: ChangeRequestId={ChangeRequestId}, Admin={AdminId}",
                request.ChangeRequestId, adminId);

            try
            {
                var response = await _configService.RollbackChangeAsync(
                    request.ChangeRequestId,
                    request.Reason,
                    adminId,
                    adminName,
                    cancellationToken);

                return AcceptedAtAction(
                    nameof(GetChangeRequestStatus),
                    new { changeRequestId = response.NewChangeRequestId },
                    response);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Get configuration change history
        /// </summary>
        [HttpGet("history/{configKey}")]
        [ProducesResponseType(typeof(List<ConfigChangeHistoryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetHistory(
            string configKey,
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default)
        {
            var history = await _configService.GetChangeHistoryAsync(configKey, limit, cancellationToken);
            return Ok(history);
        }

        /// <summary>
        /// Update configuration policy (System Administrator only)
        /// </summary>
        [HttpPut("policies/{policyId}")]
        [RequiresMfa(TimeoutMinutes = 15)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdatePolicy(
            int policyId,
            [FromBody] ConfigPolicyUpdateDto update,
            CancellationToken cancellationToken)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation(
                "Config policy update: PolicyId={PolicyId}, Admin={AdminId}",
                policyId, adminId);

            try
            {
                await _configService.UpdatePolicyAsync(policyId, update, adminId, cancellationToken);
                return Ok(new { message = "Configuration policy updated successfully" });
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }
    }
}
```

### Service Implementation

```csharp
// Services/ConfigurationManagementService.cs
using IntelliFin.Admin.Data;
using IntelliFin.Admin.Models;
using IntelliFin.Shared.Kubernetes;
using IntelliFin.Shared.Git;
using IntelliFin.Shared.Camunda;
using IntelliFin.Shared.Audit;
using Microsoft.EntityFrameworkCore;
using k8s;
using k8s.Models;
using LibGit2Sharp;

namespace IntelliFin.Admin.Services
{
    public interface IConfigurationManagementService
    {
        Task<List<ConfigurationPolicyDto>> GetPoliciesAsync(string? category, CancellationToken cancellationToken);
        Task<List<ConfigurationValueDto>> GetCurrentValuesAsync(string? category, CancellationToken cancellationToken);
        Task<ConfigChangeResponse> RequestChangeAsync(
            ConfigChangeRequest request,
            string requestorId,
            string requestorName,
            CancellationToken cancellationToken);
        Task<ConfigChangeStatusDto?> GetChangeRequestStatusAsync(Guid changeRequestId, CancellationToken cancellationToken);
        Task ApproveChangeAsync(
            Guid changeRequestId,
            string approverId,
            string approverName,
            string comments,
            CancellationToken cancellationToken);
        Task RejectChangeAsync(
            Guid changeRequestId,
            string reviewerId,
            string reviewerName,
            string reason,
            CancellationToken cancellationToken);
        Task<ConfigRollbackResponse> RollbackChangeAsync(
            Guid changeRequestId,
            string reason,
            string adminId,
            string adminName,
            CancellationToken cancellationToken);
        Task<List<ConfigChangeHistoryDto>> GetChangeHistoryAsync(string configKey, int limit, CancellationToken cancellationToken);
        Task<PagedResult<ConfigChangeSummaryDto>> ListChangeRequestsAsync(
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken);
        Task UpdatePolicyAsync(int policyId, ConfigPolicyUpdateDto update, string adminId, CancellationToken cancellationToken);
    }

    public class ConfigurationManagementService : IConfigurationManagementService
    {
        private readonly AdminDbContext _dbContext;
        private readonly IKubernetesClient _k8sClient;
        private readonly IGitService _gitService;
        private readonly ICamundaClient _camundaClient;
        private readonly IAuditService _auditService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ConfigurationManagementService> _logger;

        private const string GIT_REPO_NAME = "intellifin-k8s-config";
        private const string GIT_BRANCH = "main";

        public ConfigurationManagementService(
            AdminDbContext dbContext,
            IKubernetesClient k8sClient,
            IGitService gitService,
            ICamundaClient camundaClient,
            IAuditService auditService,
            INotificationService notificationService,
            ILogger<ConfigurationManagementService> logger)
        {
            _dbContext = dbContext;
            _k8sClient = k8sClient;
            _gitService = gitService;
            _camundaClient = camundaClient;
            _auditService = auditService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<ConfigChangeResponse> RequestChangeAsync(
            ConfigChangeRequest request,
            string requestorId,
            string requestorName,
            CancellationToken cancellationToken)
        {
            // Get policy for this config key
            var policy = await _dbContext.ConfigurationPolicies
                .FirstOrDefaultAsync(p => p.ConfigKey == request.ConfigKey, cancellationToken);

            if (policy == null)
                throw new ValidationException($"Configuration key '{request.ConfigKey}' not found");

            // Validate new value
            if (!ValidateConfigValue(policy, request.NewValue))
                throw new ValidationException($"Invalid value for '{request.ConfigKey}'. {policy.Description}");

            if (string.IsNullOrWhiteSpace(request.Justification) || request.Justification.Length < 20)
                throw new ValidationException("Justification must be at least 20 characters");

            var correlationId = Guid.NewGuid().ToString("N");
            var changeRequestId = Guid.NewGuid();

            // Get current value from Kubernetes
            var currentValue = await GetCurrentConfigValueAsync(
                policy.KubernetesNamespace,
                policy.KubernetesConfigMap,
                policy.ConfigMapKey,
                cancellationToken);

            // Create change request record
            var changeRequest = new ConfigurationChange
            {
                ChangeRequestId = changeRequestId,
                ConfigKey = request.ConfigKey,
                OldValue = currentValue,
                NewValue = request.NewValue,
                Justification = request.Justification,
                Category = policy.Category,
                Status = policy.RequiresApproval ? "Pending" : "Applied",
                Sensitivity = policy.Sensitivity,
                RequestedBy = requestorId,
                RequestedAt = DateTime.UtcNow,
                KubernetesNamespace = policy.KubernetesNamespace,
                KubernetesConfigMap = policy.KubernetesConfigMap,
                ConfigMapKey = policy.ConfigMapKey,
                CorrelationId = correlationId
            };

            _dbContext.ConfigurationChanges.Add(changeRequest);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Audit log
            await _auditService.LogAsync(new AuditEvent
            {
                Actor = requestorId,
                Action = "ConfigChangeRequested",
                EntityType = "ConfigurationChange",
                EntityId = changeRequestId.ToString(),
                CorrelationId = correlationId,
                EventData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    configKey = request.ConfigKey,
                    oldValue = RedactSensitiveValue(policy.Sensitivity, currentValue),
                    newValue = RedactSensitiveValue(policy.Sensitivity, request.NewValue),
                    justification = request.Justification
                })
            }, cancellationToken);

            if (policy.RequiresApproval)
            {
                // Start Camunda approval workflow
                var processInstanceId = await _camundaClient.StartProcessAsync(
                    "config-change-approval",
                    new Dictionary<string, object>
                    {
                        { "changeRequestId", changeRequestId.ToString() },
                        { "configKey", request.ConfigKey },
                        { "oldValue", currentValue ?? "" },
                        { "newValue", request.NewValue },
                        { "justification", request.Justification },
                        { "sensitivity", policy.Sensitivity },
                        { "requestedBy", requestorId },
                        { "correlationId", correlationId }
                    },
                    cancellationToken);

                changeRequest.CamundaProcessInstanceId = processInstanceId;
                await _dbContext.SaveChangesAsync(cancellationToken);

                // Notify managers
                await _notificationService.SendConfigChangeNotificationAsync(
                    new ConfigChangeNotificationDto
                    {
                        ChangeRequestId = changeRequestId,
                        ConfigKey = request.ConfigKey,
                        OldValue = currentValue,
                        NewValue = request.NewValue,
                        Justification = request.Justification,
                        Sensitivity = policy.Sensitivity,
                        RequestedBy = requestorName,
                        RequestedAt = DateTime.UtcNow
                    },
                    cancellationToken);

                _logger.LogInformation(
                    "Config change request created (requires approval): ChangeRequestId={ChangeRequestId}, ConfigKey={ConfigKey}",
                    changeRequestId, request.ConfigKey);

                return new ConfigChangeResponse
                {
                    ChangeRequestId = changeRequestId,
                    Status = "Pending",
                    RequiresApproval = true,
                    Message = "Configuration change request submitted. Awaiting manager approval.",
                    EstimatedApprovalTime = DateTime.UtcNow.AddHours(24)
                };
            }
            else
            {
                // Apply immediately (no approval required)
                await ApplyConfigurationChangeAsync(changeRequest, cancellationToken);

                _logger.LogInformation(
                    "Config change applied immediately (no approval required): ChangeRequestId={ChangeRequestId}, ConfigKey={ConfigKey}",
                    changeRequestId, request.ConfigKey);

                return new ConfigChangeResponse
                {
                    ChangeRequestId = changeRequestId,
                    Status = "Applied",
                    RequiresApproval = false,
                    Message = "Configuration change applied successfully."
                };
            }
        }

        public async Task ApproveChangeAsync(
            Guid changeRequestId,
            string approverId,
            string approverName,
            string comments,
            CancellationToken cancellationToken)
        {
            var changeRequest = await _dbContext.ConfigurationChanges
                .FirstOrDefaultAsync(c => c.ChangeRequestId == changeRequestId, cancellationToken);

            if (changeRequest == null)
                throw new NotFoundException("Configuration change request not found");

            if (changeRequest.Status != "Pending")
                throw new InvalidOperationException($"Change request is not pending (current status: {changeRequest.Status})");

            // Update change request
            changeRequest.Status = "Approved";
            changeRequest.ApprovedBy = approverId;
            changeRequest.ApprovedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Complete Camunda task
            if (!string.IsNullOrEmpty(changeRequest.CamundaProcessInstanceId))
            {
                await _camundaClient.CompleteTaskAsync(
                    changeRequest.CamundaProcessInstanceId,
                    "manager-approval",
                    new Dictionary<string, object>
                    {
                        { "approved", true },
                        { "approvedBy", approverId },
                        { "comments", comments ?? "" }
                    },
                    cancellationToken);
            }

            // Apply configuration change
            await ApplyConfigurationChangeAsync(changeRequest, cancellationToken);

            // Notify requester
            await _notificationService.SendConfigChangeApprovedNotificationAsync(
                changeRequest.RequestedBy,
                new ConfigChangeApprovedDto
                {
                    ChangeRequestId = changeRequestId,
                    ConfigKey = changeRequest.ConfigKey,
                    ApprovedBy = approverName,
                    Comments = comments
                },
                cancellationToken);

            // Audit log
            await _auditService.LogAsync(new AuditEvent
            {
                Actor = approverId,
                Action = "ConfigChangeApproved",
                EntityType = "ConfigurationChange",
                EntityId = changeRequestId.ToString(),
                CorrelationId = changeRequest.CorrelationId,
                EventData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    configKey = changeRequest.ConfigKey,
                    approvedBy = approverId,
                    comments = comments
                })
            }, cancellationToken);

            _logger.LogInformation(
                "Config change approved: ChangeRequestId={ChangeRequestId}, Approver={ApproverId}",
                changeRequestId, approverId);
        }

        private async Task ApplyConfigurationChangeAsync(
            ConfigurationChange changeRequest,
            CancellationToken cancellationToken)
        {
            try
            {
                // Step 1: Update Kubernetes ConfigMap
                await UpdateKubernetesConfigMapAsync(
                    changeRequest.KubernetesNamespace!,
                    changeRequest.KubernetesConfigMap!,
                    changeRequest.ConfigMapKey!,
                    changeRequest.NewValue,
                    cancellationToken);

                // Step 2: Commit to Git
                var commitSha = await CommitConfigChangeToGitAsync(changeRequest, cancellationToken);
                changeRequest.GitCommitSha = commitSha;

                // Step 3: Update change request status
                changeRequest.Status = "Applied";
                changeRequest.AppliedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);

                // Step 4: Update policy current value
                var policy = await _dbContext.ConfigurationPolicies
                    .FirstOrDefaultAsync(p => p.ConfigKey == changeRequest.ConfigKey, cancellationToken);
                if (policy != null)
                {
                    policy.CurrentValue = changeRequest.NewValue;
                    policy.UpdatedAt = DateTime.UtcNow;
                    policy.UpdatedBy = changeRequest.ApprovedBy ?? changeRequest.RequestedBy;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                // Audit log
                await _auditService.LogAsync(new AuditEvent
                {
                    Actor = changeRequest.ApprovedBy ?? "SYSTEM",
                    Action = "ConfigChangeApplied",
                    EntityType = "ConfigurationChange",
                    EntityId = changeRequest.ChangeRequestId.ToString(),
                    CorrelationId = changeRequest.CorrelationId,
                    EventData = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        configKey = changeRequest.ConfigKey,
                        gitCommitSha = commitSha,
                        appliedAt = changeRequest.AppliedAt
                    })
                }, cancellationToken);

                _logger.LogInformation(
                    "Config change applied successfully: ChangeRequestId={ChangeRequestId}, GitCommit={GitCommit}",
                    changeRequest.ChangeRequestId, commitSha);
            }
            catch (Exception ex)
            {
                changeRequest.Status = "Failed";
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogError(ex,
                    "Failed to apply config change: ChangeRequestId={ChangeRequestId}",
                    changeRequest.ChangeRequestId);

                throw;
            }
        }

        private async Task UpdateKubernetesConfigMapAsync(
            string namespaceName,
            string configMapName,
            string key,
            string value,
            CancellationToken cancellationToken)
        {
            var configMap = await _k8sClient.ReadNamespacedConfigMapAsync(namespaceName, configMapName, cancellationToken);

            if (configMap.Data == null)
                configMap.Data = new Dictionary<string, string>();

            configMap.Data[key] = value;

            await _k8sClient.ReplaceNamespacedConfigMapAsync(configMap, namespaceName, configMapName, cancellationToken);

            _logger.LogInformation(
                "Updated Kubernetes ConfigMap: Namespace={Namespace}, ConfigMap={ConfigMap}, Key={Key}",
                namespaceName, configMapName, key);
        }

        private async Task<string> CommitConfigChangeToGitAsync(
            ConfigurationChange changeRequest,
            CancellationToken cancellationToken)
        {
            // Clone or pull latest from Git repo
            var repoPath = await _gitService.EnsureRepositoryAsync(GIT_REPO_NAME, GIT_BRANCH, cancellationToken);

            // Write config change to YAML file
            var configFilePath = Path.Combine(repoPath, "config", $"{changeRequest.KubernetesConfigMap}.yaml");
            await _gitService.UpdateConfigFileAsync(
                configFilePath,
                changeRequest.ConfigMapKey!,
                changeRequest.NewValue,
                cancellationToken);

            // Commit changes
            var commitMessage = $"[ConfigChange] {changeRequest.ConfigKey}: {changeRequest.OldValue} -> {changeRequest.NewValue}";
            if (!string.IsNullOrEmpty(changeRequest.ApprovedBy))
            {
                commitMessage += $" (Approved by {changeRequest.ApprovedBy})";
            }

            var commitSha = await _gitService.CommitAndPushAsync(
                repoPath,
                commitMessage,
                changeRequest.ApprovedBy ?? changeRequest.RequestedBy,
                cancellationToken);

            _logger.LogInformation(
                "Committed config change to Git: Repo={Repo}, Commit={Commit}",
                GIT_REPO_NAME, commitSha);

            return commitSha;
        }

        private async Task<string?> GetCurrentConfigValueAsync(
            string namespaceName,
            string configMapName,
            string key,
            CancellationToken cancellationToken)
        {
            try
            {
                var configMap = await _k8sClient.ReadNamespacedConfigMapAsync(namespaceName, configMapName, cancellationToken);
                return configMap.Data?.ContainsKey(key) == true ? configMap.Data[key] : null;
            }
            catch
            {
                return null;
            }
        }

        private bool ValidateConfigValue(ConfigurationPolicy policy, string value)
        {
            // Regex validation
            if (!string.IsNullOrEmpty(policy.AllowedValuesRegex))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(value, policy.AllowedValuesRegex))
                    return false;
            }

            // Allowed values list validation
            if (!string.IsNullOrEmpty(policy.AllowedValuesList))
            {
                var allowedValues = System.Text.Json.JsonSerializer.Deserialize<List<string>>(policy.AllowedValuesList);
                if (allowedValues != null && !allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private string? RedactSensitiveValue(string sensitivity, string? value)
        {
            if (sensitivity == "Critical" || sensitivity == "High")
            {
                return value != null ? "***REDACTED***" : null;
            }
            return value;
        }

        public async Task<List<ConfigurationPolicyDto>> GetPoliciesAsync(string? category, CancellationToken cancellationToken)
        {
            var query = _dbContext.ConfigurationPolicies.AsQueryable();

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category == category);
            }

            var policies = await query
                .OrderBy(p => p.Category)
                .ThenBy(p => p.ConfigKey)
                .Select(p => new ConfigurationPolicyDto
                {
                    Id = p.Id,
                    ConfigKey = p.ConfigKey,
                    Category = p.Category,
                    RequiresApproval = p.RequiresApproval,
                    Sensitivity = p.Sensitivity,
                    Description = p.Description,
                    CurrentValue = RedactSensitiveValue(p.Sensitivity, p.CurrentValue)
                })
                .ToListAsync(cancellationToken);

            return policies;
        }

        public async Task<List<ConfigurationValueDto>> GetCurrentValuesAsync(string? category, CancellationToken cancellationToken)
        {
            var policies = await GetPoliciesAsync(category, cancellationToken);

            var values = policies.Select(p => new ConfigurationValueDto
            {
                ConfigKey = p.ConfigKey,
                CurrentValue = p.CurrentValue,
                Sensitivity = p.Sensitivity,
                RequiresApproval = p.RequiresApproval
            }).ToList();

            return values;
        }

        // Additional methods (GetChangeRequestStatusAsync, RejectChangeAsync, RollbackChangeAsync, etc.) omitted for brevity
    }
}
```

### Camunda BPMN Process

```xml
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:zeebe="http://camunda.org/schema/zeebe/1.0"
                  id="config-change-approval"
                  targetNamespace="http://intellifin.local/bpmn">
  
  <bpmn:process id="config-change-approval" name="Configuration Change Approval" isExecutable="true">
    
    <!-- Start Event -->
    <bpmn:startEvent id="StartEvent_ConfigChange" name="Config Change Requested">
      <bpmn:outgoing>Flow_ToValidate</bpmn:outgoing>
    </bpmn:startEvent>
    
    <!-- Validate Policy Service Task -->
    <bpmn:serviceTask id="Task_ValidatePolicy" name="Validate Policy">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="validate-config-policy" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToValidate</bpmn:incoming>
      <bpmn:outgoing>Flow_ToApproval</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Manager Approval User Task -->
    <bpmn:userTask id="Task_ManagerApproval" name="Manager Approval">
      <bpmn:extensionElements>
        <zeebe:assignmentDefinition candidateGroups="System Administrator,Manager" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToApproval</bpmn:incoming>
      <bpmn:outgoing>Flow_ToDecision</bpmn:outgoing>
    </bpmn:userTask>
    
    <!-- Exclusive Gateway - Approved? -->
    <bpmn:exclusiveGateway id="Gateway_ApprovalDecision" name="Approved?">
      <bpmn:incoming>Flow_ToDecision</bpmn:incoming>
      <bpmn:outgoing>Flow_ToApply</bpmn:outgoing>
      <bpmn:outgoing>Flow_ToReject</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    
    <!-- Apply Config Service Task -->
    <bpmn:serviceTask id="Task_ApplyConfig" name="Apply Configuration">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="apply-config-change" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToApply</bpmn:incoming>
      <bpmn:outgoing>Flow_ToNotifyApproved</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Notify Approved Service Task -->
    <bpmn:serviceTask id="Task_NotifyApproved" name="Notify Requester (Approved)">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="notify-config-approved" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToNotifyApproved</bpmn:incoming>
      <bpmn:outgoing>Flow_ToEndApproved</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Notify Rejected Service Task -->
    <bpmn:serviceTask id="Task_NotifyRejected" name="Notify Requester (Rejected)">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="notify-config-rejected" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToReject</bpmn:incoming>
      <bpmn:outgoing>Flow_ToEndRejected</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- End Events -->
    <bpmn:endEvent id="EndEvent_Applied" name="Config Applied">
      <bpmn:incoming>Flow_ToEndApproved</bpmn:incoming>
    </bpmn:endEvent>
    
    <bpmn:endEvent id="EndEvent_Rejected" name="Config Rejected">
      <bpmn:incoming>Flow_ToEndRejected</bpmn:incoming>
    </bpmn:endEvent>
    
    <!-- Sequence Flows -->
    <bpmn:sequenceFlow id="Flow_ToValidate" sourceRef="StartEvent_ConfigChange" targetRef="Task_ValidatePolicy" />
    <bpmn:sequenceFlow id="Flow_ToApproval" sourceRef="Task_ValidatePolicy" targetRef="Task_ManagerApproval" />
    <bpmn:sequenceFlow id="Flow_ToDecision" sourceRef="Task_ManagerApproval" targetRef="Gateway_ApprovalDecision" />
    
    <bpmn:sequenceFlow id="Flow_ToApply" sourceRef="Gateway_ApprovalDecision" targetRef="Task_ApplyConfig">
      <bpmn:conditionExpression>${approved == true}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    
    <bpmn:sequenceFlow id="Flow_ToReject" sourceRef="Gateway_ApprovalDecision" targetRef="Task_NotifyRejected">
      <bpmn:conditionExpression>${approved == false}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    
    <bpmn:sequenceFlow id="Flow_ToNotifyApproved" sourceRef="Task_ApplyConfig" targetRef="Task_NotifyApproved" />
    <bpmn:sequenceFlow id="Flow_ToEndApproved" sourceRef="Task_NotifyApproved" targetRef="EndEvent_Applied" />
    <bpmn:sequenceFlow id="Flow_ToEndRejected" sourceRef="Task_NotifyRejected" targetRef="EndEvent_Rejected" />
    
  </bpmn:process>
</bpmn:definitions>
```

### Configuration

```json
// appsettings.json - Admin Service
{
  "ConfigurationManagement": {
    "GitRepository": "https://github.com/intellifin/intellifin-k8s-config.git",
    "GitBranch": "main",
    "GitUsername": "${VAULT_GIT_USERNAME}",
    "GitToken": "${VAULT_GIT_TOKEN}",
    "LocalRepoPath": "/tmp/intellifin-k8s-config"
  },
  "Kubernetes": {
    "InCluster": true,
    "KubeConfigPath": "/root/.kube/config"
  }
}
```

---

## Integration Verification

### IV1: Non-Sensitive Config Changes Bypass Approval Workflow
**Verification Steps**:
1. Request config change for non-sensitive parameter (e.g., `logging.level`)
2. Verify change applies immediately without approval
3. Check Kubernetes ConfigMap updated
4. Verify Git commit created
5. Confirm no Camunda workflow triggered

**Success Criteria**:
- Config change applied immediately (HTTP 200 OK)
- No approval required
- Total time <30 seconds

### IV2: Sensitive Config Changes Blocked Until Approved
**Verification Steps**:
1. Request config change for sensitive parameter (e.g., `jwt.expiry.minutes`)
2. Verify HTTP 202 Accepted returned (pending approval)
3. Attempt to read new value from Kubernetes - should still be old value
4. Manager approves change via Admin UI
5. Verify config applied to Kubernetes
6. Verify Git commit created
7. Check pods restart (if applicable)

**Success Criteria**:
- Config change requires approval
- Old value remains until approved
- New value applied within 5 minutes of approval (NFR12)
- Git commit includes approval metadata

### IV3: Config Deployment Verified via ArgoCD Sync Status
**Verification Steps**:
1. Request and approve config change
2. Verify Git commit created in `intellifin-k8s-config` repo
3. Check ArgoCD detects config drift (if ArgoCD enabled)
4. Verify ArgoCD syncs change to Kubernetes
5. Check ArgoCD sync status shows "Healthy"
6. Verify application pods restart with new config

**Success Criteria**:
- ArgoCD detects config change within 5 minutes
- ArgoCD sync completes successfully
- Pods restart with new configuration
- ArgoCD health check passes

---

## Testing Strategy

### Unit Tests

#### Test: Config Value Validation
```csharp
[Fact]
public void ValidateConfigValue_RegexValidation_ReturnsTrue()
{
    // Arrange
    var service = CreateService();
    var policy = new ConfigurationPolicy
    {
        ConfigKey = "logging.level",
        AllowedValuesRegex = "^(Debug|Info|Warning|Error)$"
    };

    // Act
    var isValid = service.ValidateConfigValue(policy, "Info");

    // Assert
    Assert.True(isValid);
}

[Fact]
public void ValidateConfigValue_InvalidRegex_ReturnsFalse()
{
    // Arrange
    var service = CreateService();
    var policy = new ConfigurationPolicy
    {
        ConfigKey = "logging.level",
        AllowedValuesRegex = "^(Debug|Info|Warning|Error)$"
    };

    // Act
    var isValid = service.ValidateConfigValue(policy, "InvalidLevel");

    // Assert
    Assert.False(isValid);
}
```

#### Test: Sensitive Value Redaction
```csharp
[Fact]
public void RedactSensitiveValue_HighSensitivity_ReturnsRedacted()
{
    // Arrange
    var service = CreateService();

    // Act
    var redacted = service.RedactSensitiveValue("High", "secret-value-123");

    // Assert
    Assert.Equal("***REDACTED***", redacted);
}
```

### Integration Tests

#### Test: End-to-End Config Change Workflow
```csharp
[Fact]
public async Task ConfigChangeWorkflow_RequestApproveApply_Success()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>();
    var client = factory.CreateClient();

    // Act 1: Request config change
    var requestPayload = new
    {
        configKey = "jwt.expiry.minutes",
        newValue = "60",
        justification = "Increasing JWT expiry to improve user experience and reduce token refresh frequency"
    };
    var requestResponse = await client.PostAsJsonAsync("/api/admin/config/change-request", requestPayload);
    requestResponse.EnsureSuccessStatusCode();
    var changeResponse = await requestResponse.Content.ReadFromJsonAsync<ConfigChangeResponse>();

    // Assert: Requires approval
    Assert.True(changeResponse.RequiresApproval);
    Assert.Equal("Pending", changeResponse.Status);

    // Act 2: Approve change
    var approvalPayload = new { comments = "Approved for UX improvement" };
    var approvalResponse = await client.PostAsJsonAsync(
        $"/api/admin/config/change-requests/{changeResponse.ChangeRequestId}/approve",
        approvalPayload);
    approvalResponse.EnsureSuccessStatusCode();

    // Act 3: Verify change applied
    await Task.Delay(TimeSpan.FromSeconds(5)); // Allow time for async processing
    var statusResponse = await client.GetAsync($"/api/admin/config/change-requests/{changeResponse.ChangeRequestId}");
    var status = await statusResponse.Content.ReadFromJsonAsync<ConfigChangeStatusDto>();

    // Assert
    Assert.Equal("Applied", status.Status);
    Assert.NotNull(status.GitCommitSha);
    Assert.NotNull(status.AppliedAt);
}
```

### Performance Tests

#### Test: Config Change Latency
```csharp
[Fact]
public async Task ConfigChange_ApprovalToDeployment_Within5Minutes()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>();
    var client = factory.CreateClient();
    var stopwatch = Stopwatch.StartNew();

    // Act: Request, approve, and verify deployment
    var requestPayload = new { configKey = "api.rate.limit.requests", newValue = "1000", justification = "Increasing rate limit for load test" };
    var requestResponse = await client.PostAsJsonAsync("/api/admin/config/change-request", requestPayload);
    var changeResponse = await requestResponse.Content.ReadFromJsonAsync<ConfigChangeResponse>();

    // Since no approval required for Medium sensitivity, should apply immediately
    stopwatch.Stop();

    // Assert
    Assert.True(stopwatch.Elapsed < TimeSpan.FromMinutes(5), "Config deployment should complete within 5 minutes (NFR12)");
    Assert.Equal("Applied", changeResponse.Status);
}
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| Git repository unavailable | Config changes blocked | Low | Implement retry logic with exponential backoff. Queue changes for later commit. Alert DevOps team. |
| Kubernetes API errors | Config deployment fails | Low | Implement transaction rollback (revert to old value). Alert on deployment failures. Manual intervention procedure. |
| ConfigMap update doesn't trigger pod restart | Old config persists | Medium | Document which services require manual restart. Implement pod restart automation via Kubernetes API. |
| Conflicting config changes | Last write wins, changes overwritten | Low | Implement optimistic concurrency control (check old value before update). Reject if value changed since request. |
| Git merge conflicts | Config commit fails | Low | Use separate files per ConfigMap. Implement conflict detection and resolution workflow. |

---

## Definition of Done

- [ ] Database schema created with config policies, changes, rollbacks
- [ ] API endpoints implemented with full validation
- [ ] Kubernetes client integration complete
- [ ] Git client integration complete
- [ ] Camunda BPMN process deployed
- [ ] Configuration policy seeding complete
- [ ] Admin UI config management interface complete
- [ ] Unit tests: >85% code coverage
- [ ] Integration tests: All workflows pass
- [ ] Performance test: Config deployment <5 minutes (NFR12)
- [ ] Security review: MFA required for config changes
- [ ] Audit events logged for all config activities
- [ ] Documentation: Config policy guide, rollback procedure
- [ ] Training materials for System Administrators

---

## Related Documentation

### PRD References
- **Lines 1160-1184**: Story 1.22 detailed requirements
- **Lines 1079-1243**: Phase 4 (Governance & Workflows) overview
- **FR8**: Configuration management and approval
- **NFR12**: Config deployment <5 minutes

### Architecture References
- **Section 4.1**: Admin Service architecture
- **Section 9**: Kubernetes Infrastructure
- **Section 5**: Camunda Workflow Integration

### External Documentation
- [Kubernetes ConfigMaps](https://kubernetes.io/docs/concepts/configuration/configmap/)
- [LibGit2Sharp Documentation](https://github.com/libgit2/libgit2sharp)
- [ArgoCD Configuration Management](https://argo-cd.readthedocs.io/)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Set up Git repository `intellifin-k8s-config`
- [ ] Configure Git credentials in Vault
- [ ] Test Kubernetes API access from Admin Service pod
- [ ] Review ConfigMap naming conventions
- [ ] Plan pod restart strategy for config changes
- [ ] Coordinate with DevOps on ArgoCD integration timeline
- [ ] Document config rollback emergency procedure

### Post-Implementation Handoff
- [ ] Train System Administrators on config change workflow
- [ ] Demo config approval process to managers
- [ ] Create config policy reference guide
- [ ] Set up monitoring for failed config deployments
- [ ] Schedule quarterly config policy review
- [ ] Add config change metrics to operations dashboard
- [ ] Document common rollback scenarios

### Technical Debt / Future Enhancements
- [ ] Implement config change impact analysis (which pods affected)
- [ ] Add config validation testing (apply to test environment first)
- [ ] Create config change scheduling (deploy during maintenance window)
- [ ] Implement blue/green config deployment (canary testing)
- [ ] Add config drift detection (alert on manual changes)
- [ ] Create config change simulation tool (preview impact)

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: [Story 1.23: Vault Secret Rotation Automation](./story-1.23-vault-secret-rotation.md)
