# Story 1.24: Quarterly Access Recertification Workflows

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.24 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 4: Governance & Workflows |
| **Sprint** | Sprint 8 |
| **Story Points** | 13 |
| **Estimated Effort** | 8-12 days |
| **Priority** | P1 (High - Compliance) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Story 1.19 (Camunda workflows), Story 1.21 (Role definitions), Identity Service |
| **Blocks** | Compliance audit readiness |

---

## User Story

**As a** Compliance Officer,  
**I want** user access rights to be recertified quarterly by managers,  
**so that** we maintain least-privilege access and meet BoZ regulatory requirements.

---

## Business Value

Quarterly access recertification provides critical compliance and security benefits:

- **Regulatory Compliance**: Meets BoZ requirements for periodic access reviews (quarterly frequency)
- **Least Privilege**: Identifies and removes unnecessary access rights over time
- **Audit Evidence**: Automated collection of recertification evidence for regulatory audits
- **Risk Reduction**: Detects and remediates privilege creep and orphaned accounts
- **Accountability**: Ensures managers actively review and approve their team's access
- **Automation**: Reduces manual effort through Camunda workflow automation

This story is **essential** for maintaining regulatory compliance and passing audits.

---

## Acceptance Criteria

### AC1: Quarterly Recertification Campaign Scheduling
**Given** Access recertification is required quarterly  
**When** scheduling recertification campaigns  
**Then**:
- Scheduled job runs on first day of each quarter (Jan 1, Apr 1, Jul 1, Oct 1)
- Campaign created with:
  - Unique campaign ID (e.g., `RECERT-2025-Q1`)
  - Campaign name (e.g., "Q1 2025 Access Recertification")
  - Start date (first day of quarter)
  - Due date (30 days from start)
  - Status: `Active`
- All active users with roles/permissions included in campaign
- Camunda process instance started for campaign
- Campaign record stored in database
- Notification sent to compliance team

### AC2: Manager Task Assignment
**Given** Recertification campaign is active  
**When** assigning review tasks to managers  
**Then**:
- User-manager relationships determined from Identity Service
- One review task created per manager
- Each task includes:
  - List of direct reports requiring recertification
  - Current roles and permissions for each user
  - Last recertification date
  - Risk indicators (privileged access, SoD violations)
- Task assigned in Camunda with 30-day deadline
- Manager receives email notification with task summary
- Manager UI task list displays pending recertification tasks

### AC3: Manager Review UI - User Access Review
**Given** Manager has pending recertification task  
**When** reviewing team member access  
**Then**:
- Admin UI displays recertification review page with:
  - Campaign name and deadline
  - List of direct reports
  - For each user:
    - User name, email, department, job title
    - Current roles (e.g., Loan Officer, Approver)
    - Current permissions (e.g., Create Loan, Approve Loan)
    - Last login date
    - Access granted date
    - Risk indicators (High Privilege, SoD Violation)
    - Review decision radio buttons: **Approve**, **Revoke**, **Modify**
    - Comments field (required if revoking/modifying)
- Manager can filter by risk level, role, last login
- Manager can bulk approve low-risk users
- Manager can drill down into detailed permission view

### AC4: Access Approval Decision
**Given** Manager reviews user access  
**When** manager approves access  
**Then**:
- Decision recorded: `Approved`
- User retains all current roles and permissions
- Approval timestamp and manager ID stored
- User recertification status: `Certified`
- Next recertification date set (3 months from campaign date)
- Audit event logged: `AccessRecertified`

### AC5: Access Revocation Decision
**Given** Manager reviews user access  
**When** manager revokes access  
**Then**:
- Decision recorded: `Revoked`
- Comments required (min 20 characters)
- User roles/permissions marked for revocation
- Revocation effective date: End of campaign (30 days)
- User notified of pending revocation with appeal option
- HR notified if access revocation impacts job duties
- Revocation ticket created in Admin Service
- Audit event logged: `AccessRevoked`

### AC6: Access Modification Decision
**Given** Manager reviews user access  
**When** manager requests access modification  
**Then**:
- Decision recorded: `Modified`
- Comments required explaining modification rationale
- Manager selects specific roles/permissions to revoke
- Remaining roles/permissions approved
- Modification effective date: End of campaign (30 days)
- User notified of pending modifications
- Change request created in Admin Service
- Audit event logged: `AccessModified`

### AC7: Escalation for Overdue Reviews
**Given** Manager has pending recertification task  
**When** task is overdue (30 days past start)  
**Then**:
- Reminder email sent to manager at 20 days, 25 days, 28 days
- At 30 days:
  - Task escalated to manager's manager
  - Escalation email sent with original task details
  - Compliance team notified of escalation
  - Escalation counter incremented for manager (performance metric)
- If manager's manager doesn't respond in 7 days:
  - Task escalated to Compliance Officer
  - Auto-revoke option enabled (last resort)

### AC8: Campaign Completion and Reporting
**Given** All managers complete recertification reviews  
**When** campaign reaches completion  
**Then**:
- Campaign status updated: `Completed`
- Completion metrics calculated:
  - Total users reviewed
  - Access approved count/percentage
  - Access revoked count/percentage
  - Access modified count/percentage
  - Average review time per manager
  - Escalation count
- Compliance dashboard updated with campaign results
- Compliance report generated (PDF):
  - Executive summary
  - Detailed review results by department
  - Risk findings (SoD violations, high privilege users)
  - Revocation/modification summary
- Report stored in document management system
- Report available for audit evidence

