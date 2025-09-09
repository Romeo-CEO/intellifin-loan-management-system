using Microsoft.AspNetCore.Mvc;
using IntelliFin.FinancialService.Models;
using IntelliFin.FinancialService.Services;

namespace IntelliFin.FinancialService.Controllers;

[ApiController]
[Route("api/reporting")]
public class ReportingController : ControllerBase
{
    private readonly IReportingService _reportingService;
    private readonly IBozReportingService _bozReportingService;
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<ReportingController> _logger;

    public ReportingController(
        IReportingService reportingService,
        IBozReportingService bozReportingService,
        IDashboardService dashboardService,
        ILogger<ReportingController> logger)
    {
        _reportingService = reportingService;
        _bozReportingService = bozReportingService;
        _dashboardService = dashboardService;
        _logger = logger;
    }

    /// <summary>
    /// Get real-time dashboard metrics
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardMetrics>> GetDashboardMetrics([FromQuery] string? branchId = null)
    {
        try
        {
            var metrics = await _dashboardService.GetDashboardMetricsAsync(branchId);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard metrics");
            return StatusCode(500, new { error = "Failed to retrieve dashboard metrics", details = ex.Message });
        }
    }

    /// <summary>
    /// Get account balance summary for dashboard
    /// </summary>
    [HttpGet("dashboard/balances")]
    public async Task<ActionResult<Dictionary<string, decimal>>> GetAccountBalanceSummary([FromQuery] string? branchId = null)
    {
        try
        {
            var balances = await _dashboardService.GetAccountBalanceSummaryAsync(branchId);
            return Ok(balances);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving account balance summary");
            return StatusCode(500, new { error = "Failed to retrieve account balances", details = ex.Message });
        }
    }

    /// <summary>
    /// Get loan performance metrics
    /// </summary>
    [HttpGet("dashboard/loans")]
    public async Task<ActionResult<Dictionary<string, object>>> GetLoanPerformanceMetrics([FromQuery] string? branchId = null)
    {
        try
        {
            var metrics = await _dashboardService.GetLoanPerformanceMetricsAsync(branchId);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving loan performance metrics");
            return StatusCode(500, new { error = "Failed to retrieve loan metrics", details = ex.Message });
        }
    }

    /// <summary>
    /// Get cash flow metrics
    /// </summary>
    [HttpGet("dashboard/cashflow")]
    public async Task<ActionResult<Dictionary<string, decimal>>> GetCashFlowMetrics(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string? branchId = null)
    {
        try
        {
            var metrics = await _dashboardService.GetCashFlowMetricsAsync(startDate, endDate, branchId);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cash flow metrics");
            return StatusCode(500, new { error = "Failed to retrieve cash flow metrics", details = ex.Message });
        }
    }

    /// <summary>
    /// Get collection metrics
    /// </summary>
    [HttpGet("dashboard/collections")]
    public async Task<ActionResult<Dictionary<string, object>>> GetCollectionMetrics([FromQuery] string? branchId = null)
    {
        try
        {
            var metrics = await _dashboardService.GetCollectionMetricsAsync(branchId);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving collection metrics");
            return StatusCode(500, new { error = "Failed to retrieve collection metrics", details = ex.Message });
        }
    }

    /// <summary>
    /// Generate a custom report
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateReport([FromBody] ReportRequest request)
    {
        try
        {
            var report = await _reportingService.GenerateReportAsync(request);
            return File(report.Content, report.ContentType, report.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report");
            return StatusCode(500, new { error = "Failed to generate report", details = ex.Message });
        }
    }

    /// <summary>
    /// Get available report templates
    /// </summary>
    [HttpGet("templates")]
    public async Task<ActionResult<List<ReportTemplate>>> GetReportTemplates()
    {
        try
        {
            var templates = await _reportingService.GetReportTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report templates");
            return StatusCode(500, new { error = "Failed to retrieve report templates", details = ex.Message });
        }
    }

    /// <summary>
    /// Generate BoZ NPL Classification Report
    /// </summary>
    [HttpPost("boz/npl-classification")]
    public async Task<IActionResult> GenerateNplClassificationReport(
        [FromQuery] DateTime asOfDate,
        [FromQuery] string branchId)
    {
        try
        {
            var report = await _bozReportingService.GenerateNplClassificationReportAsync(asOfDate, branchId);
            return File(report.Content, report.ContentType, report.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating NPL Classification report");
            return StatusCode(500, new { error = "Failed to generate NPL Classification report", details = ex.Message });
        }
    }

    /// <summary>
    /// Generate BoZ Capital Adequacy Report
    /// </summary>
    [HttpPost("boz/capital-adequacy")]
    public async Task<IActionResult> GenerateCapitalAdequacyReport(
        [FromQuery] DateTime asOfDate,
        [FromQuery] string branchId)
    {
        try
        {
            var report = await _bozReportingService.GenerateCapitalAdequacyReportAsync(asOfDate, branchId);
            return File(report.Content, report.ContentType, report.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Capital Adequacy report");
            return StatusCode(500, new { error = "Failed to generate Capital Adequacy report", details = ex.Message });
        }
    }

    /// <summary>
    /// Generate BoZ Loan Portfolio Summary
    /// </summary>
    [HttpPost("boz/loan-portfolio")]
    public async Task<IActionResult> GenerateLoanPortfolioSummary(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string branchId)
    {
        try
        {
            var report = await _bozReportingService.GenerateLoanPortfolioSummaryAsync(startDate, endDate, branchId);
            return File(report.Content, report.ContentType, report.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Loan Portfolio Summary");
            return StatusCode(500, new { error = "Failed to generate Loan Portfolio Summary", details = ex.Message });
        }
    }

    /// <summary>
    /// Generate BoZ Prudential Report
    /// </summary>
    [HttpPost("boz/prudential")]
    public async Task<ActionResult<BozPrudentialReport>> GeneratePrudentialReport(
        [FromQuery] DateTime reportingPeriod,
        [FromQuery] string branchId)
    {
        try
        {
            var report = await _bozReportingService.GeneratePrudentialReportAsync(reportingPeriod, branchId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Prudential report");
            return StatusCode(500, new { error = "Failed to generate Prudential report", details = ex.Message });
        }
    }

    /// <summary>
    /// Schedule a report for automated generation
    /// </summary>
    [HttpPost("schedule")]
    public async Task<ActionResult<string>> ScheduleReport([FromBody] ScheduledReport scheduledReport)
    {
        try
        {
            var scheduleId = await _reportingService.ScheduleReportAsync(scheduledReport);
            return Ok(new { scheduleId, message = "Report scheduled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling report");
            return StatusCode(500, new { error = "Failed to schedule report", details = ex.Message });
        }
    }

    /// <summary>
    /// Get scheduled reports
    /// </summary>
    [HttpGet("scheduled")]
    public async Task<ActionResult<List<ScheduledReport>>> GetScheduledReports()
    {
        try
        {
            var scheduledReports = await _reportingService.GetScheduledReportsAsync();
            return Ok(scheduledReports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving scheduled reports");
            return StatusCode(500, new { error = "Failed to retrieve scheduled reports", details = ex.Message });
        }
    }
}