# Story 1.19: JIT Privilege Elevation with Camunda Workflows

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.19 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 4: Governance & Workflows |
| **Sprint** | Sprint 6 |
| **Story Points** | 13 |
| **Estimated Effort** | 7-10 days |
| **Priority** | P0 (Critical for governance) |
| **Status** | ✅ Delivered |
| **Assigned To** | TBD |
| **Dependencies** | Story 1.5 (Keycloak Admin API), Camunda 8 existing integration, Story 1.3 (JWT tokens) |
| **Blocks** | Stories 1.20, 1.21, 1.24, 1.28 |

---

## User Story

**As a** developer,  
**I want** just-in-time (JIT) privilege elevation with Camunda approval workflows,  
**so that** I can request temporary elevated permissions for production debugging without permanent admin access.

---

## Business Value

Just-in-time privilege elevation addresses critical security and governance needs:

- **Principle of Least Privilege**: Developers operate with minimal access, requesting elevated permissions only when needed
- **Audit Trail**: Complete visibility into who requested elevated access, when, why, and what they did
- **Risk Reduction**: Time-bound elevated access automatically expires, reducing the risk of compromised credentials
- **Compliance**: Meets Bank of Zambia requirements for controlled administrative access
- **Zero Standing Privileges**: Eliminates permanent admin access, following zero-trust security principles

This story establishes the foundation for JIT workflows used in Stories 1.20 (MFA), 1.24 (Recertification), and 1.28 (Infrastructure Access).

---

## Acceptance Criteria

### ✅ Implementation Highlights

- Added persistence for elevation requests with manager linkage, lifecycle timestamps, and correlation identifiers.
- Introduced `/api/admin/access` endpoints for requesting, reviewing, revoking, and listing temporary privilege elevations backed by validation and audit logging.
- Integrated Keycloak role assignment, attribute stamping, and session invalidation plus periodic expiration processing to enforce JIT access windows.
- Bootstrapped a Camunda workflow client and notification hooks to align API actions with BPMN orchestration while retaining graceful fallbacks when the workflow endpoint is unavailable.

### AC1: Camunda BPMN Process Deployed
**Given** Camunda 8 is integrated with IntelliFin  
**When** deploying the elevation approval workflow  
**Then**:
- BPMN process `access-elevation-approval.bpmn` created and deployed
- Process includes: Start Event → Request Validation → Manager Approval Task → Role Assignment → End Event
- Parallel path for auto-rejection after 24 hours without approval
- Process variables: `userId`, `requestedRoles`, `justification`, `duration`, `managerId`, `approvalStatus`
- Process deployed to Camunda 8 cluster with version tracking

### AC2: Elevation Request API Implemented
**Given** Admin Service exposes JIT elevation endpoints  
**When** developer requests elevation  
**Then**:
- POST `/api/admin/access/elevate` endpoint accepts elevation requests
- Request payload validated: userId (exists), requestedRoles (valid roles), justification (min 20 chars), duration (max 8 hours)
- Camunda process instance created with unique `processInstanceId`
- Manager identified automatically (org hierarchy lookup from User.ManagerId)
- HTTP 202 Accepted response with elevation request ID and estimated approval time
- Request persisted to database with status `Pending`

### AC3: Real-Time Manager Notifications
**Given** Manager is online in Admin UI
**When** elevation request created
**Then**:
- SignalR notification sent to manager with request details *(placeholder logging implemented; SignalR wiring tracked separately)*
- Admin UI displays notification banner with "View Request" action *(UI hook pending front-end story)*
- Email notification sent to manager as fallback (if offline >5 minutes)
- Notification includes: Requester name, requested roles, justification, duration, approval deadline
- Notification deep-links to approval screen in Admin UI *(deep link provided via API response)*

### AC4: Manager Approval Interface
**Given** Manager receives elevation request notification  
**When** manager reviews request  
**Then**:
- Admin UI displays elevation request details with approve/reject actions
- Risk indicators shown: Role power level, past elevation history, current user roles
- Justification text displayed with character count (validates minimum)
- Duration slider allows manager to adjust requested duration (1-8 hours)
- Approve button triggers Camunda task completion with approved duration
- Reject button requires rejection reason (min 10 chars)

