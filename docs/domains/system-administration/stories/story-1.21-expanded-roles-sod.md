# Story 1.21: Expanded Operational Roles and SoD Enforcement

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.21 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 4: Governance & Workflows |
| **Sprint** | Sprint 7 |
| **Story Points** | 13 |
| **Estimated Effort** | 7-10 days |
| **Priority** | P0 (Critical for compliance) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Story 1.5 (Keycloak Admin API), Story 1.19 (Camunda workflows for exceptions) |
| **Blocks** | Story 1.24 (Access Recertification) |

---

## User Story

**As a** Compliance Officer,  
**I want** expanded RBAC roles with Segregation of Duties (SoD) enforcement,  
**so that** we prevent conflicting role assignments and meet BoZ compliance requirements.

---

## Business Value

Expanded roles with SoD enforcement addresses critical compliance and security needs:

- **Regulatory Compliance**: Meets Bank of Zambia requirements for role segregation and dual authorization
- **Fraud Prevention**: Prevents single users from having conflicting roles that enable fraud (e.g., creating and approving loans)
- **Audit Trail**: Complete visibility into role assignments, SoD violations, and approved exceptions
- **Operational Clarity**: Clear role definitions aligned with actual job functions in Zambian microfinance
- **Risk Management**: Reduces insider threat risk through enforced separation of duties

This story establishes the foundational governance controls for role-based access management.

---

## Acceptance Criteria

### AC1: New Keycloak Realm Roles Created
**Given** Keycloak IntelliFin realm is operational  
**When** creating expanded role set  
**Then**:
- New realm roles created in Keycloak:
  - **Collections Officer**: Manages overdue loans, initiates collections actions
  - **Compliance Officer**: Reviews compliance reports, approves SoD exceptions
  - **Treasury Officer**: Manages disbursements, reconciles accounts
  - **GL Accountant**: Records journal entries, generates financial reports
  - **Auditor**: Read-only access to all audit logs and financial records
  - **Risk Manager**: Reviews risk metrics, approves high-risk loans
  - **Branch Manager**: Supervises branch operations, approves branch-level transactions
- Existing V1 roles preserved: Loan Officer, Loan Processor, Loan Approver, System Administrator, CEO, Credit Analyst
- Role descriptions documented in Keycloak role metadata

### AC2: Role Hierarchy Configured
**Given** Roles are created in Keycloak  
**When** configuring role hierarchy  
**Then**:
- Hierarchical role structure defined:
  - **CEO**: Inherits all roles (highest authority)
  - **Branch Manager**: Inherits Collections Officer, Loan Officer, Loan Processor permissions (branch-level)
  - **Compliance Officer**: Inherits Auditor permissions (read-only compliance view)
  - **System Administrator**: Technical admin, no business role inheritance
- Composite roles configured in Keycloak
- Role hierarchy queryable via Admin API
- JWT tokens include inherited roles in `realm_access.roles` claim

### AC3: SoD Policy Matrix Defined
**Given** SoD compliance requirements are known  
**When** defining SoD policy matrix  
**Then**:
- SoD policy matrix stored in database with conflicting role pairs:
  - **Loan Processor ↔ Loan Approver**: Cannot create and approve own loans
  - **Treasury Officer ↔ GL Accountant**: Cannot disburse and record same transaction
  - **Collections Officer ↔ Loan Officer**: Cannot originate and collect same loans
  - **Loan Officer ↔ Auditor**: Cannot create loans and audit own work
  - **System Administrator ↔ CEO**: Technical vs. business role separation
- SoD severity levels: Critical (hard block), High (requires exception), Medium (warning only)
- SoD policy configurable per role pair (enabled/disabled, severity level)

### AC4: Role Assignment Validation with SoD Check
**Given** Admin assigns role to user  
**When** validating role assignment  
**Then**:
- POST `/api/admin/users/{userId}/roles` endpoint validates SoD policy before assignment
- SoD validation checks user's existing roles against new role assignment
- If SoD conflict detected (Critical severity):
  - Return HTTP 409 Conflict
  - Response includes: conflicting role pair, SoD policy details, exception request URL
  - Role assignment blocked
- If SoD conflict detected (High severity):
  - Return HTTP 202 Accepted with warning
  - Response includes: warning message, requires explicit confirmation
  - Admin must re-submit with `confirmedSodOverride: true` flag
- SoD validation logged in audit trail

### AC5: SoD Exception Workflow
**Given** SoD conflict requires business exception  
**When** requesting SoD exception  
**Then**:
- Camunda BPMN process `sod-exception-request.bpmn` created and deployed
- POST `/api/admin/sod/exception-request` endpoint triggers exception workflow
- Exception request includes: userId, requestedRole, conflictingRoles, businessJustification, exceptionDuration (max 90 days)
- Compliance Officer receives notification for review
- Compliance Officer can approve/reject via Admin UI
- Upon approval, role assignment created with exception metadata:
  - `sodExceptionId`, `approvedBy`, `expiresAt`, `justification`
  - Exception stored in database and Keycloak user attributes
- Exception expiry handled by scheduled job (auto-revoke after expiration)

### AC6: Admin UI SoD Conflict Warnings
**Given** Admin UI displays role assignment interface  
**When** admin selects role to assign  
**Then**:
- UI calls `/api/admin/sod/validate` endpoint with userId and proposed role
- If SoD conflict detected:
  - Warning banner displayed in UI with conflict details
  - "Request Exception" button shown (triggers exception workflow)
  - "Cancel" button abandons role assignment
- If no conflict:
  - Role assignment proceeds normally
- SoD conflict indicator shown next to user's existing roles (visual cue)

### AC7: Quarterly SoD Compliance Report
**Given** Compliance Officer needs SoD compliance visibility  
**When** generating SoD compliance report  
**Then**:
- GET `/api/admin/sod/compliance-report` endpoint generates report
- Report includes:
  - Total active role assignments
  - Active SoD exceptions (count, list with expiry dates)
  - Expired exceptions (auto-revoked count)
  - SoD violations detected and blocked (count, trend)
  - Users with most role assignments (potential over-privilege)
- Report exportable as PDF and CSV
- Report scheduled to generate quarterly and email to Compliance Officer
- Report data queryable via Admin UI dashboard

### AC8: SoD Audit Trail
**Given** SoD operations occur  
**When** audit events are logged  
**Then**:
- Audit events logged for all SoD activities:
  - `SodConflictDetected`: Role assignment blocked due to SoD conflict
  - `SodExceptionRequested`: SoD exception requested with justification
  - `SodExceptionApproved`: Compliance Officer approved exception
  - `SodExceptionRejected`: Compliance Officer rejected exception
  - `SodExceptionExpired`: Exception expired and role auto-revoked
  - `SodExceptionRevoked`: Manual revocation by admin
- All audit events include correlation ID, user details, role details, conflict details
- Audit events queryable via Admin Service audit API

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Lines 1134-1157 (Story 1.21), Phase 4 Overview  
**Architecture Sections**: Section 4.2 (Keycloak RBAC), Section 4.3 (Keycloak Admin API), Section 4.1 (Admin Service)  
**Requirements**: FR6 (Expanded roles), FR7 (SoD enforcement), NFR10 (Security controls)

### Technology Stack

- **Identity Provider**: Keycloak 24+ with realm roles and composite roles
- **Workflow Engine**: Camunda 8 (Zeebe) for exception workflows
- **Database**: SQL Server 2022 (Admin Service database)
- **Reporting**: SQL Server Reporting Services (SSRS) or custom PDF generation
- **Frontend**: React with role management UI

### Database Schema