### AC9: Audit Evidence Collection
**Given** Recertification campaign completed  
**When** collecting audit evidence  
**Then**:
- Audit package includes:
  - Campaign configuration (dates, scope)
  - Manager task assignments
  - Review decisions with timestamps and comments
  - Escalations and resolutions
  - Revocation/modification tickets
  - Compliance report PDF
- All evidence stored immutably (tamper-proof)
- Evidence retrievable by campaign ID
- Evidence retention: 7 years (regulatory requirement)
- Audit trail shows who accessed evidence and when

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Lines 1209-1232 (Story 1.24), Phase 4 Overview  
**Architecture Sections**: Section 5 (Camunda Workflows), Section 4.1 (Admin Service), Section 3.1 (Identity Service)  
**Requirements**: FR11 (Quarterly access recertification), NFR13 (95% manager completion rate)

### Technology Stack

- **Workflow Engine**: Camunda 8 (Zeebe)
- **Database**: SQL Server 2022 (Admin Service)
- **Scheduling**: Quartz.NET (quarterly campaign scheduling)
- **Notifications**: SendGrid (email), Slack webhooks
- **Reporting**: Telerik Reporting, iTextSharp (PDF generation)
- **Frontend**: React with manager review UI

### Database Schema

```sql
-- Admin Service Database

CREATE TABLE RecertificationCampaigns (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CampaignId NVARCHAR(50) NOT NULL UNIQUE,  -- RECERT-2025-Q1
    CampaignName NVARCHAR(200) NOT NULL,
    Quarter INT NOT NULL,  -- 1, 2, 3, 4
    Year INT NOT NULL,
    StartDate DATE NOT NULL,
    DueDate DATE NOT NULL,
    CompletionDate DATETIME2 NULL,
    Status NVARCHAR(50) NOT NULL,  -- Active, Completed, Cancelled
    
    TotalUsersInScope INT NOT NULL DEFAULT 0,
    UsersReviewed INT NOT NULL DEFAULT 0,
    UsersApproved INT NOT NULL DEFAULT 0,
    UsersRevoked INT NOT NULL DEFAULT 0,
    UsersModified INT NOT NULL DEFAULT 0,
    EscalationCount INT NOT NULL DEFAULT 0,
    
    CamundaProcessInstanceId NVARCHAR(100),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy NVARCHAR(100),
    CompletedBy NVARCHAR(100),
    
    INDEX IX_CampaignId (CampaignId),
    INDEX IX_Status (Status),
    INDEX IX_Quarter_Year (Quarter, Year)
);

CREATE TABLE RecertificationTasks (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    TaskId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    CampaignId NVARCHAR(50) NOT NULL,
    
    ManagerUserId NVARCHAR(100) NOT NULL,
    ManagerName NVARCHAR(200) NOT NULL,
    ManagerEmail NVARCHAR(200) NOT NULL,
    
    AssignedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    DueDate DATE NOT NULL,
    CompletedAt DATETIME2 NULL,
    Status NVARCHAR(50) NOT NULL,  -- Pending, InProgress, Completed, Overdue, Escalated
    
    UsersInScope INT NOT NULL,  -- Count of direct reports
    UsersReviewed INT NOT NULL DEFAULT 0,
    
    RemindersSent INT NOT NULL DEFAULT 0,
    LastReminderAt DATETIME2 NULL,
    EscalatedTo NVARCHAR(100) NULL,  -- Manager's manager or Compliance Officer
    EscalatedAt DATETIME2 NULL,
    
    CamundaTaskId NVARCHAR(100),
    
    FOREIGN KEY (CampaignId) REFERENCES RecertificationCampaigns(CampaignId),
    INDEX IX_TaskId (TaskId),
    INDEX IX_ManagerUserId (ManagerUserId),
    INDEX IX_Status (Status),
    INDEX IX_DueDate (DueDate)
);

CREATE TABLE RecertificationReviews (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ReviewId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    TaskId UNIQUEIDENTIFIER NOT NULL,
    CampaignId NVARCHAR(50) NOT NULL,
    
    UserId NVARCHAR(100) NOT NULL,  -- User being reviewed
    UserName NVARCHAR(200) NOT NULL,
    UserEmail NVARCHAR(200) NOT NULL,
    UserDepartment NVARCHAR(100),
    UserJobTitle NVARCHAR(100),
    
    CurrentRoles NVARCHAR(MAX),  -- JSON array of role IDs
    CurrentPermissions NVARCHAR(MAX),  -- JSON array of permission IDs
    LastLoginDate DATETIME2,
    AccessGrantedDate DATETIME2,
    
    RiskLevel NVARCHAR(20),  -- Low, Medium, High, Critical
    RiskIndicators NVARCHAR(MAX),  -- JSON array of risk flags
    
    Decision NVARCHAR(50),  -- Approved, Revoked, Modified, Pending
    DecisionComments NVARCHAR(1000),
    DecisionMadeBy NVARCHAR(100),
    DecisionMadeAt DATETIME2,
    
    RolesToRevoke NVARCHAR(MAX),  -- JSON array (if Modified decision)
    EffectiveDate DATE,  -- Date when revocation/modification takes effect
    
    AppealsSubmitted INT NOT NULL DEFAULT 0,
    AppealStatus NVARCHAR(50),  -- None, Pending, Approved, Denied
    
    FOREIGN KEY (TaskId) REFERENCES RecertificationTasks(TaskId),
    FOREIGN KEY (CampaignId) REFERENCES RecertificationCampaigns(CampaignId),
    INDEX IX_ReviewId (ReviewId),
    INDEX IX_UserId (UserId),
    INDEX IX_Decision (Decision),
    INDEX IX_RiskLevel (RiskLevel)
);

CREATE TABLE RecertificationEscalations (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    EscalationId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    TaskId UNIQUEIDENTIFIER NOT NULL,
    CampaignId NVARCHAR(50) NOT NULL,
    
    OriginalManagerUserId NVARCHAR(100) NOT NULL,
    EscalatedToUserId NVARCHAR(100) NOT NULL,
    EscalationType NVARCHAR(50) NOT NULL,  -- ManagerManagerEscalation, ComplianceOfficerEscalation
    
    EscalatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ResolvedAt DATETIME2 NULL,
    Resolution NVARCHAR(50),  -- Completed, AutoRevoked, Overridden
    ResolutionComments NVARCHAR(500),
    
    FOREIGN KEY (TaskId) REFERENCES RecertificationTasks(TaskId),
    INDEX IX_TaskId (TaskId),
    INDEX IX_EscalatedToUserId (EscalatedToUserId)
);

CREATE TABLE RecertificationReports (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ReportId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    CampaignId NVARCHAR(50) NOT NULL,
    
    ReportType NVARCHAR(50) NOT NULL,  -- ComplianceReport, AuditEvidence, ExecutiveSummary
    ReportFormat NVARCHAR(20) NOT NULL,  -- PDF, Excel
    
    FilePath NVARCHAR(500),  -- Path to stored report file
    FileSize BIGINT,
    GeneratedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    GeneratedBy NVARCHAR(100),
    
    AccessedCount INT NOT NULL DEFAULT 0,
    LastAccessedAt DATETIME2 NULL,
    LastAccessedBy NVARCHAR(100),
    
    RetentionDate DATE NOT NULL,  -- 7 years from generation
    
    FOREIGN KEY (CampaignId) REFERENCES RecertificationCampaigns(CampaignId),
    INDEX IX_CampaignId (CampaignId),
    INDEX IX_ReportType (ReportType)
);

-- View for campaign dashboard
CREATE VIEW vw_RecertificationCampaignSummary AS
SELECT 
    c.CampaignId,
    c.CampaignName,
    c.StartDate,
    c.DueDate,
    c.Status,
    c.TotalUsersInScope,
    c.UsersReviewed,
    c.UsersApproved,
    c.UsersRevoked,
    c.UsersModified,
    CAST(c.UsersReviewed AS FLOAT) / NULLIF(c.TotalUsersInScope, 0) * 100 AS CompletionPercentage,
    COUNT(t.Id) AS ManagerTaskCount,
    SUM(CASE WHEN t.Status = 'Completed' THEN 1 ELSE 0 END) AS CompletedTaskCount,
    SUM(CASE WHEN t.Status = 'Overdue' THEN 1 ELSE 0 END) AS OverdueTaskCount,
    c.EscalationCount
FROM RecertificationCampaigns c
LEFT JOIN RecertificationTasks t ON c.CampaignId = t.CampaignId
GROUP BY 
    c.CampaignId, c.CampaignName, c.StartDate, c.DueDate, c.Status,
    c.TotalUsersInScope, c.UsersReviewed, c.UsersApproved, 
    c.UsersRevoked, c.UsersModified, c.EscalationCount;
GO
```