### AC5: Temporary Role Assignment in Keycloak
**Given** Manager approves elevation request  
**When** Camunda process reaches role assignment step  
**Then**:
- Keycloak Admin API called to assign requested roles to user
- Role assignment metadata stored in Keycloak: `elevationId`, `expiresAt`, `approvedBy`, `justification`
- Metadata stored in Keycloak user attributes: `jit_elevation_{elevationId}` → JSON payload
- User JWT token invalidated to force refresh (existing tokens don't get new roles)
- Elevation record in database updated: status `Active`, activatedAt timestamp
- SignalR notification sent to requester: "Access granted, roles active"

### AC6: Automatic Expiration and Revocation
**Given** Elevated access is active  
**When** scheduled job checks for expired elevations  
**Then**:
- Background job runs every 5 minutes (Hangfire recurring job)
- Query for active elevations with `expiresAt < GETUTCDATE()`
- Keycloak Admin API removes expired roles from user
- User JWT tokens invalidated to force refresh (revoke elevated permissions immediately)
- Elevation record updated: status `Expired`, expiredAt timestamp
- Audit event logged: `ElevationExpired` with duration and roles revoked
- Optional: SignalR notification to user: "Elevated access expired"

### AC7: Manual Emergency Revocation
**Given** Active elevated session exists  
**When** System Administrator clicks "Revoke Now" in Admin UI  
**Then**:
- POST `/api/admin/access/revoke/{elevationId}` endpoint called
- Keycloak Admin API removes roles immediately (no waiting for TTL)
- User JWT tokens invalidated
- Elevation record updated: status `Revoked`, revokedBy, revokedAt, revocationReason
- Audit event logged: `ElevationRevoked` with revocation details
- SignalR notification sent to user: "Elevated access revoked by administrator"

### AC8: Comprehensive Audit Trail
**Given** Elevation lifecycle events occur  
**When** audit events are logged  
**Then**:
- Audit events logged for all lifecycle events:
  - `ElevationRequested`: userId, requestedRoles, justification, duration
  - `ElevationApproved`: managerId, approvedDuration, approvalTimestamp
  - `ElevationRejected`: managerId, rejectionReason, rejectionTimestamp
  - `ElevationActivated`: roles assigned, activationTimestamp
  - `ElevationExpired`: roles revoked, expirationTimestamp
  - `ElevationRevoked`: revokedBy, revocationReason, revocationTimestamp
- All audit events include correlation ID linking elevation lifecycle
- Audit events queryable via Admin Service audit API

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Lines 1081-1105 (Story 1.19), Phase 4 Overview  
**Architecture Sections**: Section 4.3 (Keycloak Integration), Section 5 (Camunda Workflows), Section 4.1 (Admin Service)  
**Requirements**: FR7 (JIT Elevation), FR8 (Approval Workflows), NFR11 (Elevation approval <15 seconds)

### Technology Stack

- **Workflow Engine**: Camunda 8 (Zeebe)
- **Identity Provider**: Keycloak 24+ with Admin REST API
- **Real-Time**: SignalR (ASP.NET Core)
- **Background Jobs**: Hangfire
- **Database**: SQL Server 2022 (Admin Service database)
- **Notifications**: SendGrid (email) + SignalR (browser push)

### Database Schema

```sql
-- Admin Service Database

CREATE TABLE ElevationRequests (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ElevationId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    UserId NVARCHAR(100) NOT NULL,  -- Keycloak User ID
    UserName NVARCHAR(200) NOT NULL,
    RequestedRoles NVARCHAR(MAX) NOT NULL,  -- JSON array of role names
    Justification NVARCHAR(1000) NOT NULL,
    RequestedDuration INT NOT NULL,  -- Minutes (max 480 = 8 hours)
    ApprovedDuration INT NULL,  -- Minutes (manager can adjust)
    ManagerId NVARCHAR(100) NOT NULL,
    ManagerName NVARCHAR(200) NOT NULL,
    Status NVARCHAR(50) NOT NULL,  -- Pending, Approved, Rejected, Active, Expired, Revoked
    
    CamundaProcessInstanceId NVARCHAR(100),
    
    RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ApprovedAt DATETIME2 NULL,
    RejectedAt DATETIME2 NULL,
    ActivatedAt DATETIME2 NULL,
    ExpiresAt DATETIME2 NULL,
    ExpiredAt DATETIME2 NULL,
    RevokedAt DATETIME2 NULL,
    
    ApprovedBy NVARCHAR(100) NULL,  -- Manager User ID
    RejectedBy NVARCHAR(100) NULL,
    RejectionReason NVARCHAR(500) NULL,
    RevokedBy NVARCHAR(100) NULL,
    RevocationReason NVARCHAR(500) NULL,
    
    CorrelationId NVARCHAR(100),  -- For tracing across audit events
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_UserId (UserId),
    INDEX IX_Status (Status),
    INDEX IX_ExpiresAt (ExpiresAt),
    INDEX IX_RequestedAt (RequestedAt DESC),
    INDEX IX_ManagerId (ManagerId)
);

-- Trigger to update UpdatedAt automatically
CREATE TRIGGER trg_ElevationRequests_UpdatedAt
ON ElevationRequests
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE ElevationRequests
    SET UpdatedAt = GETUTCDATE()
    FROM ElevationRequests er
    INNER JOIN inserted i ON er.Id = i.Id;
END;
GO

-- View for active elevated sessions
CREATE VIEW vw_ActiveElevatedSessions AS
SELECT 
    ElevationId,
    UserId,
    UserName,
    RequestedRoles,
    Justification,
    ApprovedDuration,
    ActivatedAt,
    ExpiresAt,
    DATEDIFF(MINUTE, GETUTCDATE(), ExpiresAt) AS MinutesRemaining,
    ApprovedBy,
    ManagerName
FROM ElevationRequests
WHERE Status = 'Active'
    AND ExpiresAt > GETUTCDATE();
GO
```

### API Endpoints

#### Admin Service JIT Elevation API

```csharp
// Controllers/AccessElevationController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IntelliFin.Admin.Services;
using IntelliFin.Admin.Models;
using System.Security.Claims;

namespace IntelliFin.Admin.Controllers
{
    [ApiController]
    [Route("api/admin/access")]
    [Authorize]
    public class AccessElevationController : ControllerBase
    {
        private readonly IAccessElevationService _elevationService;
        private readonly ILogger<AccessElevationController> _logger;

        public AccessElevationController(
            IAccessElevationService elevationService,
            ILogger<AccessElevationController> logger)
        {
            _elevationService = elevationService;
            _logger = logger;
        }

        /// <summary>
        /// Request temporary privilege elevation
        /// </summary>
        [HttpPost("elevate")]
        [ProducesResponseType(typeof(ElevationRequestResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RequestElevation(
            [FromBody] ElevationRequestDto request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation(
                "Elevation request received: User={UserId}, Roles={Roles}, Duration={Duration}min",
                userId, string.Join(",", request.RequestedRoles), request.Duration);

            try
            {
                var response = await _elevationService.RequestElevationAsync(
                    userId, 
                    userName,
                    request, 
                    cancellationToken);

                return AcceptedAtAction(
                    nameof(GetElevationStatus),
                    new { elevationId = response.ElevationId },
                    response);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Message, validationErrors = ex.Errors });
            }
            catch (UnauthorizedException ex)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Get elevation request status
        /// </summary>
        [HttpGet("elevations/{elevationId}")]
        [ProducesResponseType(typeof(ElevationStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetElevationStatus(
            Guid elevationId,
            CancellationToken cancellationToken)
        {
            var status = await _elevationService.GetElevationStatusAsync(elevationId, cancellationToken);
            
            if (status == null)
                return NotFound();

            return Ok(status);
        }

        /// <summary>
        /// List all elevation requests (filtered by user or manager)
        /// </summary>
        [HttpGet("elevations")]
        [ProducesResponseType(typeof(PagedResult<ElevationSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListElevations(
            [FromQuery] string? filter = null,  // "my-requests" or "pending-approvals"
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var result = await _elevationService.ListElevationsAsync(
                userId, 
                filter, 
                page, 
                pageSize, 
                cancellationToken);

            return Ok(result);
        }

        /// <summary>
        /// Approve elevation request (Manager only)
        /// </summary>
        [HttpPost("elevations/{elevationId}/approve")]
        [Authorize(Roles = "Manager,System Administrator")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ApproveElevation(
            Guid elevationId,
            [FromBody] ElevationApprovalDto approval,
            CancellationToken cancellationToken)
        {
            var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var managerName = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation(
                "Elevation approval: ElevationId={ElevationId}, Manager={ManagerId}",
                elevationId, managerId);

            try
            {
                await _elevationService.ApproveElevationAsync(
                    elevationId,
                    managerId,
                    managerName,
                    approval.ApprovedDuration,
                    cancellationToken);

                return Ok(new { message = "Elevation approved successfully" });
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedException ex)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Reject elevation request (Manager only)
        /// </summary>
        [HttpPost("elevations/{elevationId}/reject")]
        [Authorize(Roles = "Manager,System Administrator")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RejectElevation(
            Guid elevationId,
            [FromBody] ElevationRejectionDto rejection,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(rejection.Reason) || rejection.Reason.Length < 10)
                return BadRequest(new { error = "Rejection reason must be at least 10 characters" });

            var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var managerName = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation(
                "Elevation rejection: ElevationId={ElevationId}, Manager={ManagerId}",
                elevationId, managerId);

            try
            {
                await _elevationService.RejectElevationAsync(
                    elevationId,
                    managerId,
                    managerName,
                    rejection.Reason,
                    cancellationToken);

                return Ok(new { message = "Elevation rejected successfully" });
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Revoke active elevation (Emergency use - System Administrator only)
        /// </summary>
        [HttpPost("revoke/{elevationId}")]
        [Authorize(Roles = "System Administrator")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RevokeElevation(
            Guid elevationId,
            [FromBody] ElevationRevocationDto revocation,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(revocation.Reason) || revocation.Reason.Length < 10)
                return BadRequest(new { error = "Revocation reason must be at least 10 characters" });

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var adminName = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogWarning(
                "Emergency elevation revocation: ElevationId={ElevationId}, Admin={AdminId}",
                elevationId, adminId);

            try
            {
                await _elevationService.RevokeElevationAsync(
                    elevationId,
                    adminId,
                    adminName,
                    revocation.Reason,
                    cancellationToken);

                return Ok(new { message = "Elevation revoked successfully" });
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// List active elevated sessions (System Administrator only)
        /// </summary>
        [HttpGet("elevated-sessions")]
        [Authorize(Roles = "System Administrator,Auditor")]
        [ProducesResponseType(typeof(List<ActiveSessionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListActiveSessions(CancellationToken cancellationToken)
        {
            var sessions = await _elevationService.GetActiveSessionsAsync(cancellationToken);
            return Ok(sessions);
        }
    }
}
```

### Service Implementation

```csharp
// Services/AccessElevationService.cs
using IntelliFin.Admin.Data;
using IntelliFin.Admin.Models;
using IntelliFin.Shared.Keycloak;
using IntelliFin.Shared.Camunda;
using IntelliFin.Shared.Audit;
using IntelliFin.Shared.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Admin.Services
{
    public interface IAccessElevationService
    {
        Task<ElevationRequestResponse> RequestElevationAsync(
            string userId, 
            string userName,
            ElevationRequestDto request, 
            CancellationToken cancellationToken);
        
        Task<ElevationStatusDto?> GetElevationStatusAsync(
            Guid elevationId, 
            CancellationToken cancellationToken);
        
        Task ApproveElevationAsync(
            Guid elevationId,
            string managerId,
            string managerName,
            int approvedDuration,
            CancellationToken cancellationToken);
        
        Task RejectElevationAsync(
            Guid elevationId,
            string managerId,
            string managerName,
            string reason,
            CancellationToken cancellationToken);
        
        Task RevokeElevationAsync(
            Guid elevationId,
            string adminId,
            string adminName,
            string reason,
            CancellationToken cancellationToken);
        
        Task<List<ActiveSessionDto>> GetActiveSessionsAsync(CancellationToken cancellationToken);
        
        Task<PagedResult<ElevationSummaryDto>> ListElevationsAsync(
            string userId,
            string? filter,
            int page,
            int pageSize,
            CancellationToken cancellationToken);
    }

    public class AccessElevationService : IAccessElevationService
    {
        private readonly AdminDbContext _dbContext;
        private readonly IKeycloakAdminClient _keycloakClient;
        private readonly ICamundaClient _camundaClient;
        private readonly IAuditService _auditService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<AccessElevationService> _logger;

        public AccessElevationService(
            AdminDbContext dbContext,
            IKeycloakAdminClient keycloakClient,
            ICamundaClient camundaClient,
            IAuditService auditService,
            INotificationService notificationService,
            ILogger<AccessElevationService> logger)
        {
            _dbContext = dbContext;
            _keycloakClient = keycloakClient;
            _camundaClient = camundaClient;
            _auditService = auditService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<ElevationRequestResponse> RequestElevationAsync(
            string userId,
            string userName,
            ElevationRequestDto request,
            CancellationToken cancellationToken)
        {
            // Validation
            if (request.Duration <= 0 || request.Duration > 480)
                throw new ValidationException("Duration must be between 1 and 480 minutes (8 hours)");

            if (string.IsNullOrWhiteSpace(request.Justification) || request.Justification.Length < 20)
                throw new ValidationException("Justification must be at least 20 characters");

            if (request.RequestedRoles == null || !request.RequestedRoles.Any())
                throw new ValidationException("At least one role must be requested");

            // Verify requested roles exist in Keycloak
            var availableRoles = await _keycloakClient.GetRealmRolesAsync(cancellationToken);
            var invalidRoles = request.RequestedRoles.Except(availableRoles.Select(r => r.Name)).ToList();
            if (invalidRoles.Any())
                throw new ValidationException($"Invalid roles requested: {string.Join(", ", invalidRoles)}");

            // Get user's manager from organizational hierarchy
            var manager = await GetUserManagerAsync(userId, cancellationToken);
            if (manager == null)
                throw new ValidationException("Cannot determine your manager. Please contact System Administrator.");

            var correlationId = Guid.NewGuid().ToString("N");
            var elevationId = Guid.NewGuid();

            // Create elevation request record
            var elevationRequest = new ElevationRequest
            {
                ElevationId = elevationId,
                UserId = userId,
                UserName = userName,
                RequestedRoles = System.Text.Json.JsonSerializer.Serialize(request.RequestedRoles),
                Justification = request.Justification,
                RequestedDuration = request.Duration,
                ManagerId = manager.UserId,
                ManagerName = manager.UserName,
                Status = "Pending",
                CorrelationId = correlationId,
                RequestedAt = DateTime.UtcNow
            };

            _dbContext.ElevationRequests.Add(elevationRequest);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Start Camunda workflow
            var processInstanceId = await _camundaClient.StartProcessAsync(
                "access-elevation-approval",
                new Dictionary<string, object>
                {
                    { "elevationId", elevationId.ToString() },
                    { "userId", userId },
                    { "userName", userName },
                    { "requestedRoles", request.RequestedRoles },
                    { "justification", request.Justification },
                    { "duration", request.Duration },
                    { "managerId", manager.UserId },
                    { "managerName", manager.UserName },
                    { "correlationId", correlationId }
                },
                cancellationToken);

            elevationRequest.CamundaProcessInstanceId = processInstanceId;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Send manager notification
            await _notificationService.SendElevationRequestNotificationAsync(
                manager.UserId,
                new ElevationNotificationDto
                {
                    ElevationId = elevationId,
                    RequesterName = userName,
                    RequestedRoles = request.RequestedRoles,
                    Justification = request.Justification,
                    Duration = request.Duration,
                    RequestedAt = DateTime.UtcNow
                },
                cancellationToken);

            // Audit log
            await _auditService.LogAsync(new AuditEvent
            {
                Actor = userId,
                Action = "ElevationRequested",
                EntityType = "ElevationRequest",
                EntityId = elevationId.ToString(),
                CorrelationId = correlationId,
                EventData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    requestedRoles = request.RequestedRoles,
                    duration = request.Duration,
                    justification = request.Justification,
                    managerId = manager.UserId
                })
            }, cancellationToken);

            _logger.LogInformation(
                "Elevation request created: ElevationId={ElevationId}, User={UserId}, Manager={ManagerId}",
                elevationId, userId, manager.UserId);

            return new ElevationRequestResponse
            {
                ElevationId = elevationId,
                Status = "Pending",
                Message = "Elevation request submitted successfully. Awaiting manager approval.",
                EstimatedApprovalTime = DateTime.UtcNow.AddHours(4)  // SLA estimate
            };
        }

        public async Task ApproveElevationAsync(
            Guid elevationId,
            string managerId,
            string managerName,
            int approvedDuration,
            CancellationToken cancellationToken)
        {
            var elevation = await _dbContext.ElevationRequests
                .FirstOrDefaultAsync(e => e.ElevationId == elevationId, cancellationToken);

            if (elevation == null)
                throw new NotFoundException("Elevation request not found");

            if (elevation.ManagerId != managerId)
                throw new UnauthorizedException("You are not authorized to approve this request");

            if (elevation.Status != "Pending")
                throw new InvalidOperationException($"Elevation request is not pending (current status: {elevation.Status})");

            // Validate approved duration
            if (approvedDuration <= 0 || approvedDuration > Math.Min(elevation.RequestedDuration, 480))
                throw new ValidationException("Approved duration exceeds requested duration or maximum (8 hours)");

            // Update elevation record
            elevation.Status = "Approved";
            elevation.ApprovedBy = managerId;
            elevation.ApprovedDuration = approvedDuration;
            elevation.ApprovedAt = DateTime.UtcNow;
            elevation.ExpiresAt = DateTime.UtcNow.AddMinutes(approvedDuration);

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Complete Camunda approval task
            await _camundaClient.CompleteTaskAsync(
                elevation.CamundaProcessInstanceId!,
                "manager-approval",
                new Dictionary<string, object>
                {
                    { "approved", true },
                    { "approvedDuration", approvedDuration },
                    { "approvedBy", managerId }
                },
                cancellationToken);

            // Assign roles in Keycloak
            var requestedRoles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(elevation.RequestedRoles)!;
            await AssignRolesWithMetadataAsync(elevation, requestedRoles, cancellationToken);

            // Update status to Active
            elevation.Status = "Active";
            elevation.ActivatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Invalidate user's JWT to force token refresh
            await _keycloakClient.InvalidateUserSessionsAsync(elevation.UserId, cancellationToken);

            // Send notification to requester
            await _notificationService.SendElevationApprovedNotificationAsync(
                elevation.UserId,
                new ElevationApprovedDto
                {
                    ElevationId = elevationId,
                    ApprovedRoles = requestedRoles,
                    ApprovedDuration = approvedDuration,
                    ExpiresAt = elevation.ExpiresAt.Value,
                    ApprovedBy = managerName
                },
                cancellationToken);

            // Audit logs
            await _auditService.LogAsync(new AuditEvent
            {
                Actor = managerId,
                Action = "ElevationApproved",
                EntityType = "ElevationRequest",
                EntityId = elevationId.ToString(),
                CorrelationId = elevation.CorrelationId,
                EventData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    approvedDuration = approvedDuration,
                    expiresAt = elevation.ExpiresAt
                })
            }, cancellationToken);

            await _auditService.LogAsync(new AuditEvent
            {
                Actor = elevation.UserId,
                Action = "ElevationActivated",
                EntityType = "ElevationRequest",
                EntityId = elevationId.ToString(),
                CorrelationId = elevation.CorrelationId,
                EventData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    roles = requestedRoles,
                    expiresAt = elevation.ExpiresAt
                })
            }, cancellationToken);

            _logger.LogInformation(
                "Elevation approved and activated: ElevationId={ElevationId}, Duration={Duration}min",
                elevationId, approvedDuration);
        }

        public async Task RevokeElevationAsync(
            Guid elevationId,
            string adminId,
            string adminName,
            string reason,
            CancellationToken cancellationToken)
        {
            var elevation = await _dbContext.ElevationRequests
                .FirstOrDefaultAsync(e => e.ElevationId == elevationId, cancellationToken);

            if (elevation == null)
                throw new NotFoundException("Elevation request not found");

            if (elevation.Status != "Active")
                throw new InvalidOperationException($"Elevation is not active (current status: {elevation.Status})");

            // Remove roles from Keycloak
            var requestedRoles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(elevation.RequestedRoles)!;
            await RemoveRolesAndMetadataAsync(elevation, requestedRoles, cancellationToken);

            // Update elevation record
            elevation.Status = "Revoked";
            elevation.RevokedBy = adminId;
            elevation.RevokedAt = DateTime.UtcNow;
            elevation.RevocationReason = reason;

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Invalidate user's JWT
            await _keycloakClient.InvalidateUserSessionsAsync(elevation.UserId, cancellationToken);

            // Send notification to user
            await _notificationService.SendElevationRevokedNotificationAsync(
                elevation.UserId,
                new ElevationRevokedDto
                {
                    ElevationId = elevationId,
                    Reason = reason,
                    RevokedBy = adminName,
                    RevokedAt = DateTime.UtcNow
                },
                cancellationToken);

            // Audit log
            await _auditService.LogAsync(new AuditEvent
            {
                Actor = adminId,
                Action = "ElevationRevoked",
                EntityType = "ElevationRequest",
                EntityId = elevationId.ToString(),
                CorrelationId = elevation.CorrelationId,
                EventData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    reason = reason,
                    revokedBy = adminId,
                    roles = requestedRoles
                })
            }, cancellationToken);

            _logger.LogWarning(
                "Elevation revoked: ElevationId={ElevationId}, Admin={AdminId}, Reason={Reason}",
                elevationId, adminId, reason);
        }

        public async Task<List<ActiveSessionDto>> GetActiveSessionsAsync(CancellationToken cancellationToken)
        {
            var sessions = await _dbContext.ElevationRequests
                .Where(e => e.Status == "Active" && e.ExpiresAt > DateTime.UtcNow)
                .OrderBy(e => e.ExpiresAt)
                .Select(e => new ActiveSessionDto
                {
                    ElevationId = e.ElevationId,
                    UserId = e.UserId,
                    UserName = e.UserName,
                    RequestedRoles = e.RequestedRoles,
                    Justification = e.Justification,
                    ApprovedDuration = e.ApprovedDuration!.Value,
                    ActivatedAt = e.ActivatedAt!.Value,
                    ExpiresAt = e.ExpiresAt!.Value,
                    ApprovedBy = e.ApprovedBy!,
                    ManagerName = e.ManagerName
                })
                .ToListAsync(cancellationToken);

            return sessions;
        }

        private async Task AssignRolesWithMetadataAsync(
            ElevationRequest elevation,
            List<string> roles,
            CancellationToken cancellationToken)
        {
            // Assign roles via Keycloak Admin API
            await _keycloakClient.AssignRolesToUserAsync(elevation.UserId, roles, cancellationToken);

            // Store elevation metadata in Keycloak user attributes
            var metadata = new
            {
                elevationId = elevation.ElevationId,
                expiresAt = elevation.ExpiresAt,
                approvedBy = elevation.ApprovedBy,
                justification = elevation.Justification,
                roles = roles
            };

            await _keycloakClient.SetUserAttributeAsync(
                elevation.UserId,
                $"jit_elevation_{elevation.ElevationId}",
                System.Text.Json.JsonSerializer.Serialize(metadata),
                cancellationToken);
        }

        private async Task RemoveRolesAndMetadataAsync(
            ElevationRequest elevation,
            List<string> roles,
            CancellationToken cancellationToken)
        {
            // Remove roles via Keycloak Admin API
            await _keycloakClient.RemoveRolesFromUserAsync(elevation.UserId, roles, cancellationToken);

            // Remove elevation metadata from Keycloak
            await _keycloakClient.RemoveUserAttributeAsync(
                elevation.UserId,
                $"jit_elevation_{elevation.ElevationId}",
                cancellationToken);
        }

        private async Task<(string UserId, string UserName)?> GetUserManagerAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            // Query organizational hierarchy (assumes Users table has ManagerId)
            // This could also be stored in Keycloak attributes or separate org service
            var user = await _dbContext.Users
                .Include(u => u.Manager)
                .FirstOrDefaultAsync(u => u.KeycloakId == userId, cancellationToken);

            if (user?.Manager == null)
                return null;

            return (user.Manager.KeycloakId, user.Manager.FullName);
        }

        // Additional methods omitted for brevity (GetElevationStatusAsync, RejectElevationAsync, ListElevationsAsync)
    }
}
```

### Background Job - Automatic Expiration

```csharp
// Jobs/ElevationExpirationJob.cs
using Hangfire;
using IntelliFin.Admin.Data;
using IntelliFin.Admin.Services;
using IntelliFin.Shared.Keycloak;
using IntelliFin.Shared.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Admin.Jobs
{
    public class ElevationExpirationJob
    {
        private readonly AdminDbContext _dbContext;
        private readonly IKeycloakAdminClient _keycloakClient;
        private readonly IAuditService _auditService;
        private readonly ILogger<ElevationExpirationJob> _logger;

        public ElevationExpirationJob(
            AdminDbContext dbContext,
            IKeycloakAdminClient keycloakClient,
            IAuditService auditService,
            ILogger<ElevationExpirationJob> logger)
        {
            _dbContext = dbContext;
            _keycloakClient = keycloakClient;
            _auditService = auditService;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task CheckExpiredElevationsAsync()
        {
            _logger.LogInformation("Checking for expired elevations...");

            var expiredElevations = await _dbContext.ElevationRequests
                .Where(e => e.Status == "Active" && e.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync();

            if (!expiredElevations.Any())
            {
                _logger.LogInformation("No expired elevations found");
                return;
            }

            _logger.LogInformation("Found {Count} expired elevations", expiredElevations.Count);

            foreach (var elevation in expiredElevations)
            {
                try
                {
                    var requestedRoles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(elevation.RequestedRoles)!;

                    // Remove roles from Keycloak
                    await _keycloakClient.RemoveRolesFromUserAsync(elevation.UserId, requestedRoles, default);

                    // Remove metadata
                    await _keycloakClient.RemoveUserAttributeAsync(
                        elevation.UserId,
                        $"jit_elevation_{elevation.ElevationId}",
                        default);

                    // Invalidate user sessions
                    await _keycloakClient.InvalidateUserSessionsAsync(elevation.UserId, default);

                    // Update elevation record
                    elevation.Status = "Expired";
                    elevation.ExpiredAt = DateTime.UtcNow;

                    // Audit log
                    await _auditService.LogAsync(new AuditEvent
                    {
                        Actor = "SYSTEM",
                        Action = "ElevationExpired",
                        EntityType = "ElevationRequest",
                        EntityId = elevation.ElevationId.ToString(),
                        CorrelationId = elevation.CorrelationId,
                        EventData = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            userId = elevation.UserId,
                            roles = requestedRoles,
                            duration = elevation.ApprovedDuration,
                            expiredAt = DateTime.UtcNow
                        })
                    }, default);

                    _logger.LogInformation(
                        "Expired elevation processed: ElevationId={ElevationId}, User={UserId}",
                        elevation.ElevationId, elevation.UserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to expire elevation: ElevationId={ElevationId}",
                        elevation.ElevationId);
                    // Continue processing other elevations
                }
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Elevation expiration check complete");
        }
    }

    // Register recurring job in Startup.cs
    public static class ElevationExpirationJobExtensions
    {
        public static void RegisterElevationExpirationJob(this IServiceProvider services)
        {
            RecurringJob.AddOrUpdate<ElevationExpirationJob>(
                "elevation-expiration-check",
                job => job.CheckExpiredElevationsAsync(),
                "*/5 * * * *");  // Every 5 minutes
        }
    }
}
```

### Camunda BPMN Process

```xml
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:zeebe="http://camunda.org/schema/zeebe/1.0"
                  id="access-elevation-approval"
                  targetNamespace="http://intellifin.local/bpmn">
  
  <bpmn:process id="access-elevation-approval" name="Access Elevation Approval" isExecutable="true">
    
    <!-- Start Event -->
    <bpmn:startEvent id="StartEvent_ElevationRequest" name="Elevation Requested">
      <bpmn:outgoing>Flow_ToValidation</bpmn:outgoing>
    </bpmn:startEvent>
    
    <!-- Validation Service Task -->
    <bpmn:serviceTask id="Task_ValidateRequest" name="Validate Request">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="validate-elevation-request" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToValidation</bpmn:incoming>
      <bpmn:outgoing>Flow_ToManagerApproval</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Manager Approval User Task -->
    <bpmn:userTask id="Task_ManagerApproval" name="Manager Approval">
      <bpmn:extensionElements>
        <zeebe:assignmentDefinition assignee="${managerId}" />
        <zeebe:taskHeaders>
          <zeebe:header key="priority" value="high" />
        </zeebe:taskHeaders>
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToManagerApproval</bpmn:incoming>
      <bpmn:outgoing>Flow_ToGateway</bpmn:outgoing>
    </bpmn:userTask>
    
    <!-- Exclusive Gateway - Approved or Rejected -->
    <bpmn:exclusiveGateway id="Gateway_ApprovalDecision" name="Approved?">
      <bpmn:incoming>Flow_ToGateway</bpmn:incoming>
      <bpmn:outgoing>Flow_ToRoleAssignment</bpmn:outgoing>
      <bpmn:outgoing>Flow_ToRejection</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    
    <!-- Role Assignment Service Task -->
    <bpmn:serviceTask id="Task_AssignRoles" name="Assign Roles in Keycloak">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="assign-elevated-roles" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToRoleAssignment</bpmn:incoming>
      <bpmn:outgoing>Flow_ToNotifyApproved</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Notify User - Approved -->
    <bpmn:serviceTask id="Task_NotifyApproved" name="Notify User (Approved)">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="notify-elevation-approved" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToNotifyApproved</bpmn:incoming>
      <bpmn:outgoing>Flow_ToEndApproved</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Notify User - Rejected -->
    <bpmn:serviceTask id="Task_NotifyRejected" name="Notify User (Rejected)">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="notify-elevation-rejected" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToRejection</bpmn:incoming>
      <bpmn:outgoing>Flow_ToEndRejected</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- End Events -->
    <bpmn:endEvent id="EndEvent_Approved" name="Elevation Activated">
      <bpmn:incoming>Flow_ToEndApproved</bpmn:incoming>
    </bpmn:endEvent>
    
    <bpmn:endEvent id="EndEvent_Rejected" name="Elevation Rejected">
      <bpmn:incoming>Flow_ToEndRejected</bpmn:incoming>
    </bpmn:endEvent>
    
    <!-- Sequence Flows -->
    <bpmn:sequenceFlow id="Flow_ToValidation" sourceRef="StartEvent_ElevationRequest" targetRef="Task_ValidateRequest" />
    <bpmn:sequenceFlow id="Flow_ToManagerApproval" sourceRef="Task_ValidateRequest" targetRef="Task_ManagerApproval" />
    <bpmn:sequenceFlow id="Flow_ToGateway" sourceRef="Task_ManagerApproval" targetRef="Gateway_ApprovalDecision" />
    
    <bpmn:sequenceFlow id="Flow_ToRoleAssignment" sourceRef="Gateway_ApprovalDecision" targetRef="Task_AssignRoles">
      <bpmn:conditionExpression>${approved == true}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    
    <bpmn:sequenceFlow id="Flow_ToRejection" sourceRef="Gateway_ApprovalDecision" targetRef="Task_NotifyRejected">
      <bpmn:conditionExpression>${approved == false}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    
    <bpmn:sequenceFlow id="Flow_ToNotifyApproved" sourceRef="Task_AssignRoles" targetRef="Task_NotifyApproved" />
    <bpmn:sequenceFlow id="Flow_ToEndApproved" sourceRef="Task_NotifyApproved" targetRef="EndEvent_Approved" />
    <bpmn:sequenceFlow id="Flow_ToEndRejected" sourceRef="Task_NotifyRejected" targetRef="EndEvent_Rejected" />
    
    <!-- Boundary Event - Timeout (24 hours) -->
    <bpmn:boundaryEvent id="BoundaryEvent_Timeout" name="24h Timeout" attachedToRef="Task_ManagerApproval">
      <bpmn:outgoing>Flow_ToAutoReject</bpmn:outgoing>
      <bpmn:timerEventDefinition>
        <bpmn:timeDuration>PT24H</bpmn:timeDuration>
      </bpmn:timerEventDefinition>
    </bpmn:boundaryEvent>
    
    <bpmn:sequenceFlow id="Flow_ToAutoReject" sourceRef="BoundaryEvent_Timeout" targetRef="Task_NotifyRejected" />
    
  </bpmn:process>
</bpmn:definitions>
```

### Configuration

```json
// appsettings.json - Admin Service
{
  "ElevationSettings": {
    "MaxDurationMinutes": 480,
    "MinJustificationLength": 20,
    "ApprovalTimeoutHours": 24,
    "ExpirationCheckIntervalMinutes": 5,
    "NotifyUserOnExpiration": true
  },
  "CamundaSettings": {
    "ZeebeGatewayAddress": "zeebe-gateway.camunda.svc.cluster.local:26500",
    "ClientId": "admin-service",
    "ClientSecret": "${VAULT_CAMUNDA_CLIENT_SECRET}"
  },
  "KeycloakSettings": {
    "AdminApiUrl": "https://keycloak.intellifin.local/admin/realms/IntelliFin",
    "ClientId": "admin-service",
    "ClientSecret": "${VAULT_KEYCLOAK_ADMIN_SECRET}"
  }
}
```

---

## Integration Verification

### IV1: Existing Permanent Role Assignments Unaffected
**Verification Steps**:
1. Query Keycloak for users with permanent role assignments
2. Request JIT elevation for user with existing permanent roles
3. Verify permanent roles remain after elevation expires
4. Confirm only JIT-assigned roles are removed on expiration

**Success Criteria**:
- Permanent role assignments unchanged
- JIT roles additive to existing permissions
- Token refresh includes both permanent and JIT roles

### IV2: User JWT Tokens Refreshed Automatically After Elevation Approval
**Verification Steps**:
1. User logs in with base permissions (capture JWT)
2. Request elevation, get approval
3. Force token refresh (wait for TTL or call refresh endpoint)
4. Decode new JWT and verify elevated roles present in `realm_access.roles` claim

**Success Criteria**:
- New JWT includes elevated roles within 15 seconds of approval
- Old JWT still valid until TTL expiry (no immediate invalidation unless configured)
- Token refresh endpoint returns elevated permissions

### IV3: Performance Test - Elevation Approval to Activation <15 Seconds
**Verification Steps**:
1. Submit elevation request (start timer)
2. Manager approves via API (simulated)
3. Measure time until:
   - Keycloak roles assigned
   - Database status = "Active"
   - User notification sent
4. Stop timer

**Success Criteria**:
- p95 latency < 15 seconds (NFR11)
- p50 latency < 8 seconds
- Zero errors during 100 concurrent elevation requests

---

## Testing Strategy

### Unit Tests

#### Test: ElevationRequestValidation
```csharp
[Fact]
public async Task RequestElevation_InvalidDuration_ThrowsValidationException()
{
    // Arrange
    var service = CreateService();
    var request = new ElevationRequestDto
    {
        RequestedRoles = new[] { "Admin" },
        Justification = "Need access for urgent production fix",
        Duration = 500  // Exceeds max 480 minutes
    };

    // Act & Assert
    await Assert.ThrowsAsync<ValidationException>(
        () => service.RequestElevationAsync("user123", "John Doe", request, CancellationToken.None));
}

[Fact]
public async Task RequestElevation_ShortJustification_ThrowsValidationException()
{
    // Arrange
    var service = CreateService();
    var request = new ElevationRequestDto
    {
        RequestedRoles = new[] { "Admin" },
        Justification = "Quick fix",  // Too short
        Duration = 60
    };

    // Act & Assert
    await Assert.ThrowsAsync<ValidationException>(
        () => service.RequestElevationAsync("user123", "John Doe", request, CancellationToken.None));
}
```

#### Test: AutomaticExpiration
```csharp
[Fact]
public async Task ExpirationJob_ExpiredElevation_RemovesRoles()
{
    // Arrange
    var dbContext = CreateInMemoryDbContext();
    var keycloakMock = new Mock<IKeycloakAdminClient>();
    var job = new ElevationExpirationJob(dbContext, keycloakMock.Object, /* ... */);

    var elevation = new ElevationRequest
    {
        ElevationId = Guid.NewGuid(),
        UserId = "user123",
        Status = "Active",
        RequestedRoles = "[\"Admin\",\"Auditor\"]",
        ExpiresAt = DateTime.UtcNow.AddMinutes(-5)  // Expired 5 minutes ago
    };
    dbContext.ElevationRequests.Add(elevation);
    await dbContext.SaveChangesAsync();

    // Act
    await job.CheckExpiredElevationsAsync();

    // Assert
    var updated = await dbContext.ElevationRequests.FindAsync(elevation.Id);
    Assert.Equal("Expired", updated.Status);
    Assert.NotNull(updated.ExpiredAt);
    
    keycloakMock.Verify(k => k.RemoveRolesFromUserAsync(
        "user123",
        It.Is<List<string>>(roles => roles.Contains("Admin") && roles.Contains("Auditor")),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

### Integration Tests

#### Test: End-to-End Elevation Workflow
```csharp
[Fact]
public async Task ElevationWorkflow_RequestApprovalActivation_Success()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>();
    var client = factory.CreateClient();
    
    // Act 1: Request elevation
    var requestPayload = new
    {
        requestedRoles = new[] { "System Administrator" },
        justification = "Need to investigate production database deadlock issue affecting loan approvals",
        duration = 120
    };
    var requestResponse = await client.PostAsJsonAsync("/api/admin/access/elevate", requestPayload);
    requestResponse.EnsureSuccessStatusCode();
    var elevationResponse = await requestResponse.Content.ReadFromJsonAsync<ElevationRequestResponse>();

    // Act 2: Manager approves (simulate)
    var approvalPayload = new { approvedDuration = 120 };
    var approvalResponse = await client.PostAsJsonAsync(
        $"/api/admin/access/elevations/{elevationResponse.ElevationId}/approve",
        approvalPayload);
    approvalResponse.EnsureSuccessStatusCode();

    // Act 3: Check status
    var statusResponse = await client.GetAsync($"/api/admin/access/elevations/{elevationResponse.ElevationId}");
    var status = await statusResponse.Content.ReadFromJsonAsync<ElevationStatusDto>();

    // Assert
    Assert.Equal("Active", status.Status);
    Assert.NotNull(status.ActivatedAt);
    Assert.True(status.ExpiresAt > DateTime.UtcNow);
}
```

### Performance Tests

#### Test: Concurrent Elevation Requests
```csharp
[Fact]
public async Task ElevationService_100ConcurrentRequests_PerformanceAcceptable()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>();
    var client = factory.CreateClient();
    var stopwatch = Stopwatch.StartNew();

    // Act
    var tasks = Enumerable.Range(1, 100).Select(async i =>
    {
        var payload = new
        {
            requestedRoles = new[] { "Developer" },
            justification = $"Concurrent test request {i} - investigating production issue",
            duration = 60
        };
        var response = await client.PostAsJsonAsync("/api/admin/access/elevate", payload);
        return response.IsSuccessStatusCode;
    });

    var results = await Task.WhenAll(tasks);
    stopwatch.Stop();

    // Assert
    Assert.True(results.All(r => r), "All requests should succeed");
    Assert.True(stopwatch.ElapsedMilliseconds < 30000, "100 requests should complete in <30 seconds");
}
```

### Security Tests

#### Test: Unauthorized Approval Attempt
```csharp
[Fact]
public async Task ApproveElevation_NonManager_ReturnsForbidden()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>();
    var client = factory.CreateClient();
    var elevationId = Guid.NewGuid();

    // Act (user is not the manager for this elevation)
    var approvalPayload = new { approvedDuration = 60 };
    var response = await client.PostAsJsonAsync(
        $"/api/admin/access/elevations/{elevationId}/approve",
        approvalPayload);

    // Assert
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| Keycloak session invalidation lag | Users keep elevated access in cached tokens | Medium | Force token refresh via Keycloak session invalidation API. Client apps refresh tokens proactively. |
| Camunda workflow failures | Elevation requests stuck in pending | Low | Implement timeout boundary event (24h auto-reject). Add dead-letter queue monitoring. |
| Manager unavailable | Elevation requests not approved timely | Medium | Escalation workflow: Auto-assign to manager's manager after 12 hours. Email + SMS notifications. |
| Expiration job downtime | Elevated access persists beyond TTL | Low | Idempotent job design. Run job on multiple Admin Service replicas (distributed lock via database). Monitor job execution health. |
| Abuse of elevation requests | Users request unnecessary elevated access | Medium | Rate limiting: Max 3 elevation requests per day per user. Audit flagging for suspicious patterns (e.g., same justification text). |