```sql
-- Admin Service Database

CREATE TABLE SodPolicies (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Role1 NVARCHAR(100) NOT NULL,
    Role2 NVARCHAR(100) NOT NULL,
    ConflictDescription NVARCHAR(500) NOT NULL,
    Severity NVARCHAR(20) NOT NULL,  -- Critical, High, Medium
    Enabled BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy NVARCHAR(100),
    
    CONSTRAINT UQ_SodPolicy_Roles UNIQUE (Role1, Role2),
    INDEX IX_Role1 (Role1),
    INDEX IX_Role2 (Role2),
    INDEX IX_Enabled (Enabled)
);

-- Seed SoD policies
INSERT INTO SodPolicies (Role1, Role2, ConflictDescription, Severity)
VALUES 
    ('Loan Processor', 'Loan Approver', 'Cannot create and approve own loans (fraud risk)', 'Critical'),
    ('Treasury Officer', 'GL Accountant', 'Cannot disburse and record same transaction (fraud risk)', 'Critical'),
    ('Collections Officer', 'Loan Officer', 'Cannot originate and collect same loans (conflict of interest)', 'High'),
    ('Loan Officer', 'Auditor', 'Cannot create loans and audit own work (independence conflict)', 'High'),
    ('Credit Analyst', 'Loan Approver', 'Cannot analyze and approve same loan (dual authorization)', 'High'),
    ('System Administrator', 'CEO', 'Technical vs. business role separation (governance)', 'Medium'),
    ('Loan Processor', 'Collections Officer', 'Cannot process and collect same loans (conflict of interest)', 'Medium');

CREATE TABLE SodExceptions (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ExceptionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    UserId NVARCHAR(100) NOT NULL,
    UserName NVARCHAR(200) NOT NULL,
    RequestedRole NVARCHAR(100) NOT NULL,
    ConflictingRoles NVARCHAR(MAX) NOT NULL,  -- JSON array of conflicting roles
    BusinessJustification NVARCHAR(1000) NOT NULL,
    ExceptionDuration INT NOT NULL,  -- Days (max 90)
    Status NVARCHAR(50) NOT NULL,  -- Pending, Approved, Rejected, Active, Expired, Revoked
    
    RequestedBy NVARCHAR(100) NOT NULL,
    RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ReviewedBy NVARCHAR(100) NULL,
    ReviewedAt DATETIME2 NULL,
    ReviewComments NVARCHAR(1000) NULL,
    
    ApprovedAt DATETIME2 NULL,
    ExpiresAt DATETIME2 NULL,
    ExpiredAt DATETIME2 NULL,
    RevokedAt DATETIME2 NULL,
    RevokedBy NVARCHAR(100) NULL,
    RevocationReason NVARCHAR(500) NULL,
    
    CamundaProcessInstanceId NVARCHAR(100),
    CorrelationId NVARCHAR(100),
    
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_UserId (UserId),
    INDEX IX_Status (Status),
    INDEX IX_ExpiresAt (ExpiresAt),
    INDEX IX_RequestedAt (RequestedAt DESC)
);

CREATE TABLE RoleDefinitions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RoleName NVARCHAR(100) NOT NULL UNIQUE,
    DisplayName NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000),
    Category NVARCHAR(50),  -- Business, Technical, Management
    RiskLevel NVARCHAR(20),  -- Low, Medium, High, Critical
    RequiresApproval BIT NOT NULL DEFAULT 0,
    ApprovalWorkflow NVARCHAR(100),  -- Camunda process ID
    MaxAssignments INT NULL,  -- Limit how many users can have this role
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_RoleName (RoleName)
);

-- Seed role definitions
INSERT INTO RoleDefinitions (RoleName, DisplayName, Description, Category, RiskLevel, RequiresApproval)
VALUES 
    -- Existing V1 roles
    ('Loan Officer', 'Loan Officer', 'Originates and manages loan applications', 'Business', 'Medium', 0),
    ('Loan Processor', 'Loan Processor', 'Processes loan documentation and verification', 'Business', 'Medium', 0),
    ('Loan Approver', 'Loan Approver', 'Approves or rejects loan applications', 'Business', 'High', 1),
    ('Credit Analyst', 'Credit Analyst', 'Analyzes creditworthiness and risk', 'Business', 'Medium', 0),
    ('System Administrator', 'System Administrator', 'Technical system administration', 'Technical', 'Critical', 1),
    ('CEO', 'Chief Executive Officer', 'Executive authority over all operations', 'Management', 'Critical', 1),
    
    -- New expanded roles (Story 1.21)
    ('Collections Officer', 'Collections Officer', 'Manages overdue loans and collections', 'Business', 'Medium', 0),
    ('Compliance Officer', 'Compliance Officer', 'Ensures regulatory compliance', 'Management', 'High', 1),
    ('Treasury Officer', 'Treasury Officer', 'Manages disbursements and cash flow', 'Business', 'High', 1),
    ('GL Accountant', 'General Ledger Accountant', 'Records journal entries and financial transactions', 'Business', 'High', 0),
    ('Auditor', 'Auditor', 'Reviews audit logs and compliance (read-only)', 'Management', 'Medium', 1),
    ('Risk Manager', 'Risk Manager', 'Assesses and manages institutional risk', 'Management', 'High', 1),
    ('Branch Manager', 'Branch Manager', 'Supervises branch operations', 'Management', 'High', 1);

CREATE TABLE RoleHierarchy (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ParentRole NVARCHAR(100) NOT NULL,
    ChildRole NVARCHAR(100) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT UQ_RoleHierarchy UNIQUE (ParentRole, ChildRole),
    INDEX IX_ParentRole (ParentRole),
    INDEX IX_ChildRole (ChildRole)
);

-- Seed role hierarchy
INSERT INTO RoleHierarchy (ParentRole, ChildRole)
VALUES 
    -- CEO inherits all roles
    ('CEO', 'Branch Manager'),
    ('CEO', 'Compliance Officer'),
    ('CEO', 'Risk Manager'),
    ('CEO', 'Loan Approver'),
    
    -- Branch Manager inherits branch-level roles
    ('Branch Manager', 'Loan Officer'),
    ('Branch Manager', 'Loan Processor'),
    ('Branch Manager', 'Collections Officer'),
    
    -- Compliance Officer inherits Auditor permissions
    ('Compliance Officer', 'Auditor');

-- View for active SoD exceptions
CREATE VIEW vw_ActiveSodExceptions AS
SELECT 
    ExceptionId,
    UserId,
    UserName,
    RequestedRole,
    ConflictingRoles,
    BusinessJustification,
    ApprovedAt,
    ExpiresAt,
    DATEDIFF(DAY, GETUTCDATE(), ExpiresAt) AS DaysRemaining,
    ReviewedBy AS ApprovedBy
FROM SodExceptions
WHERE Status = 'Active'
    AND ExpiresAt > GETUTCDATE();
GO
```

### API Endpoints

#### Role Management API