### Camunda BPMN Process

```xml
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:zeebe="http://camunda.org/schema/zeebe/1.0"
                  id="recertification-campaign"
                  targetNamespace="http://intellifin.local/bpmn">
  
  <bpmn:process id="recertification-campaign" name="Quarterly Access Recertification Campaign" isExecutable="true">
    
    <!-- Start Event (Scheduled Quarterly) -->
    <bpmn:startEvent id="StartEvent_QuarterlyCampaign" name="Quarterly Campaign Started">
      <bpmn:outgoing>Flow_ToCreateCampaign</bpmn:outgoing>
    </bpmn:startEvent>
    
    <!-- Create Campaign Service Task -->
    <bpmn:serviceTask id="Task_CreateCampaign" name="Create Recertification Campaign">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="create-recertification-campaign" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToCreateCampaign</bpmn:incoming>
      <bpmn:outgoing>Flow_ToAssignTasks</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Assign Manager Tasks Service Task -->
    <bpmn:serviceTask id="Task_AssignManagerTasks" name="Assign Review Tasks to Managers">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="assign-manager-recertification-tasks" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToAssignTasks</bpmn:incoming>
      <bpmn:outgoing>Flow_ToWaitReviews</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Multi-Instance Manager Review (Parallel) -->
    <bpmn:subProcess id="SubProcess_ManagerReviews" name="Manager Reviews (Multi-Instance)">
      <bpmn:incoming>Flow_ToWaitReviews</bpmn:incoming>
      <bpmn:outgoing>Flow_ToCompleteCampaign</bpmn:outgoing>
      
      <bpmn:multiInstanceLoopCharacteristics isSequential="false">
        <bpmn:loopCardinality>${managerCount}</bpmn:loopCardinality>
      </bpmn:multiInstanceLoopCharacteristics>
      
      <!-- Manager Review User Task -->
      <bpmn:userTask id="Task_ManagerReview" name="Review Team Access">
        <bpmn:extensionElements>
          <zeebe:assignmentDefinition assignee="${managerId}" />
        </bpmn:extensionElements>
        <bpmn:incoming>Flow_ToReview</bpmn:incoming>
        <bpmn:outgoing>Flow_ToCheckCompletion</bpmn:outgoing>
      </bpmn:userTask>
      
      <!-- Timer for Reminder -->
      <bpmn:boundaryEvent id="Timer_Reminder" name="20 Days" attachedToRef="Task_ManagerReview">
        <bpmn:outgoing>Flow_ToSendReminder</bpmn:outgoing>
        <bpmn:timerEventDefinition>
          <bpmn:timeDuration>P20D</bpmn:timeDuration>
        </bpmn:timerEventDefinition>
      </bpmn:boundaryEvent>
      
      <!-- Timer for Escalation -->
      <bpmn:boundaryEvent id="Timer_Escalation" name="30 Days" attachedToRef="Task_ManagerReview">
        <bpmn:outgoing>Flow_ToEscalate</bpmn:outgoing>
        <bpmn:timerEventDefinition>
          <bpmn:timeDuration>P30D</bpmn:timeDuration>
        </bpmn:timerEventDefinition>
      </bpmn:boundaryEvent>
      
      <!-- Send Reminder Service Task -->
      <bpmn:serviceTask id="Task_SendReminder" name="Send Reminder Email">
        <bpmn:extensionElements>
          <zeebe:taskDefinition type="send-recertification-reminder" />
        </bpmn:extensionElements>
        <bpmn:incoming>Flow_ToSendReminder</bpmn:incoming>
      </bpmn:serviceTask>
      
      <!-- Escalate Service Task -->
      <bpmn:serviceTask id="Task_Escalate" name="Escalate to Manager's Manager">
        <bpmn:extensionElements>
          <zeebe:taskDefinition type="escalate-recertification-task" />
        </bpmn:extensionElements>
        <bpmn:incoming>Flow_ToEscalate</bpmn:incoming>
      </bpmn:serviceTask>
    </bpmn:subProcess>
    
    <!-- Complete Campaign Service Task -->
    <bpmn:serviceTask id="Task_CompleteCampaign" name="Complete Campaign and Generate Reports">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="complete-recertification-campaign" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToCompleteCampaign</bpmn:incoming>
      <bpmn:outgoing>Flow_ToEnd</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- End Event -->
    <bpmn:endEvent id="EndEvent_CampaignCompleted" name="Campaign Completed">
      <bpmn:incoming>Flow_ToEnd</bpmn:incoming>
    </bpmn:endEvent>
    
    <!-- Sequence Flows -->
    <bpmn:sequenceFlow id="Flow_ToCreateCampaign" sourceRef="StartEvent_QuarterlyCampaign" targetRef="Task_CreateCampaign" />
    <bpmn:sequenceFlow id="Flow_ToAssignTasks" sourceRef="Task_CreateCampaign" targetRef="Task_AssignManagerTasks" />
    <bpmn:sequenceFlow id="Flow_ToWaitReviews" sourceRef="Task_AssignManagerTasks" targetRef="SubProcess_ManagerReviews" />
    <bpmn:sequenceFlow id="Flow_ToCompleteCampaign" sourceRef="SubProcess_ManagerReviews" targetRef="Task_CompleteCampaign" />
    <bpmn:sequenceFlow id="Flow_ToEnd" sourceRef="Task_CompleteCampaign" targetRef="EndEvent_CampaignCompleted" />
    
  </bpmn:process>
</bpmn:definitions>
```

