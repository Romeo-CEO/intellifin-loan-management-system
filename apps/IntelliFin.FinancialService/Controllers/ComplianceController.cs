using IntelliFin.FinancialService.Models;
using IntelliFin.FinancialService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace IntelliFin.FinancialService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComplianceController : ControllerBase
{
    private readonly IComplianceMonitoringService _complianceService;
    private readonly IBozComplianceService _bozComplianceService;
    private readonly ILogger<ComplianceController> _logger;

    public ComplianceController(
        IComplianceMonitoringService complianceService,
        IBozComplianceService bozComplianceService,
        ILogger<ComplianceController> logger)
    {
        _complianceService = complianceService;
        _bozComplianceService = bozComplianceService;
        _logger = logger;
    }

    /// <summary>
    /// Monitor all compliance rules for a branch
    /// </summary>
    [HttpGet("monitor")]
    [Authorize(Roles = "Compliance,Manager,Admin")]
    public async Task<IActionResult> MonitorCompliance([FromQuery] string? branchId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveBranchId = branchId ?? GetUserBranchId();
            
            if (string.IsNullOrEmpty(effectiveBranchId))
            {
                return BadRequest(new { message = "Branch ID is required" });
            }

            var result = await _complianceService.MonitorComplianceAsync(effectiveBranchId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring compliance for branch {BranchId}", branchId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Check a specific compliance rule
    /// </summary>
    [HttpGet("rules/{ruleId}/check")]
    [Authorize(Roles = "Compliance,Manager,Admin")]
    public async Task<IActionResult> CheckComplianceRule(string ruleId, [FromQuery] string? branchId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveBranchId = branchId ?? GetUserBranchId();
            
            if (string.IsNullOrEmpty(effectiveBranchId))
            {
                return BadRequest(new { message = "Branch ID is required" });
            }

            var result = await _complianceService.CheckComplianceRuleAsync(ruleId, effectiveBranchId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking compliance rule {RuleId} for branch {BranchId}", ruleId, branchId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get compliance dashboard metrics
    /// </summary>
    [HttpGet("dashboard")]
    [Authorize(Roles = "Compliance,Manager,Admin,Finance")]
    public async Task<IActionResult> GetComplianceDashboard([FromQuery] string? branchId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveBranchId = branchId ?? GetUserBranchId();
            
            if (string.IsNullOrEmpty(effectiveBranchId))
            {
                return BadRequest(new { message = "Branch ID is required" });
            }

            var dashboard = await _complianceService.GetComplianceDashboardAsync(effectiveBranchId, cancellationToken);
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance dashboard for branch {BranchId}", branchId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get compliance alerts for a branch
    /// </summary>
    [HttpGet("alerts")]
    [Authorize(Roles = "Compliance,Manager,Admin,Finance")]
    public async Task<IActionResult> GetComplianceAlerts(
        [FromQuery] string? branchId = null,
        [FromQuery] ComplianceAlertStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveBranchId = branchId ?? GetUserBranchId();
            
            if (string.IsNullOrEmpty(effectiveBranchId))
            {
                return BadRequest(new { message = "Branch ID is required" });
            }

            var alerts = await _complianceService.GetComplianceAlertsAsync(effectiveBranchId, status, cancellationToken);
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance alerts for branch {BranchId}", branchId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Acknowledge a compliance alert
    /// </summary>
    [HttpPost("alerts/{alertId}/acknowledge")]
    [Authorize(Roles = "Compliance,Manager,Admin")]
    public async Task<IActionResult> AcknowledgeAlert(string alertId, [FromBody] AcknowledgeAlertRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            await _complianceService.AcknowledgeAlertAsync(alertId, userId, request.Notes, cancellationToken);
            
            return Ok(new { message = "Alert acknowledged successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging compliance alert {AlertId}", alertId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Resolve a compliance alert
    /// </summary>
    [HttpPost("alerts/{alertId}/resolve")]
    [Authorize(Roles = "Compliance,Manager,Admin")]
    public async Task<IActionResult> ResolveAlert(string alertId, [FromBody] ResolveAlertRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            await _complianceService.ResolveAlertAsync(alertId, userId, request.ResolutionNotes, cancellationToken);
            
            return Ok(new { message = "Alert resolved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving compliance alert {AlertId}", alertId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Create a manual compliance alert
    /// </summary>
    [HttpPost("alerts")]
    [Authorize(Roles = "Compliance,Manager,Admin")]
    public async Task<IActionResult> CreateManualAlert([FromBody] CreateComplianceAlertRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var alertId = await _complianceService.CreateManualAlertAsync(request, cancellationToken);
            
            return CreatedAtAction(
                nameof(GetComplianceAlerts), 
                new { branchId = request.BranchId }, 
                new { alertId, message = "Manual alert created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating manual compliance alert for branch {BranchId}", request.BranchId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get all compliance rules
    /// </summary>
    [HttpGet("rules")]
    [Authorize(Roles = "Compliance,Manager,Admin")]
    public async Task<IActionResult> GetComplianceRules([FromQuery] ComplianceRuleCategory? category = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var rules = await _complianceService.GetComplianceRulesAsync(category, cancellationToken);
            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance rules");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Update compliance rule configuration
    /// </summary>
    [HttpPut("rules/{ruleId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateComplianceRule(string ruleId, [FromBody] UpdateComplianceRuleRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _complianceService.UpdateComplianceRuleAsync(ruleId, request, cancellationToken);
            
            return Ok(new { message = "Compliance rule updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating compliance rule {RuleId}", ruleId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Generate compliance report for a date range
    /// </summary>
    [HttpPost("reports/generate")]
    [Authorize(Roles = "Compliance,Manager,Admin")]
    public async Task<IActionResult> GenerateComplianceReport([FromBody] GenerateComplianceReportRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var effectiveBranchId = request.BranchId ?? GetUserBranchId();
            
            if (string.IsNullOrEmpty(effectiveBranchId))
            {
                return BadRequest(new { message = "Branch ID is required" });
            }

            var report = await _complianceService.GenerateComplianceReportAsync(
                request.StartDate, 
                request.EndDate, 
                effectiveBranchId, 
                cancellationToken);
            
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance report");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get compliance history for a branch
    /// </summary>
    [HttpGet("history")]
    [Authorize(Roles = "Compliance,Manager,Admin")]
    public async Task<IActionResult> GetComplianceHistory(
        [FromQuery] string? branchId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveBranchId = branchId ?? GetUserBranchId();
            
            if (string.IsNullOrEmpty(effectiveBranchId))
            {
                return BadRequest(new { message = "Branch ID is required" });
            }

            var history = await _complianceService.GetComplianceHistoryAsync(effectiveBranchId, startDate, endDate, cancellationToken);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance history for branch {BranchId}", branchId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    #region BoZ-Specific Compliance Checks

    /// <summary>
    /// Check Capital Adequacy Ratio compliance
    /// </summary>
    [HttpGet("boz/capital-adequacy")]
    [Authorize(Roles = "Compliance,Manager,Admin,Finance")]
    public async Task<IActionResult> CheckCapitalAdequacyRatio([FromQuery] string? branchId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveBranchId = branchId ?? GetUserBranchId();
            
            if (string.IsNullOrEmpty(effectiveBranchId))
            {
                return BadRequest(new { message = "Branch ID is required" });
            }

            var result = await _bozComplianceService.CheckCapitalAdequacyRatioAsync(effectiveBranchId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking capital adequacy ratio for branch {BranchId}", branchId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Check loan classification compliance
    /// </summary>
    [HttpGet("boz/loan-classification")]
    [Authorize(Roles = "Compliance,Manager,Admin")]
    public async Task<IActionResult> CheckLoanClassificationCompliance([FromQuery] string? branchId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveBranchId = branchId ?? GetUserBranchId();
            
            if (string.IsNullOrEmpty(effectiveBranchId))
            {
                return BadRequest(new { message = "Branch ID is required" });
            }

            var result = await _bozComplianceService.CheckLoanClassificationComplianceAsync(effectiveBranchId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking loan classification compliance for branch {BranchId}", branchId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Check provision coverage compliance
    /// </summary>
    [HttpGet("boz/provision-coverage")]
    [Authorize(Roles = "Compliance,Manager,Admin,Finance")]
    public async Task<IActionResult> CheckProvisionCoverage([FromQuery] string? branchId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveBranchId = branchId ?? GetUserBranchId();
            
            if (string.IsNullOrEmpty(effectiveBranchId))
            {
                return BadRequest(new { message = "Branch ID is required" });
            }

            var result = await _bozComplianceService.CheckProvisionCoverageAsync(effectiveBranchId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking provision coverage for branch {BranchId}", branchId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Check large exposure limits compliance
    /// </summary>
    [HttpGet("boz/large-exposures")]
    [Authorize(Roles = "Compliance,Manager,Admin")]
    public async Task<IActionResult> CheckLargeExposureLimits([FromQuery] string? branchId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveBranchId = branchId ?? GetUserBranchId();
            
            if (string.IsNullOrEmpty(effectiveBranchId))
            {
                return BadRequest(new { message = "Branch ID is required" });
            }

            var result = await _bozComplianceService.CheckLargeExposureLimitsAsync(effectiveBranchId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking large exposure limits for branch {BranchId}", branchId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Check regulatory reporting deadlines
    /// </summary>
    [HttpGet("boz/reporting-deadlines")]
    [Authorize(Roles = "Compliance,Manager,Admin")]
    public async Task<IActionResult> CheckReportingDeadlines([FromQuery] string? branchId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveBranchId = branchId ?? GetUserBranchId();
            
            if (string.IsNullOrEmpty(effectiveBranchId))
            {
                return BadRequest(new { message = "Branch ID is required" });
            }

            var result = await _bozComplianceService.CheckReportingDeadlinesAsync(effectiveBranchId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking reporting deadlines for branch {BranchId}", branchId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Check liquidity ratios compliance
    /// </summary>
    [HttpGet("boz/liquidity-ratios")]
    [Authorize(Roles = "Compliance,Manager,Admin,Finance")]
    public async Task<IActionResult> CheckLiquidityRatios([FromQuery] string? branchId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveBranchId = branchId ?? GetUserBranchId();
            
            if (string.IsNullOrEmpty(effectiveBranchId))
            {
                return BadRequest(new { message = "Branch ID is required" });
            }

            var result = await _bozComplianceService.CheckLiquidityRatiosAsync(effectiveBranchId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking liquidity ratios for branch {BranchId}", branchId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    #endregion

    #region Compliance Monitoring Schedule

    /// <summary>
    /// Schedule automated compliance monitoring
    /// </summary>
    [HttpPost("schedules")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ScheduleComplianceMonitoring([FromBody] ComplianceMonitoringSchedule schedule, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _complianceService.ScheduleComplianceMonitoringAsync(schedule.BranchId, schedule, cancellationToken);
            
            return CreatedAtAction(
                nameof(GetComplianceHistory), 
                new { branchId = schedule.BranchId }, 
                new { message = "Compliance monitoring scheduled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling compliance monitoring for branch {BranchId}", schedule.BranchId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    #endregion

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               User.FindFirst("sub")?.Value ?? 
               User.FindFirst("user_id")?.Value ?? 
               string.Empty;
    }

    private string GetUserBranchId()
    {
        return User.FindFirst("branch_id")?.Value ?? 
               Request.Headers["X-Branch-Id"].FirstOrDefault() ?? 
               string.Empty;
    }
}

public class AcknowledgeAlertRequest
{
    public string? Notes { get; set; }
}

public class ResolveAlertRequest
{
    public string ResolutionNotes { get; set; } = string.Empty;
}

public class GenerateComplianceReportRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? BranchId { get; set; }
}