```csharp
// Controllers/RoleManagementController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IntelliFin.Admin.Services;
using IntelliFin.Admin.Models;
using System.Security.Claims;

namespace IntelliFin.Admin.Controllers
{
    [ApiController]
    [Route("api/admin/roles")]
    [Authorize(Roles = "System Administrator,Compliance Officer")]
    public class RoleManagementController : ControllerBase
    {
        private readonly IRoleManagementService _roleService;
        private readonly ILogger<RoleManagementController> _logger;

        public RoleManagementController(
            IRoleManagementService roleService,
            ILogger<RoleManagementController> logger)
        {
            _roleService = roleService;
            _logger = logger;
        }

        /// <summary>
        /// Get all available roles with metadata
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<RoleDefinitionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
        {
            var roles = await _roleService.GetAllRolesAsync(cancellationToken);
            return Ok(roles);
        }

        /// <summary>
        /// Get user's current roles
        /// </summary>
        [HttpGet("users/{userId}")]
        [ProducesResponseType(typeof(UserRolesDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserRoles(
            string userId,
            CancellationToken cancellationToken)
        {
            var userRoles = await _roleService.GetUserRolesAsync(userId, cancellationToken);
            
            if (userRoles == null)
                return NotFound();

            return Ok(userRoles);
        }

        /// <summary>
        /// Assign role to user (with SoD validation)
        /// </summary>
        [HttpPost("users/{userId}/assign")]
        [Authorize(Roles = "System Administrator")]
        [RequiresMfa(TimeoutMinutes = 15)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SodConflictResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AssignRole(
            string userId,
            [FromBody] RoleAssignmentRequest request,
            CancellationToken cancellationToken)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var adminName = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation(
                "Role assignment request: User={UserId}, Role={Role}, Admin={AdminId}",
                userId, request.RoleName, adminId);

            try
            {
                var result = await _roleService.AssignRoleAsync(
                    userId,
                    request.RoleName,
                    request.ConfirmedSodOverride ?? false,
                    adminId,
                    adminName,
                    cancellationToken);

                return Ok(new
                {
                    message = "Role assigned successfully",
                    roleAssignmentId = result.RoleAssignmentId
                });
            }
            catch (SodConflictException ex)
            {
                return Conflict(new SodConflictResponse
                {
                    ConflictDetected = true,
                    ConflictingRoles = ex.ConflictingRoles,
                    ConflictDescription = ex.Message,
                    Severity = ex.Severity,
                    CanRequestException = ex.Severity != "Critical",
                    ExceptionRequestUrl = "/api/admin/sod/exception-request"
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Remove role from user
        /// </summary>
        [HttpPost("users/{userId}/remove")]
        [Authorize(Roles = "System Administrator")]
        [RequiresMfa(TimeoutMinutes = 15)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveRole(
            string userId,
            [FromBody] RoleRemovalRequest request,
            CancellationToken cancellationToken)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var adminName = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation(
                "Role removal request: User={UserId}, Role={Role}, Admin={AdminId}",
                userId, request.RoleName, adminId);

            try
            {
                await _roleService.RemoveRoleAsync(
                    userId,
                    request.RoleName,
                    adminId,
                    adminName,
                    request.Reason,
                    cancellationToken);

                return Ok(new { message = "Role removed successfully" });
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Validate SoD conflict for role assignment (pre-check)
        /// </summary>
        [HttpPost("validate-sod")]
        [ProducesResponseType(typeof(SodValidationResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> ValidateSod(
            [FromBody] SodValidationRequest request,
            CancellationToken cancellationToken)
        {
            var validation = await _roleService.ValidateSodAsync(
                request.UserId,
                request.ProposedRole,
                cancellationToken);

            return Ok(validation);
        }

        /// <summary>
        /// Get role hierarchy
        /// </summary>
        [HttpGet("hierarchy")]
        [ProducesResponseType(typeof(List<RoleHierarchyDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRoleHierarchy(CancellationToken cancellationToken)
        {
            var hierarchy = await _roleService.GetRoleHierarchyAsync(cancellationToken);
            return Ok(hierarchy);
        }
    }

    /// <summary>
    /// SoD Exception Management API
    /// </summary>
    [ApiController]
    [Route("api/admin/sod")]
    [Authorize]
    public class SodExceptionController : ControllerBase
    {
        private readonly ISodExceptionService _sodService;
        private readonly ILogger<SodExceptionController> _logger;

        public SodExceptionController(
            ISodExceptionService sodService,
            ILogger<SodExceptionController> logger)
        {
            _sodService = sodService;
            _logger = logger;
        }

        /// <summary>
        /// Request SoD exception
        /// </summary>
        [HttpPost("exception-request")]
        [ProducesResponseType(typeof(SodExceptionResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RequestException(
            [FromBody] SodExceptionRequest request,
            CancellationToken cancellationToken)
        {
            var requestorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var requestorName = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation(
                "SoD exception request: User={UserId}, Role={Role}, Requestor={RequestorId}",
                request.UserId, request.RequestedRole, requestorId);

            try
            {
                var response = await _sodService.RequestExceptionAsync(
                    request,
                    requestorId,
                    requestorName,
                    cancellationToken);

                return AcceptedAtAction(
                    nameof(GetExceptionStatus),
                    new { exceptionId = response.ExceptionId },
                    response);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get exception status
        /// </summary>
        [HttpGet("exceptions/{exceptionId}")]
        [ProducesResponseType(typeof(SodExceptionStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetExceptionStatus(
            Guid exceptionId,
            CancellationToken cancellationToken)
        {
            var status = await _sodService.GetExceptionStatusAsync(exceptionId, cancellationToken);
            
            if (status == null)
                return NotFound();

            return Ok(status);
        }

        /// <summary>
        /// Approve SoD exception (Compliance Officer only)
        /// </summary>
        [HttpPost("exceptions/{exceptionId}/approve")]
        [Authorize(Roles = "Compliance Officer")]
        [RequiresMfa(TimeoutMinutes = 15)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApproveException(
            Guid exceptionId,
            [FromBody] SodExceptionReviewDto review,
            CancellationToken cancellationToken)
        {
            var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var reviewerName = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation(
                "SoD exception approval: ExceptionId={ExceptionId}, Reviewer={ReviewerId}",
                exceptionId, reviewerId);

            try
            {
                await _sodService.ApproveExceptionAsync(
                    exceptionId,
                    reviewerId,
                    reviewerName,
                    review.Comments,
                    cancellationToken);

                return Ok(new { message = "SoD exception approved successfully" });
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Reject SoD exception (Compliance Officer only)
        /// </summary>
        [HttpPost("exceptions/{exceptionId}/reject")]
        [Authorize(Roles = "Compliance Officer")]
        [RequiresMfa(TimeoutMinutes = 15)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RejectException(
            Guid exceptionId,
            [FromBody] SodExceptionReviewDto review,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(review.Comments) || review.Comments.Length < 20)
                return BadRequest(new { error = "Rejection reason must be at least 20 characters" });

            var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var reviewerName = User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation(
                "SoD exception rejection: ExceptionId={ExceptionId}, Reviewer={ReviewerId}",
                exceptionId, reviewerId);

            try
            {
                await _sodService.RejectExceptionAsync(
                    exceptionId,
                    reviewerId,
                    reviewerName,
                    review.Comments,
                    cancellationToken);

                return Ok(new { message = "SoD exception rejected successfully" });
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Get SoD compliance report
        /// </summary>
        [HttpGet("compliance-report")]
        [Authorize(Roles = "Compliance Officer,Auditor")]
        [ProducesResponseType(typeof(SodComplianceReportDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetComplianceReport(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            CancellationToken cancellationToken)
        {
            var report = await _sodService.GenerateComplianceReportAsync(
                startDate ?? DateTime.UtcNow.AddMonths(-3),
                endDate ?? DateTime.UtcNow,
                cancellationToken);

            return Ok(report);
        }

        /// <summary>
        /// Get all SoD policies
        /// </summary>
        [HttpGet("policies")]
        [ProducesResponseType(typeof(List<SodPolicyDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSodPolicies(CancellationToken cancellationToken)
        {
            var policies = await _sodService.GetAllPoliciesAsync(cancellationToken);
            return Ok(policies);
        }

        /// <summary>
        /// Update SoD policy (Compliance Officer only)
        /// </summary>
        [HttpPut("policies/{policyId}")]
        [Authorize(Roles = "Compliance Officer")]
        [RequiresMfa(TimeoutMinutes = 15)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateSodPolicy(
            int policyId,
            [FromBody] SodPolicyUpdateDto update,
            CancellationToken cancellationToken)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation(
                "SoD policy update: PolicyId={PolicyId}, Admin={AdminId}",
                policyId, adminId);

            try
            {
                await _sodService.UpdatePolicyAsync(policyId, update, adminId, cancellationToken);
                return Ok(new { message = "SoD policy updated successfully" });
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
// Services/RoleManagementService.cs
using IntelliFin.Admin.Data;
using IntelliFin.Admin.Models;
using IntelliFin.Shared.Keycloak;
using IntelliFin.Shared.Audit;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.Admin.Services
{
    public interface IRoleManagementService
    {
        Task<List<RoleDefinitionDto>> GetAllRolesAsync(CancellationToken cancellationToken);
        Task<UserRolesDto?> GetUserRolesAsync(string userId, CancellationToken cancellationToken);
        Task<RoleAssignmentResult> AssignRoleAsync(
            string userId,
            string roleName,
            bool confirmedSodOverride,
            string adminId,
            string adminName,
            CancellationToken cancellationToken);
        Task RemoveRoleAsync(
            string userId,
            string roleName,
            string adminId,
            string adminName,
            string reason,
            CancellationToken cancellationToken);
        Task<SodValidationResponse> ValidateSodAsync(
            string userId,
            string proposedRole,
            CancellationToken cancellationToken);
        Task<List<RoleHierarchyDto>> GetRoleHierarchyAsync(CancellationToken cancellationToken);
    }

    public class RoleManagementService : IRoleManagementService
    {
        private readonly AdminDbContext _dbContext;
        private readonly IKeycloakAdminClient _keycloakClient;
        private readonly IAuditService _auditService;
        private readonly ILogger<RoleManagementService> _logger;

        public RoleManagementService(
            AdminDbContext dbContext,
            IKeycloakAdminClient keycloakClient,
            IAuditService auditService,
            ILogger<RoleManagementService> logger)
        {
            _dbContext = dbContext;
            _keycloakClient = keycloakClient;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<List<RoleDefinitionDto>> GetAllRolesAsync(CancellationToken cancellationToken)
        {
            var roles = await _dbContext.RoleDefinitions
                .OrderBy(r => r.Category)
                .ThenBy(r => r.DisplayName)
                .Select(r => new RoleDefinitionDto
                {
                    RoleName = r.RoleName,
                    DisplayName = r.DisplayName,
                    Description = r.Description,
                    Category = r.Category,
                    RiskLevel = r.RiskLevel,
                    RequiresApproval = r.RequiresApproval
                })
                .ToListAsync(cancellationToken);

            return roles;
        }

        public async Task<UserRolesDto?> GetUserRolesAsync(string userId, CancellationToken cancellationToken)
        {
            // Get roles from Keycloak
            var keycloakRoles = await _keycloakClient.GetUserRolesAsync(userId, cancellationToken);

            if (keycloakRoles == null)
                return null;

            // Enrich with role definitions from database
            var roleNames = keycloakRoles.Select(r => r.Name).ToList();
            var roleDefinitions = await _dbContext.RoleDefinitions
                .Where(r => roleNames.Contains(r.RoleName))
                .ToListAsync(cancellationToken);

            var roles = keycloakRoles.Select(kr =>
            {
                var def = roleDefinitions.FirstOrDefault(rd => rd.RoleName == kr.Name);
                return new UserRoleDto
                {
                    RoleName = kr.Name,
                    DisplayName = def?.DisplayName ?? kr.Name,
                    Category = def?.Category,
                    RiskLevel = def?.RiskLevel,
                    AssignedAt = kr.AssignedAt
                };
            }).ToList();

            // Check for active SoD exceptions
            var activeExceptions = await _dbContext.SodExceptions
                .Where(e => e.UserId == userId && e.Status == "Active" && e.ExpiresAt > DateTime.UtcNow)
                .Select(e => new SodExceptionSummaryDto
                {
                    ExceptionId = e.ExceptionId,
                    RequestedRole = e.RequestedRole,
                    ExpiresAt = e.ExpiresAt.Value,
                    Justification = e.BusinessJustification
                })
                .ToListAsync(cancellationToken);

            return new UserRolesDto
            {
                UserId = userId,
                Roles = roles,
                ActiveSodExceptions = activeExceptions
            };
        }

        public async Task<RoleAssignmentResult> AssignRoleAsync(
            string userId,
            string roleName,
            bool confirmedSodOverride,
            string adminId,
            string adminName,
            CancellationToken cancellationToken)
        {
            // Get user's current roles
            var currentRoles = await _keycloakClient.GetUserRolesAsync(userId, cancellationToken);
            var currentRoleNames = currentRoles?.Select(r => r.Name).ToList() ?? new List<string>();

            // Check SoD conflicts
            var sodConflicts = await CheckSodConflictsAsync(currentRoleNames, roleName, cancellationToken);

            if (sodConflicts.Any())
            {
                var criticalConflicts = sodConflicts.Where(c => c.Severity == "Critical").ToList();

                if (criticalConflicts.Any())
                {
                    // Log conflict detection
                    await _auditService.LogAsync(new AuditEvent
                    {
                        Actor = adminId,
                        Action = "SodConflictDetected",
                        EntityType = "RoleAssignment",
                        EntityId = $"{userId}:{roleName}",
                        EventData = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            userId = userId,
                            requestedRole = roleName,
                            conflicts = criticalConflicts
                        })
                    }, cancellationToken);

                    throw new SodConflictException(
                        $"Critical SoD conflict: Cannot assign role '{roleName}' due to existing role(s): {string.Join(", ", criticalConflicts.Select(c => c.ConflictingRole))}",
                        criticalConflicts.Select(c => c.ConflictingRole).ToList(),
                        "Critical");
                }

                // High severity conflicts require confirmation
                if (!confirmedSodOverride)
                {
                    throw new SodConflictException(
                        $"SoD conflict warning: Assigning role '{roleName}' conflicts with existing role(s): {string.Join(", ", sodConflicts.Select(c => c.ConflictingRole))}. Confirmation required.",
                        sodConflicts.Select(c => c.ConflictingRole).ToList(),
                        "High");
                }
            }

            // Assign role in Keycloak
            await _keycloakClient.AssignRoleToUserAsync(userId, roleName, cancellationToken);

            var roleAssignmentId = Guid.NewGuid();

            // Audit log
            await _auditService.LogAsync(new AuditEvent
            {
                Actor = adminId,
                Action = "RoleAssigned",
                EntityType = "UserRole",
                EntityId = $"{userId}:{roleName}",
                EventData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    userId = userId,
                    roleName = roleName,
                    assignedBy = adminId,
                    sodOverrideConfirmed = confirmedSodOverride,
                    roleAssignmentId = roleAssignmentId
                })
            }, cancellationToken);

            _logger.LogInformation(
                "Role assigned: User={UserId}, Role={Role}, Admin={AdminId}",
                userId, roleName, adminId);

            return new RoleAssignmentResult
            {
                RoleAssignmentId = roleAssignmentId,
                Success = true
            };
        }

        public async Task<SodValidationResponse> ValidateSodAsync(
            string userId,
            string proposedRole,
            CancellationToken cancellationToken)
        {
            var currentRoles = await _keycloakClient.GetUserRolesAsync(userId, cancellationToken);
            var currentRoleNames = currentRoles?.Select(r => r.Name).ToList() ?? new List<string>();

            var conflicts = await CheckSodConflictsAsync(currentRoleNames, proposedRole, cancellationToken);

            return new SodValidationResponse
            {
                HasConflict = conflicts.Any(),
                Conflicts = conflicts,
                CanProceedWithOverride = !conflicts.Any(c => c.Severity == "Critical")
            };
        }

        private async Task<List<SodConflictDto>> CheckSodConflictsAsync(
            List<string> currentRoles,
            string proposedRole,
            CancellationToken cancellationToken)
        {
            var conflicts = new List<SodConflictDto>();

            var policies = await _dbContext.SodPolicies
                .Where(p => p.Enabled)
                .Where(p => (p.Role1 == proposedRole || p.Role2 == proposedRole))
                .ToListAsync(cancellationToken);

            foreach (var policy in policies)
            {
                var conflictingRole = policy.Role1 == proposedRole ? policy.Role2 : policy.Role1;

                if (currentRoles.Contains(conflictingRole))
                {
                    conflicts.Add(new SodConflictDto
                    {
                        ConflictingRole = conflictingRole,
                        ConflictDescription = policy.ConflictDescription,
                        Severity = policy.Severity,
                        PolicyId = policy.Id
                    });
                }
            }

            return conflicts;
        }

        public async Task<List<RoleHierarchyDto>> GetRoleHierarchyAsync(CancellationToken cancellationToken)
        {
            var hierarchy = await _dbContext.RoleHierarchy
                .Select(rh => new RoleHierarchyDto
                {
                    ParentRole = rh.ParentRole,
                    ChildRole = rh.ChildRole
                })
                .ToListAsync(cancellationToken);

            return hierarchy;
        }

        // Additional methods (RemoveRoleAsync) omitted for brevity
    }
}
```