### API Endpoints

```csharp
// Controllers/RecertificationController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IntelliFin.Admin.Services;
using IntelliFin.Admin.Models;

namespace IntelliFin.Admin.Controllers
{
    [ApiController]
    [Route("api/admin/recertification")]
    public class RecertificationController : ControllerBase
    {
        private readonly IRecertificationService _recertificationService;
        private readonly ILogger<RecertificationController> _logger;

        public RecertificationController(
            IRecertificationService recertificationService,
            ILogger<RecertificationController> logger)
        {
            _recertificationService = recertificationService;
            _logger = logger;
        }

        /// <summary>
        /// Get active recertification campaigns
        /// </summary>
        [HttpGet("campaigns")]
        [Authorize(Roles = "System Administrator,Compliance Officer,Manager")]
        [ProducesResponseType(typeof(List<RecertificationCampaignSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCampaigns(
            [FromQuery] string? status = null,
            CancellationToken cancellationToken = default)
        {
            var campaigns = await _recertificationService.GetCampaignsAsync(status, cancellationToken);
            return Ok(campaigns);
        }

        /// <summary>
        /// Get manager's pending recertification tasks
        /// </summary>
        [HttpGet("tasks/my-tasks")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(typeof(List<RecertificationTaskDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyTasks(CancellationToken cancellationToken)
        {
            var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tasks = await _recertificationService.GetManagerTasksAsync(managerId, cancellationToken);
            return Ok(tasks);
        }

        /// <summary>
        /// Get users in scope for a recertification task
        /// </summary>
        [HttpGet("tasks/{taskId}/users")]
        [Authorize(Roles = "Manager,System Administrator")]
        [ProducesResponseType(typeof(List<RecertificationUserReviewDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTaskUsers(
            Guid taskId,
            CancellationToken cancellationToken)
        {
            var users = await _recertificationService.GetTaskUsersAsync(taskId, cancellationToken);
            return Ok(users);
        }

        /// <summary>
        /// Submit review decision for a user
        /// </summary>
        [HttpPost("reviews")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SubmitReviewDecision(
            [FromBody] RecertificationReviewDecisionDto decision,
            CancellationToken cancellationToken)
        {
            var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var managerName = User.FindFirstValue(ClaimTypes.Name);

            try
            {
                await _recertificationService.SubmitReviewDecisionAsync(
                    decision,
                    managerId,
                    managerName,
                    cancellationToken);

                return Ok(new { message = "Review decision submitted successfully" });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Bulk approve low-risk users
        /// </summary>
        [HttpPost("reviews/bulk-approve")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> BulkApprove(
            [FromBody] BulkApprovalRequest request,
            CancellationToken cancellationToken)
        {
            var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var result = await _recertificationService.BulkApproveAsync(
                request.TaskId,
                request.UserIds,
                managerId,
                cancellationToken);

            return Ok(new
            {
                message = $"Bulk approval completed: {result.ApprovedCount} users approved",
                approvedCount = result.ApprovedCount,
                failedCount = result.FailedCount
            });
        }

        /// <summary>
        /// Complete recertification task
        /// </summary>
        [HttpPost("tasks/{taskId}/complete")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CompleteTask(
            Guid taskId,
            CancellationToken cancellationToken)
        {
            var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                await _recertificationService.CompleteTaskAsync(taskId, managerId, cancellationToken);
                return Ok(new { message = "Recertification task completed successfully" });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Generate compliance report for campaign
        /// </summary>
        [HttpPost("campaigns/{campaignId}/generate-report")]
        [Authorize(Roles = "Compliance Officer,System Administrator")]
        [ProducesResponseType(typeof(RecertificationReportDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GenerateReport(
            string campaignId,
            [FromQuery] string reportType = "ComplianceReport",
            CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var report = await _recertificationService.GenerateReportAsync(
                campaignId,
                reportType,
                userId,
                cancellationToken);

            return Ok(report);
        }

        /// <summary>
        /// Download compliance report PDF
        /// </summary>
        [HttpGet("reports/{reportId}/download")]
        [Authorize(Roles = "Compliance Officer,System Administrator,Auditor")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> DownloadReport(
            Guid reportId,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var report = await _recertificationService.GetReportAsync(reportId, userId, cancellationToken);

            if (report == null)
                return NotFound();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(report.FilePath, cancellationToken);
            return File(fileBytes, "application/pdf", $"{report.CampaignId}_ComplianceReport.pdf");
        }

        /// <summary>
        /// Get campaign statistics for dashboard
        /// </summary>
        [HttpGet("campaigns/{campaignId}/statistics")]
        [Authorize(Roles = "Compliance Officer,System Administrator")]
        [ProducesResponseType(typeof(CampaignStatisticsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCampaignStatistics(
            string campaignId,
            CancellationToken cancellationToken)
        {
            var stats = await _recertificationService.GetCampaignStatisticsAsync(campaignId, cancellationToken);
            return Ok(stats);
        }
    }
}
```

