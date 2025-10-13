using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.Controllers;

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

    [HttpGet("campaigns")]
    [Authorize(Roles = "System Administrator,Compliance Officer,Manager")]
    public async Task<IActionResult> GetCampaigns([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var campaigns = await _recertificationService.GetCampaignsAsync(status, cancellationToken);
        return Ok(campaigns);
    }

    [HttpGet("tasks/my-tasks")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> GetMyTasks(CancellationToken cancellationToken)
    {
        var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(managerId))
        {
            return Forbid();
        }

        var tasks = await _recertificationService.GetManagerTasksAsync(managerId, cancellationToken);
        return Ok(tasks);
    }

    [HttpGet("tasks/{taskId:guid}/users")]
    [Authorize(Roles = "Manager,System Administrator")]
    public async Task<IActionResult> GetTaskUsers(Guid taskId, CancellationToken cancellationToken)
    {
        try
        {
            var users = await _recertificationService.GetTaskUsersAsync(taskId, cancellationToken);
            return Ok(users);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Unable to find recertification task {TaskId}", taskId);
            return NotFound();
        }
    }

    [HttpPost("reviews")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> SubmitReviewDecision([FromBody] RecertificationReviewDecisionDto decision, CancellationToken cancellationToken)
    {
        var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(managerId))
        {
            return Forbid();
        }

        var managerName = User.Identity?.Name ?? managerId;

        try
        {
            await _recertificationService.SubmitReviewDecisionAsync(decision, managerId, managerName, cancellationToken);
            return Ok(new { message = "Review decision submitted successfully" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("reviews/bulk-approve")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> BulkApprove([FromBody] BulkApprovalRequest request, CancellationToken cancellationToken)
    {
        var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(managerId))
        {
            return Forbid();
        }

        try
        {
            var result = await _recertificationService.BulkApproveAsync(request.TaskId, request.UserIds, managerId, cancellationToken);
            return Ok(new
            {
                message = "Bulk approval completed",
                approvedCount = result.ApprovedCount,
                failedCount = result.FailedCount
            });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("tasks/{taskId:guid}/complete")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> CompleteTask(Guid taskId, CancellationToken cancellationToken)
    {
        var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(managerId))
        {
            return Forbid();
        }

        try
        {
            await _recertificationService.CompleteTaskAsync(taskId, managerId, cancellationToken);
            return Ok(new { message = "Recertification task completed successfully" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("campaigns/{campaignId}/generate-report")]
    [Authorize(Roles = "Compliance Officer,System Administrator")]
    public async Task<IActionResult> GenerateReport(string campaignId, [FromQuery] string reportType = "ComplianceReport", CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        try
        {
            var report = await _recertificationService.GenerateReportAsync(campaignId, reportType, userId, cancellationToken);
            return Ok(report);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("reports/{reportId:guid}/download")]
    [Authorize(Roles = "Compliance Officer,System Administrator,Auditor")]
    public async Task<IActionResult> DownloadReport(Guid reportId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "audit";
        var report = await _recertificationService.GetReportAsync(reportId, userId, cancellationToken);
        if (report == null || string.IsNullOrWhiteSpace(report.FilePath) || !System.IO.File.Exists(report.FilePath))
        {
            return NotFound();
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(report.FilePath, cancellationToken);
        return File(fileBytes, "application/octet-stream", System.IO.Path.GetFileName(report.FilePath));
    }

    [HttpGet("campaigns/{campaignId}/statistics")]
    [Authorize(Roles = "Compliance Officer,System Administrator")]
    public async Task<IActionResult> GetCampaignStatistics(string campaignId, CancellationToken cancellationToken)
    {
        try
        {
            var stats = await _recertificationService.GetCampaignStatisticsAsync(campaignId, cancellationToken);
            return Ok(stats);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