### SoD Exception Service

```csharp
// Services/SodExceptionService.cs
using IntelliFin.Admin.Data;
using IntelliFin.Admin.Models;
using IntelliFin.Shared.Keycloak;
using IntelliFin.Shared.Camunda;
using IntelliFin.Shared.Audit;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.Admin.Services
{
    public interface ISodExceptionService
    {
        Task<SodExceptionResponse> RequestExceptionAsync(
            SodExceptionRequest request,
            string requestorId,
            string requestorName,
            CancellationToken cancellationToken);
        
        Task<SodExceptionStatusDto?> GetExceptionStatusAsync(
            Guid exceptionId,
            CancellationToken cancellationToken);
        
        Task ApproveExceptionAsync(
            Guid exceptionId,
            string reviewerId,
            string reviewerName,
            string comments,
            CancellationToken cancellationToken);
        
        Task RejectExceptionAsync(
            Guid exceptionId,
            string reviewerId,
            string reviewerName,
            string comments,
            CancellationToken cancellationToken);
        
        Task<SodComplianceReportDto> GenerateComplianceReportAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken);
        
        Task<List<SodPolicyDto>> GetAllPoliciesAsync(CancellationToken cancellationToken);
        
        Task UpdatePolicyAsync(
            int policyId,
            SodPolicyUpdateDto update,
            string adminId,
            CancellationToken cancellationToken);
    }

    public class SodExceptionService : ISodExceptionService
    {
        private readonly AdminDbContext _dbContext;
        private readonly IKeycloakAdminClient _keycloakClient;
        private readonly ICamundaClient _camundaClient;
        private readonly IAuditService _auditService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<SodExceptionService> _logger;

        private const int MAX_EXCEPTION_DURATION_DAYS = 90;

        public SodExceptionService(
            AdminDbContext dbContext,
            IKeycloakAdminClient keycloakClient,
            ICamundaClient camundaClient,
            IAuditService auditService,
            INotificationService notificationService,
            ILogger<SodExceptionService> logger)
        {
            _dbContext = dbContext;
            _keycloakClient = keycloakClient;
            _camundaClient = camundaClient;
            _auditService = auditService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<SodExceptionResponse> RequestExceptionAsync(
            SodExceptionRequest request,
            string requestorId,
            string requestorName,
            CancellationToken cancellationToken)
        {
            // Validation
            if (request.ExceptionDuration <= 0 || request.ExceptionDuration > MAX_EXCEPTION_DURATION_DAYS)
                throw new ValidationException($"Exception duration must be between 1 and {MAX_EXCEPTION_DURATION_DAYS} days");

            if (string.IsNullOrWhiteSpace(request.BusinessJustification) || request.BusinessJustification.Length < 50)
                throw new ValidationException("Business justification must be at least 50 characters");

            var correlationId = Guid.NewGuid().ToString("N");
            var exceptionId = Guid.NewGuid();

            // Create exception record
            var exception = new SodException
            {
                ExceptionId = exceptionId,
                UserId = request.UserId,
                UserName = request.UserName,
                RequestedRole = request.RequestedRole,
                ConflictingRoles = System.Text.Json.JsonSerializer.Serialize(request.ConflictingRoles),
                BusinessJustification = request.BusinessJustification,
                ExceptionDuration = request.ExceptionDuration,
                Status = "Pending",
                RequestedBy = requestorId,
                RequestedAt = DateTime.UtcNow,
                CorrelationId = correlationId
            };

            _dbContext.SodExceptions.Add(exception);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Start Camunda workflow
            var processInstanceId = await _camundaClient.StartProcessAsync(
                "sod-exception-request",
                new Dictionary<string, object>
                {
                    { "exceptionId", exceptionId.ToString() },
                    { "userId", request.UserId },
                    { "requestedRole", request.RequestedRole },
                    { "conflictingRoles", request.ConflictingRoles },
                    { "justification", request.BusinessJustification },
                    { "requestedBy", requestorId },
                    { "correlationId", correlationId }
                },
                cancellationToken);

            exception.CamundaProcessInstanceId = processInstanceId;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Notify Compliance Officer
            var complianceOfficers = await _keycloakClient.GetUsersWithRoleAsync("Compliance Officer", cancellationToken);
            foreach (var officer in complianceOfficers)
            {
                await _notificationService.SendSodExceptionNotificationAsync(
                    officer.UserId,
                    new SodExceptionNotificationDto
                    {
                        ExceptionId = exceptionId,
                        UserName = request.UserName,
                        RequestedRole = request.RequestedRole,
                        ConflictingRoles = request.ConflictingRoles,
                        Justification = request.BusinessJustification,
                        RequestedAt = DateTime.UtcNow
                    },
                    cancellationToken);
            }

            // Audit log
            await _auditService.LogAsync(new AuditEvent
            {
                Actor = requestorId,
                Action = "SodExceptionRequested",
                EntityType = "SodException",
                EntityId = exceptionId.ToString(),
                CorrelationId = correlationId,
                EventData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    userId = request.UserId,
                    requestedRole = request.RequestedRole,
                    conflictingRoles = request.ConflictingRoles,
                    duration = request.ExceptionDuration
                })
            }, cancellationToken);

            _logger.LogInformation(
                "SoD exception requested: ExceptionId={ExceptionId}, User={UserId}, Role={Role}",
                exceptionId, request.UserId, request.RequestedRole);

            return new SodExceptionResponse
            {
                ExceptionId = exceptionId,
                Status = "Pending",
                Message = "SoD exception request submitted. Awaiting Compliance Officer review.",
                EstimatedReviewTime = DateTime.UtcNow.AddHours(24)
            };
        }

        public async Task ApproveExceptionAsync(
            Guid exceptionId,
            string reviewerId,
            string reviewerName,
            string comments,
            CancellationToken cancellationToken)
        {
            var exception = await _dbContext.SodExceptions
                .FirstOrDefaultAsync(e => e.ExceptionId == exceptionId, cancellationToken);

            if (exception == null)
                throw new NotFoundException("SoD exception not found");

            if (exception.Status != "Pending")
                throw new InvalidOperationException($"Exception is not pending (current status: {exception.Status})");

            // Update exception record
            exception.Status = "Approved";
            exception.ReviewedBy = reviewerId;
            exception.ReviewedAt = DateTime.UtcNow;
            exception.ReviewComments = comments;
            exception.ApprovedAt = DateTime.UtcNow;
            exception.ExpiresAt = DateTime.UtcNow.AddDays(exception.ExceptionDuration);

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Complete Camunda task
            await _camundaClient.CompleteTaskAsync(
                exception.CamundaProcessInstanceId!,
                "compliance-review",
                new Dictionary<string, object>
                {
                    { "approved", true },
                    { "reviewedBy", reviewerId },
                    { "comments", comments ?? "" }
                },
                cancellationToken);

            // Assign role in Keycloak with exception metadata
            await _keycloakClient.AssignRoleToUserAsync(exception.UserId, exception.RequestedRole, cancellationToken);
            await _keycloakClient.SetUserAttributeAsync(
                exception.UserId,
                $"sod_exception_{exception.ExceptionId}",
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    exceptionId = exception.ExceptionId,
                    role = exception.RequestedRole,
                    expiresAt = exception.ExpiresAt,
                    approvedBy = reviewerId
                }),
                cancellationToken);

            // Update status to Active
            exception.Status = "Active";
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Notify requester and user
            await _notificationService.SendSodExceptionApprovedNotificationAsync(
                exception.UserId,
                new SodExceptionApprovedDto
                {
                    ExceptionId = exceptionId,
                    RequestedRole = exception.RequestedRole,
                    ExpiresAt = exception.ExpiresAt.Value,
                    ApprovedBy = reviewerName
                },
                cancellationToken);

            // Audit logs
            await _auditService.LogAsync(new AuditEvent
            {
                Actor = reviewerId,
                Action = "SodExceptionApproved",
                EntityType = "SodException",
                EntityId = exceptionId.ToString(),
                CorrelationId = exception.CorrelationId,
                EventData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    userId = exception.UserId,
                    requestedRole = exception.RequestedRole,
                    expiresAt = exception.ExpiresAt,
                    comments = comments
                })
            }, cancellationToken);

            _logger.LogInformation(
                "SoD exception approved: ExceptionId={ExceptionId}, Reviewer={ReviewerId}",
                exceptionId, reviewerId);
        }

        public async Task<SodComplianceReportDto> GenerateComplianceReportAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken)
        {
            var totalActiveExceptions = await _dbContext.SodExceptions
                .Where(e => e.Status == "Active" && e.ExpiresAt > DateTime.UtcNow)
                .CountAsync(cancellationToken);

            var expiredExceptions = await _dbContext.SodExceptions
                .Where(e => e.Status == "Expired" && e.ExpiredAt >= startDate && e.ExpiredAt <= endDate)
                .CountAsync(cancellationToken);

            var blockedAssignments = await _auditService.CountEventsByActionAsync(
                "SodConflictDetected",
                startDate,
                endDate,
                cancellationToken);

            var activeExceptionsList = await _dbContext.SodExceptions
                .Where(e => e.Status == "Active" && e.ExpiresAt > DateTime.UtcNow)
                .Select(e => new SodExceptionSummaryDto
                {
                    ExceptionId = e.ExceptionId,
                    UserId = e.UserId,
                    UserName = e.UserName,
                    RequestedRole = e.RequestedRole,
                    ExpiresAt = e.ExpiresAt.Value,
                    Justification = e.BusinessJustification,
                    ApprovedBy = e.ReviewedBy!
                })
                .ToListAsync(cancellationToken);

            return new SodComplianceReportDto
            {
                ReportPeriodStart = startDate,
                ReportPeriodEnd = endDate,
                TotalActiveExceptions = totalActiveExceptions,
                ExpiredExceptionsCount = expiredExceptions,
                BlockedAssignmentsCount = blockedAssignments,
                ActiveExceptions = activeExceptionsList,
                GeneratedAt = DateTime.UtcNow
            };
        }

        public async Task<List<SodPolicyDto>> GetAllPoliciesAsync(CancellationToken cancellationToken)
        {
            var policies = await _dbContext.SodPolicies
                .OrderBy(p => p.Severity)
                .ThenBy(p => p.Role1)
                .Select(p => new SodPolicyDto
                {
                    Id = p.Id,
                    Role1 = p.Role1,
                    Role2 = p.Role2,
                    ConflictDescription = p.ConflictDescription,
                    Severity = p.Severity,
                    Enabled = p.Enabled
                })
                .ToListAsync(cancellationToken);

            return policies;
        }

        // Additional methods (RejectExceptionAsync, UpdatePolicyAsync) omitted for brevity
    }
}
```