### Service Implementation

```csharp
// Services/RecertificationService.cs
using IntelliFin.Admin.Data;
using IntelliFin.Admin.Models;
using IntelliFin.Shared.Camunda;
using IntelliFin.Shared.Identity;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.Admin.Services
{
    public interface IRecertificationService
    {
        Task<List<RecertificationCampaignSummaryDto>> GetCampaignsAsync(string? status, CancellationToken cancellationToken);
        Task<List<RecertificationTaskDto>> GetManagerTasksAsync(string managerId, CancellationToken cancellationToken);
        Task<List<RecertificationUserReviewDto>> GetTaskUsersAsync(Guid taskId, CancellationToken cancellationToken);
        Task SubmitReviewDecisionAsync(
            RecertificationReviewDecisionDto decision,
            string managerId,
            string managerName,
            CancellationToken cancellationToken);
        Task<BulkApprovalResult> BulkApproveAsync(
            Guid taskId,
            List<string> userIds,
            string managerId,
            CancellationToken cancellationToken);
        Task CompleteTaskAsync(Guid taskId, string managerId, CancellationToken cancellationToken);
        Task<RecertificationReportDto> GenerateReportAsync(
            string campaignId,
            string reportType,
            string userId,
            CancellationToken cancellationToken);
        Task<CampaignStatisticsDto> GetCampaignStatisticsAsync(string campaignId, CancellationToken cancellationToken);
    }

    public class RecertificationService : IRecertificationService
    {
        private readonly AdminDbContext _dbContext;
        private readonly IIdentityServiceClient _identityClient;
        private readonly ICamundaClient _camundaClient;
        private readonly INotificationService _notificationService;
        private readonly IReportGenerationService _reportService;
        private readonly IAuditService _auditService;
        private readonly ILogger<RecertificationService> _logger;

        public RecertificationService(
            AdminDbContext dbContext,
            IIdentityServiceClient identityClient,
            ICamundaClient camundaClient,
            INotificationService notificationService,
            IReportGenerationService reportService,
            IAuditService auditService,
            ILogger<RecertificationService> logger)
        {
            _dbContext = dbContext;
            _identityClient = identityClient;
            _camundaClient = camundaClient;
            _notificationService = notificationService;
            _reportService = reportService;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<List<RecertificationUserReviewDto>> GetTaskUsersAsync(
            Guid taskId,
            CancellationToken cancellationToken)
        {
            var task = await _dbContext.RecertificationTasks
                .FirstOrDefaultAsync(t => t.TaskId == taskId, cancellationToken);

            if (task == null)
                throw new NotFoundException("Recertification task not found");

            // Get all reviews for this task
            var reviews = await _dbContext.RecertificationReviews
                .Where(r => r.TaskId == taskId)
                .OrderBy(r => r.RiskLevel)  // High risk first
                .ThenBy(r => r.UserName)
                .ToListAsync(cancellationToken);

            var reviewDtos = reviews.Select(r => new RecertificationUserReviewDto
            {
                ReviewId = r.ReviewId,
                UserId = r.UserId,
                UserName = r.UserName,
                UserEmail = r.UserEmail,
                Department = r.UserDepartment,
                JobTitle = r.UserJobTitle,
                CurrentRoles = JsonSerializer.Deserialize<List<string>>(r.CurrentRoles ?? "[]"),
                CurrentPermissions = JsonSerializer.Deserialize<List<string>>(r.CurrentPermissions ?? "[]"),
                LastLoginDate = r.LastLoginDate,
                AccessGrantedDate = r.AccessGrantedDate,
                RiskLevel = r.RiskLevel,
                RiskIndicators = JsonSerializer.Deserialize<List<string>>(r.RiskIndicators ?? "[]"),
                Decision = r.Decision,
                DecisionComments = r.DecisionComments,
                DecisionMadeAt = r.DecisionMadeAt
            }).ToList();

            return reviewDtos;
        }

        public async Task SubmitReviewDecisionAsync(
            RecertificationReviewDecisionDto decision,
            string managerId,
            string managerName,
            CancellationToken cancellationToken)
        {
            // Validate decision
            if (decision.Decision == "Revoked" || decision.Decision == "Modified")
            {
                if (string.IsNullOrWhiteSpace(decision.Comments) || decision.Comments.Length < 20)
                    throw new ValidationException("Comments required (min 20 characters) for Revoke/Modify decisions");
            }

            var review = await _dbContext.RecertificationReviews
                .FirstOrDefaultAsync(r => r.ReviewId == decision.ReviewId, cancellationToken);

            if (review == null)
                throw new NotFoundException("Review not found");

            // Update review
            review.Decision = decision.Decision;
            review.DecisionComments = decision.Comments;
            review.DecisionMadeBy = managerId;
            review.DecisionMadeAt = DateTime.UtcNow;
            review.EffectiveDate = decision.EffectiveDate ?? DateTime.UtcNow.AddDays(30);

            if (decision.Decision == "Modified" && decision.RolesToRevoke?.Any() == true)
            {
                review.RolesToRevoke = JsonSerializer.Serialize(decision.RolesToRevoke);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Update task progress
            var task = await _dbContext.RecertificationTasks
                .FirstOrDefaultAsync(t => t.TaskId == review.TaskId, cancellationToken);

            if (task != null)
            {
                task.UsersReviewed = await _dbContext.RecertificationReviews
                    .CountAsync(r => r.TaskId == task.TaskId && r.Decision != "Pending", cancellationToken);

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Update campaign metrics
            var campaign = await _dbContext.RecertificationCampaigns
                .FirstOrDefaultAsync(c => c.CampaignId == review.CampaignId, cancellationToken);

            if (campaign != null)
            {
                campaign.UsersReviewed++;
                
                if (decision.Decision == "Approved")
                    campaign.UsersApproved++;
                else if (decision.Decision == "Revoked")
                    campaign.UsersRevoked++;
                else if (decision.Decision == "Modified")
                    campaign.UsersModified++;

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Audit log
            await _auditService.LogAsync(new AuditEvent
            {
                Actor = managerId,
                Action = $"AccessRecertification{decision.Decision}",
                EntityType = "RecertificationReview",
                EntityId = review.ReviewId.ToString(),
                EventData = JsonSerializer.Serialize(new
                {
                    userId = review.UserId,
                    decision = decision.Decision,
                    comments = decision.Comments,
                    campaignId = review.CampaignId
                })
            }, cancellationToken);

            // Notify user if access revoked or modified
            if (decision.Decision == "Revoked" || decision.Decision == "Modified")
            {
                await _notificationService.SendAccessChangeNotificationAsync(
                    review.UserId,
                    new AccessChangeNotificationDto
                    {
                        Decision = decision.Decision,
                        Comments = decision.Comments,
                        EffectiveDate = review.EffectiveDate.Value,
                        ManagerName = managerName
                    },
                    cancellationToken);
            }

            _logger.LogInformation(
                "Recertification decision submitted: ReviewId={ReviewId}, Decision={Decision}, Manager={ManagerId}",
                review.ReviewId, decision.Decision, managerId);
        }

        public async Task<BulkApprovalResult> BulkApproveAsync(
            Guid taskId,
            List<string> userIds,
            string managerId,
            CancellationToken cancellationToken)
        {
            var approvedCount = 0;
            var failedCount = 0;

            foreach (var userId in userIds)
            {
                try
                {
                    var review = await _dbContext.RecertificationReviews
                        .FirstOrDefaultAsync(r => r.TaskId == taskId && r.UserId == userId, cancellationToken);

                    if (review == null || review.RiskLevel == "High" || review.RiskLevel == "Critical")
                    {
                        failedCount++;
                        continue;
                    }

                    await SubmitReviewDecisionAsync(
                        new RecertificationReviewDecisionDto
                        {
                            ReviewId = review.ReviewId,
                            Decision = "Approved",
                            Comments = "Bulk approval - low risk user"
                        },
                        managerId,
                        "Bulk Approval",
                        cancellationToken);

                    approvedCount++;
                }
                catch
                {
                    failedCount++;
                }
            }

            return new BulkApprovalResult
            {
                ApprovedCount = approvedCount,
                FailedCount = failedCount
            };
        }

        public async Task CompleteTaskAsync(
            Guid taskId,
            string managerId,
            CancellationToken cancellationToken)
        {
            var task = await _dbContext.RecertificationTasks
                .FirstOrDefaultAsync(t => t.TaskId == taskId, cancellationToken);

            if (task == null)
                throw new NotFoundException("Task not found");

            // Validate all users reviewed
            var pendingReviews = await _dbContext.RecertificationReviews
                .CountAsync(r => r.TaskId == taskId && r.Decision == "Pending", cancellationToken);

            if (pendingReviews > 0)
                throw new ValidationException($"{pendingReviews} users still require review");

            task.Status = "Completed";
            task.CompletedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Complete Camunda task
            if (!string.IsNullOrEmpty(task.CamundaTaskId))
            {
                await _camundaClient.CompleteTaskAsync(
                    task.CamundaTaskId,
                    "manager-review",
                    new Dictionary<string, object>
                    {
                        { "completed", true },
                        { "managerId", managerId }
                    },
                    cancellationToken);
            }

            _logger.LogInformation(
                "Recertification task completed: TaskId={TaskId}, Manager={ManagerId}",
                taskId, managerId);
        }

        // Additional methods omitted for brevity
    }
}
```