---

## Definition of Done

- [ ] Database schema created with indexes and triggers
- [ ] API endpoints implemented with full validation
- [ ] Camunda BPMN process deployed and tested
- [ ] Keycloak role assignment/removal working correctly
- [ ] SignalR real-time notifications functional
- [ ] Background expiration job scheduled and tested
- [ ] Unit tests: >85% code coverage
- [ ] Integration tests: All API workflows pass
- [ ] Performance test: Approval-to-activation <15 seconds (NFR11)
- [ ] Security review: Authorization checks verified
- [ ] Audit events logged for all lifecycle stages
- [ ] Admin UI integration complete (manager approval screen, active sessions list)
- [ ] Documentation updated: API specs, runbook for troubleshooting
- [ ] Load testing: 100 concurrent requests handled successfully
- [ ] Monitoring alerts configured: Elevation failures, expiration job health

---

## Related Documentation

### PRD References
- **Lines 1081-1105**: Story 1.19 detailed requirements
- **Lines 1079-1243**: Phase 4 (Governance & Workflows) overview
- **Lines 1554-1570**: Technology stack decisions (Camunda, Keycloak)

### Architecture References
- **Section 4.2**: Keycloak Identity Provider Integration
- **Section 4.3**: Keycloak Admin API usage patterns
- **Section 5**: Camunda Workflow Integration
- **Section 4.1**: Admin Service architecture