### Camunda BPMN Process

```xml
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:zeebe="http://camunda.org/schema/zeebe/1.0"
                  id="sod-exception-request"
                  targetNamespace="http://intellifin.local/bpmn">
  
  <bpmn:process id="sod-exception-request" name="SoD Exception Request" isExecutable="true">
    
    <!-- Start Event -->
    <bpmn:startEvent id="StartEvent_ExceptionRequest" name="Exception Requested">
      <bpmn:outgoing>Flow_ToNotifyCompliance</bpmn:outgoing>
    </bpmn:startEvent>
    
    <!-- Notify Compliance Officer Service Task -->
    <bpmn:serviceTask id="Task_NotifyCompliance" name="Notify Compliance Officer">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="notify-compliance-officer" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToNotifyCompliance</bpmn:incoming>
      <bpmn:outgoing>Flow_ToReview</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Compliance Review User Task -->
    <bpmn:userTask id="Task_ComplianceReview" name="Compliance Review">
      <bpmn:extensionElements>
        <zeebe:assignmentDefinition candidateGroups="Compliance Officer" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToReview</bpmn:incoming>
      <bpmn:outgoing>Flow_ToDecision</bpmn:outgoing>
    </bpmn:userTask>
    
    <!-- Exclusive Gateway - Approved? -->
    <bpmn:exclusiveGateway id="Gateway_ReviewDecision" name="Approved?">
      <bpmn:incoming>Flow_ToDecision</bpmn:incoming>
      <bpmn:outgoing>Flow_ToApprove</bpmn:outgoing>
      <bpmn:outgoing>Flow_ToReject</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    
    <!-- Assign Role Service Task -->
    <bpmn:serviceTask id="Task_AssignRole" name="Assign Role with Exception">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="assign-role-with-exception" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToApprove</bpmn:incoming>
      <bpmn:outgoing>Flow_ToNotifyApproved</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Notify User - Approved -->
    <bpmn:serviceTask id="Task_NotifyApproved" name="Notify User (Approved)">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="notify-exception-approved" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToNotifyApproved</bpmn:incoming>
      <bpmn:outgoing>Flow_ToEndApproved</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Notify User - Rejected -->
    <bpmn:serviceTask id="Task_NotifyRejected" name="Notify User (Rejected)">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="notify-exception-rejected" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToReject</bpmn:incoming>
      <bpmn:outgoing>Flow_ToEndRejected</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- End Events -->
    <bpmn:endEvent id="EndEvent_Approved" name="Exception Granted">
      <bpmn:incoming>Flow_ToEndApproved</bpmn:incoming>
    </bpmn:endEvent>
    
    <bpmn:endEvent id="EndEvent_Rejected" name="Exception Denied">
      <bpmn:incoming>Flow_ToEndRejected</bpmn:incoming>
    </bpmn:endEvent>
    
    <!-- Sequence Flows -->
    <bpmn:sequenceFlow id="Flow_ToNotifyCompliance" sourceRef="StartEvent_ExceptionRequest" targetRef="Task_NotifyCompliance" />
    <bpmn:sequenceFlow id="Flow_ToReview" sourceRef="Task_NotifyCompliance" targetRef="Task_ComplianceReview" />
    <bpmn:sequenceFlow id="Flow_ToDecision" sourceRef="Task_ComplianceReview" targetRef="Gateway_ReviewDecision" />
    
    <bpmn:sequenceFlow id="Flow_ToApprove" sourceRef="Gateway_ReviewDecision" targetRef="Task_AssignRole">
      <bpmn:conditionExpression>${approved == true}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    
    <bpmn:sequenceFlow id="Flow_ToReject" sourceRef="Gateway_ReviewDecision" targetRef="Task_NotifyRejected">
      <bpmn:conditionExpression>${approved == false}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    
    <bpmn:sequenceFlow id="Flow_ToNotifyApproved" sourceRef="Task_AssignRole" targetRef="Task_NotifyApproved" />
    <bpmn:sequenceFlow id="Flow_ToEndApproved" sourceRef="Task_NotifyApproved" targetRef="EndEvent_Approved" />
    <bpmn:sequenceFlow id="Flow_ToEndRejected" sourceRef="Task_NotifyRejected" targetRef="EndEvent_Rejected" />
    
  </bpmn:process>
</bpmn:definitions>
```