### Scheduled Job - Campaign Creation

```csharp
// Jobs/QuarterlyRecertificationJob.cs
using Quartz;
using IntelliFin.Admin.Services;

namespace IntelliFin.Admin.Jobs
{
    [DisallowConcurrentExecution]
    public class QuarterlyRecertificationJob : IJob
    {
        private readonly IRecertificationCampaignService _campaignService;
        private readonly ILogger<QuarterlyRecertificationJob> _logger;

        public QuarterlyRecertificationJob(
            IRecertificationCampaignService campaignService,
            ILogger<QuarterlyRecertificationJob> logger)
        {
            _campaignService = campaignService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("Starting quarterly recertification campaign creation");

            try
            {
                var currentDate = DateTime.UtcNow;
                var quarter = (currentDate.Month - 1) / 3 + 1;
                var year = currentDate.Year;

                var campaignId = $"RECERT-{year}-Q{quarter}";
                var campaignName = $"Q{quarter} {year} Access Recertification";

                await _campaignService.CreateCampaignAsync(
                    campaignId,
                    campaignName,
                    quarter,
                    year,
                    CancellationToken.None);

                _logger.LogInformation(
                    "Recertification campaign created successfully: {CampaignId}",
                    campaignId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create quarterly recertification campaign");
                throw;
            }
        }
    }

    // Quartz configuration
    public static class QuartzConfiguration
    {
        public static void ConfigureQuarterlyRecertification(IServiceCollectionQuartzConfigurator quartz)
        {
            var jobKey = new JobKey("QuarterlyRecertificationJob");

            quartz.AddJob<QuarterlyRecertificationJob>(opts => opts.WithIdentity(jobKey));

            quartz.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("QuarterlyRecertificationTrigger")
                .WithCronSchedule("0 0 0 1 1,4,7,10 ?")  // First day of each quarter (Jan, Apr, Jul, Oct)
                .WithDescription("Quarterly Access Recertification Campaign"));
        }
    }
}
```