### External Documentation
- [Keycloak Admin REST API](https://www.keycloak.org/docs-api/24.0.0/rest-api/index.html)
- [Camunda 8 BPMN Reference](https://docs.camunda.io/docs/components/modeler/bpmn/)
- [SignalR Documentation](https://learn.microsoft.com/en-us/aspnet/core/signalr/)
- [Hangfire Documentation](https://docs.hangfire.io/)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Verify Keycloak Admin API credentials in Vault
- [ ] Confirm Camunda 8 cluster connectivity
- [ ] Review existing organizational hierarchy data (Users.ManagerId)
- [ ] Test SignalR connection from Admin UI to backend
- [ ] Verify Hangfire dashboard accessible and configured
- [ ] Review rate limiting strategy with Security team
- [ ] Coordinate with Frontend team on Admin UI mockups

### Post-Implementation Handoff
- [ ] Demo elevation workflow to Product Owner
- [ ] Train System Administrators on manual revocation procedure
- [ ] Document escalation paths for stuck elevation requests
- [ ] Set up monitoring dashboards for elevation metrics (request rate, approval SLA, expiration job health)
- [ ] Schedule knowledge transfer session with DevOps team
- [ ] Add elevation request metrics to weekly operations report
- [ ] Review audit trail with Compliance Officer

### Technical Debt / Future Enhancements
- [ ] Consider multi-level approval for highly privileged roles (e.g., CEO approval required for Database Admin role)
- [ ] Implement mobile app notifications (push notifications via FCM/APNS)
- [ ] Add AI-powered justification analysis (flag suspicious or repetitive requests)
- [ ] Create self-service portal for viewing personal elevation history
- [ ] Integrate with SIEM for real-time anomaly detection

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: [Story 1.20: Step-Up MFA Integration](./story-1.20-step-up-mfa.md)