### Background Job - Exception Expiry

```csharp
// Jobs/SodExceptionExpiryJob.cs
using Hangfire;
using IntelliFin.Admin.Data;
using IntelliFin.Shared.Keycloak;
using IntelliFin.Shared.Audit;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.Admin.Jobs
{
    public class SodExceptionExpiryJob
    {
        private readonly AdminDbContext _dbContext;
        private readonly IKeycloakAdminClient _keycloakClient;
        private readonly IAuditService _auditService;
        private readonly ILogger<SodExceptionExpiryJob> _logger;

        public SodExceptionExpiryJob(
            AdminDbContext dbContext,
            IKeycloakAdminClient keycloakClient,
            IAuditService auditService,
            ILogger<SodExceptionExpiryJob> logger)
        {
            _dbContext = dbContext;
            _keycloakClient = keycloakClient;
            _auditService = auditService;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task CheckExpiredExceptionsAsync()
        {
            _logger.LogInformation("Checking for expired SoD exceptions...");

            var expiredExceptions = await _dbContext.SodExceptions
                .Where(e => e.Status == "Active" && e.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync();

            if (!expiredExceptions.Any())
            {
                _logger.LogInformation("No expired SoD exceptions found");
                return;
            }

            _logger.LogInformation("Found {Count} expired SoD exceptions", expiredExceptions.Count);

            foreach (var exception in expiredExceptions)
            {
                try
                {
                    // Remove role from Keycloak
                    await _keycloakClient.RemoveRoleFromUserAsync(
                        exception.UserId,
                        exception.RequestedRole,
                        default);

                    // Remove exception metadata
                    await _keycloakClient.RemoveUserAttributeAsync(
                        exception.UserId,
                        $"sod_exception_{exception.ExceptionId}",
                        default);

                    // Update exception record
                    exception.Status = "Expired";
                    exception.ExpiredAt = DateTime.UtcNow;

                    // Audit log
                    await _auditService.LogAsync(new AuditEvent
                    {
                        Actor = "SYSTEM",
                        Action = "SodExceptionExpired",
                        EntityType = "SodException",
                        EntityId = exception.ExceptionId.ToString(),
                        CorrelationId = exception.CorrelationId,
                        EventData = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            userId = exception.UserId,
                            role = exception.RequestedRole,
                            duration = exception.ExceptionDuration,
                            expiredAt = DateTime.UtcNow
                        })
                    }, default);

                    _logger.LogInformation(
                        "Expired SoD exception processed: ExceptionId={ExceptionId}, User={UserId}",
                        exception.ExceptionId, exception.UserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to expire SoD exception: ExceptionId={ExceptionId}",
                        exception.ExceptionId);
                }
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("SoD exception expiry check complete");
        }
    }

    public static class SodExceptionExpiryJobExtensions
    {
        public static void RegisterSodExceptionExpiryJob(this IServiceProvider services)
        {
            RecurringJob.AddOrUpdate<SodExceptionExpiryJob>(
                "sod-exception-expiry-check",
                job => job.CheckExpiredExceptionsAsync(),
                Cron.Daily(2));  // Run daily at 2 AM
        }
    }
}
```