---

## Integration Verification

### IV1: Campaign Creation and Task Assignment
**Verification Steps**:
1. Trigger quarterly recertification job manually
2. Verify campaign created in database with correct quarter/year
3. Check manager tasks assigned based on org hierarchy
4. Verify each manager receives email notification
5. Verify Camunda process instance started
6. Check Admin UI displays active campaign

**Success Criteria**:
- Campaign created within 5 minutes
- All managers with direct reports receive tasks
- Email notifications sent successfully
- Camunda workflow running

### IV2: Manager Review Workflow
**Verification Steps**:
1. Manager logs into Admin UI
2. Navigate to "My Recertification Tasks"
3. Open task for Q1 2025 campaign
4. Review list of direct reports with risk indicators
5. Approve low-risk user
6. Revoke access for high-risk user with comments
7. Modify access for medium-risk user (remove specific role)
8. Complete task
9. Verify task marked complete in database
10. Verify users notified of decisions

**Success Criteria**:
- Manager UI displays all direct reports
- Risk indicators clearly visible
- Bulk approve works for low-risk users
- Comments required for Revoke/Modify
- Task completion updates campaign metrics

### IV3: Escalation Workflow
**Verification Steps**:
1. Create test campaign with 1-day deadline
2. Assign task to manager
3. Wait 1 day (or simulate time passage)
4. Verify reminder email sent to manager
5. Wait additional days to trigger escalation
6. Verify task escalated to manager's manager
7. Check escalation notification sent
8. Verify compliance team notified
9. Manager's manager completes task
10. Check escalation resolved

**Success Criteria**:
- Reminders sent at 20, 25, 28 days
- Escalation triggered at 30 days
- Escalation email includes original task details
- Compliance team notified of escalation

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task SubmitReviewDecision_Approved_Success()
{
    // Arrange
    var service = CreateService();
    var decision = new RecertificationReviewDecisionDto
    {
        ReviewId = Guid.NewGuid(),
        Decision = "Approved",
        Comments = "Access appropriate for role"
    };

    // Act
    await service.SubmitReviewDecisionAsync(decision, "MGR001", "Manager Name", CancellationToken.None);

    // Assert
    var review = await _dbContext.RecertificationReviews.FindAsync(decision.ReviewId);
    Assert.Equal("Approved", review.Decision);
    Assert.Equal("MGR001", review.DecisionMadeBy);
}