---

## Integration Verification

### IV1: Existing V1 Roles Preserved and Functional
**Verification Steps**:
1. Query Keycloak for all realm roles
2. Verify original 6-8 V1 roles still exist
3. Assign V1 role to user, verify JWT contains role
4. Test V1 role-protected endpoints (loan origination, approval)
5. Confirm no breaking changes to existing functionality

**Success Criteria**:
- All V1 roles present and functional
- Existing users with V1 roles unaffected
- V1 role-based authorization working correctly

### IV2: Users with Expanded Roles Tested Across All Services
**Verification Steps**:
1. Create test users with new expanded roles (Collections Officer, Treasury Officer, etc.)
2. Test access to IntelliFin services:
   - Loan Origination: Collections Officer can view overdue loans
   - Financial Service: Treasury Officer can initiate disbursements
   - Reporting Service: GL Accountant can generate financial reports
   - Admin Service: Auditor can view audit logs (read-only)
3. Verify role permissions enforced correctly across services

**Success Criteria**:
- New roles grant appropriate access across microservices
- Role-based authorization consistent across all services
- No unauthorized access detected

### IV3: SoD Policy Enforced at API Level
**Verification Steps**:
1. Attempt to assign conflicting roles via API (e.g., Loan Processor + Loan Approver)
2. Verify HTTP 409 Conflict returned with conflict details
3. Attempt to bypass SoD via direct Keycloak API (should be blocked by application layer)
4. Test SoD override with confirmation flag (High severity only)
5. Verify Critical severity conflicts cannot be overridden

**Success Criteria**:
- All SoD conflicts detected and blocked appropriately
- Exception workflow required for High severity conflicts
- Critical conflicts cannot be bypassed
- SoD enforcement consistent across Admin UI and API

---

## Testing Strategy

### Unit Tests

#### Test: SoD Conflict Detection
```csharp
[Fact]
public async Task AssignRole_SodConflict_ThrowsSodConflictException()
{
    // Arrange
    var dbContext = CreateInMemoryDbContext();
    dbContext.SodPolicies.Add(new SodPolicy
    {
        Role1 = "Loan Processor",
        Role2 = "Loan Approver",
        Severity = "Critical",
        Enabled = true
    });
    await dbContext.SaveChangesAsync();

    var keycloakMock = new Mock<IKeycloakAdminClient>();
    keycloakMock.Setup(k => k.GetUserRolesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<KeycloakRole>
        {
            new KeycloakRole { Name = "Loan Processor" }
        });

    var service = new RoleManagementService(dbContext, keycloakMock.Object, /* ... */);

    // Act & Assert
    await Assert.ThrowsAsync<SodConflictException>(
        () => service.AssignRoleAsync("user123", "Loan Approver", false, "admin123", "Admin", CancellationToken.None));
}
```

#### Test: Role Hierarchy Inheritance
```csharp
[Fact]
public async Task GetUserRoles_CeoRole_IncludesInheritedRoles()
{
    // Arrange
    var keycloakMock = new Mock<IKeycloakAdminClient>();
    keycloakMock.Setup(k => k.GetUserRolesAsync("ceo123", It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<KeycloakRole>
        {
            new KeycloakRole { Name = "CEO" },
            new KeycloakRole { Name = "Branch Manager" },  // Inherited
            new KeycloakRole { Name = "Loan Approver" }    // Inherited
        });

    var service = new RoleManagementService(/* ... */, keycloakMock.Object, /* ... */);

    // Act
    var userRoles = await service.GetUserRolesAsync("ceo123", CancellationToken.None);

    // Assert
    Assert.Contains(userRoles.Roles, r => r.RoleName == "CEO");
    Assert.Contains(userRoles.Roles, r => r.RoleName == "Branch Manager");
    Assert.Contains(userRoles.Roles, r => r.RoleName == "Loan Approver");
}
```

### Integration Tests

#### Test: End-to-End SoD Exception Workflow
```csharp
[Fact]
public async Task SodExceptionWorkflow_RequestApproveExpire_Success()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>();
    var client = factory.CreateClient();

    // Act 1: Request exception
    var requestPayload = new
    {
        userId = "user123",
        userName = "John Doe",
        requestedRole = "Loan Approver",
        conflictingRoles = new[] { "Loan Processor" },
        businessJustification = "Temporary coverage for absent Loan Approver during peak season. No other approvers available in branch.",
        exceptionDuration = 30
    };
    var requestResponse = await client.PostAsJsonAsync("/api/admin/sod/exception-request", requestPayload);
    requestResponse.EnsureSuccessStatusCode();
    var exceptionResponse = await requestResponse.Content.ReadFromJsonAsync<SodExceptionResponse>();

    // Act 2: Approve exception (as Compliance Officer)
    var approvalPayload = new { comments = "Approved for 30-day emergency coverage" };
    var approvalResponse = await client.PostAsJsonAsync(
        $"/api/admin/sod/exceptions/{exceptionResponse.ExceptionId}/approve",
        approvalPayload);
    approvalResponse.EnsureSuccessStatusCode();

    // Act 3: Verify exception is active
    var statusResponse = await client.GetAsync($"/api/admin/sod/exceptions/{exceptionResponse.ExceptionId}");
    var status = await statusResponse.Content.ReadFromJsonAsync<SodExceptionStatusDto>();

    // Assert
    Assert.Equal("Active", status.Status);
    Assert.True(status.ExpiresAt > DateTime.UtcNow);
    Assert.NotNull(status.ApprovedBy);
}
```

### Performance Tests

#### Test: SoD Validation Performance
```csharp
[Fact]
public async Task SodValidation_100ConcurrentRequests_CompletesQuickly()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>();
    var client = factory.CreateClient();
    var stopwatch = Stopwatch.StartNew();

    // Act
    var tasks = Enumerable.Range(1, 100).Select(async i =>
    {
        var payload = new { userId = $"user{i}", proposedRole = "Loan Approver" };
        var response = await client.PostAsJsonAsync("/api/admin/roles/validate-sod", payload);
        return response.IsSuccessStatusCode;
    });

    var results = await Task.WhenAll(tasks);
    stopwatch.Stop();

    // Assert
    Assert.True(results.All(r => r), "All requests should succeed");
    Assert.True(stopwatch.ElapsedMilliseconds < 5000, "100 validations should complete in <5 seconds");
}
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| SoD policy too restrictive | Users unable to perform job functions | Medium | Start with High severity for most conflicts (allows exceptions). Monitor exception request rate. Adjust policies based on operational feedback. |
| Exception approval delays | Users blocked from time-sensitive operations | Medium | Set SLA for Compliance Officer review (24 hours). Implement escalation to CEO after 48 hours. Provide emergency override process. |
| Role hierarchy misconfiguration | Users get unintended permissions | Low | Test role hierarchy thoroughly. Document inheritance chains clearly. Implement role assignment audit review. |
| SoD policy database out of sync with Keycloak | Conflicts not detected | Low | Validate SoD policies on startup. Implement daily reconciliation job. Alert on policy drift. |
| Exception expiry not detected | Users retain elevated access beyond expiry | Low | Idempotent expiry job. Run multiple times per day. Monitor for expired-but-active exceptions. Alert on expiry job failures. |

---

## Definition of Done

- [ ] Database schema created with SoD policies, exceptions, role definitions
- [ ] 13 new realm roles created in Keycloak
- [ ] Role hierarchy configured in Keycloak and database
- [ ] SoD validation logic implemented and tested
- [ ] API endpoints implemented with full validation
- [ ] Camunda BPMN process deployed
- [ ] Background expiry job scheduled and tested
- [ ] Admin UI role assignment interface with SoD warnings
- [ ] Admin UI SoD exception request and review screens
- [ ] Unit tests: >85% code coverage
- [ ] Integration tests: All workflows pass
- [ ] Performance test: SoD validation <100ms per request
- [ ] Security review: Role assignment authorization checks
- [ ] Audit events logged for all role and SoD activities
- [ ] SoD compliance report generation tested
- [ ] Documentation: Role definitions, SoD policy rationale, exception process
- [ ] Training materials for Compliance Officers

---

## Related Documentation

### PRD References
- **Lines 1134-1157**: Story 1.21 detailed requirements
- **Lines 1079-1243**: Phase 4 (Governance & Workflows) overview
- **FR6**: Expanded operational roles
- **FR7**: SoD enforcement

### Architecture References
- **Section 4.2**: Keycloak RBAC and Authorization
- **Section 4.3**: Keycloak Admin API usage
- **Section 5**: Camunda Workflow Integration

### External Documentation
- [Keycloak Composite Roles](https://www.keycloak.org/docs/latest/server_admin/#_composite-roles)
- [Keycloak Role Hierarchy](https://www.keycloak.org/docs/latest/server_admin/#_role_scope_mappings)
- [Bank of Zambia Compliance Requirements](https://www.boz.zm)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Review SoD policy matrix with Compliance Officer
- [ ] Validate role definitions align with job descriptions
- [ ] Confirm role hierarchy matches organizational chart
- [ ] Coordinate with HR on role assignment approval workflows
- [ ] Test Keycloak composite roles in staging environment
- [ ] Plan role migration strategy for existing users
- [ ] Document exception request SLA with business stakeholders

### Post-Implementation Handoff
- [ ] Train Compliance Officers on exception review process
- [ ] Demo SoD conflict detection to System Administrators
- [ ] Create role definition reference guide for admins
- [ ] Set up monitoring for SoD exception request rate
- [ ] Schedule quarterly SoD policy review with Compliance
- [ ] Add SoD metrics to governance dashboard
- [ ] Review exception approval patterns (identify policy adjustments)

### Technical Debt / Future Enhancements
- [ ] Implement ML-based SoD risk scoring (predict high-risk exceptions)
- [ ] Add temporary role elevation (time-bound role assignment without exception workflow)
- [ ] Create role recommendation engine (suggest roles based on job title)
- [ ] Implement automated role recertification (quarterly review)
- [ ] Add SoD simulation tool (test policy changes before deployment)
- [ ] Create visual role hierarchy diagram in Admin UI

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: [Story 1.22: Policy-Driven Configuration Management](./story-1.22-policy-config-management.md)