[Fact]
public async Task SubmitReviewDecision_RevokeWithoutComments_ThrowsValidationException()
{
    // Arrange
    var service = CreateService();
    var decision = new RecertificationReviewDecisionDto
    {
        ReviewId = Guid.NewGuid(),
        Decision = "Revoked",
        Comments = ""  // Missing required comments
    };

    // Act & Assert
    await Assert.ThrowsAsync<ValidationException>(() =>
        service.SubmitReviewDecisionAsync(decision, "MGR001", "Manager Name", CancellationToken.None));
}
```

### Integration Tests

```csharp
[Fact]
public async Task RecertificationCampaign_EndToEnd_Success()
{
    // Arrange
    var campaignService = CreateCampaignService();

    // Act 1: Create campaign
    var campaignId = await campaignService.CreateCampaignAsync(
        "RECERT-2025-Q1", "Q1 2025 Test", 1, 2025, CancellationToken.None);

    // Act 2: Get manager tasks
    var tasks = await campaignService.GetManagerTasksAsync("MGR001", CancellationToken.None);
    Assert.Single(tasks);

    // Act 3: Submit review decisions
    var users = await campaignService.GetTaskUsersAsync(tasks[0].TaskId, CancellationToken.None);
    foreach (var user in users)
    {
        await campaignService.SubmitReviewDecisionAsync(
            new RecertificationReviewDecisionDto
            {
                ReviewId = user.ReviewId,
                Decision = "Approved",
                Comments = "Test approval"
            },
            "MGR001",
            "Test Manager",
            CancellationToken.None);
    }

    // Act 4: Complete task
    await campaignService.CompleteTaskAsync(tasks[0].TaskId, "MGR001", CancellationToken.None);

    // Assert
    var campaign = await _dbContext.RecertificationCampaigns
        .FirstAsync(c => c.CampaignId == campaignId);
    Assert.Equal(users.Count, campaign.UsersApproved);
}
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| Low manager completion rate | Failed compliance audit | Medium | Implement escalation workflow. Dashboard showing manager performance. Executive reporting of non-compliance. |
| Managers approve all access without review | Security risk | Medium | Flag suspicious patterns (100% approval rate). Audit random sample of reviews. Require MFA for bulk operations. |
| Org hierarchy data incorrect | Wrong managers assigned | Low | Validate org hierarchy before campaign. Manual correction process. Manager can delegate to correct person. |
| Campaign creation job fails | Missed quarterly recertification | Low | Monitoring alerts on job failure. Retry logic. Manual campaign creation procedure. |
| Review decisions lost | Incomplete audit trail | Low | Transaction-based database operations. Audit log backup. Immutable evidence storage. |

---

## Definition of Done

- [ ] Database schema created for campaigns, tasks, reviews, escalations, reports
- [ ] Camunda BPMN process deployed for recertification workflow
- [ ] Quartz job scheduled for quarterly campaign creation
- [ ] API endpoints implemented for campaign and review management
- [ ] Manager review UI implemented in Admin portal
- [ ] Email notifications configured (task assignment, reminders, escalations)
- [ ] Bulk approval functionality implemented
- [ ] Escalation logic implemented (reminders, manager's manager, compliance officer)
- [ ] Report generation service implemented (PDF compliance reports)
- [ ] Compliance dashboard with campaign metrics
- [ ] Audit evidence collection and immutable storage
- [ ] Unit tests: >85% code coverage
- [ ] Integration tests: End-to-end campaign workflow
- [ ] Performance test: 1000+ users, 50+ managers
- [ ] Security review: Authorization checks, MFA requirements
- [ ] Documentation: Manager user guide, compliance officer procedures
- [ ] Training materials for managers and compliance team

---

## Related Documentation

### PRD References
- **Lines 1209-1232**: Story 1.24 detailed requirements
- **Lines 1079-1243**: Phase 4 (Governance & Workflows) overview
- **FR11**: Quarterly access recertification
- **NFR13**: 95% manager completion rate

### Architecture References
- **Section 5**: Camunda Workflow Integration
- **Section 4.1**: Admin Service Architecture
- **Section 3.1**: Identity Service Integration

### External Documentation
- [Camunda Multi-Instance](https://docs.camunda.io/docs/components/modeler/bpmn/multi-instance/)
- [Quartz.NET Scheduling](https://www.quartz-scheduler.net/)
- [Access Recertification Best Practices (NIST)](https://csrc.nist.gov/publications/detail/sp/800-53/rev-5/final)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Validate org hierarchy data in Identity Service
- [ ] Define risk scoring algorithm for users
- [ ] Create email templates for notifications
- [ ] Design manager review UI mockups
- [ ] Plan report template designs (PDF format)
- [ ] Configure Quartz job scheduler
- [ ] Set up Camunda process deployment pipeline
- [ ] Document escalation procedures

### Post-Implementation Handoff
- [ ] Train managers on review process
- [ ] Train compliance officers on reporting
- [ ] Create video tutorials for manager UI
- [ ] Schedule dry-run campaign before Q1
- [ ] Establish SLA for task completion (30 days)
- [ ] Set up executive dashboard for campaign metrics
- [ ] Document troubleshooting procedures
- [ ] Create incident response plan for campaign failures

### Technical Debt / Future Enhancements
- [ ] AI-powered risk scoring for users
- [ ] Predictive analytics for non-compliance risk
- [ ] Mobile app for manager reviews
- [ ] Integration with HR system for job changes
- [ ] Automated role recommendations based on job title
- [ ] Machine learning for anomaly detection (privilege creep)
- [ ] Self-service access request during recertification
- [ ] Real-time collaboration (multiple managers reviewing same user)

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: [Story 1.25: GitOps Configuration Deployment with ArgoCD](./story-1.25-gitops-argocd.md)
