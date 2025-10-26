using IntelliFin.Collections.Application.DTOs;
using IntelliFin.Collections.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.Collections.API.Controllers;

[ApiController]
[Route("api/collections/reports")]
public class ReportsController : ControllerBase
{
    private readonly ICollectionsReportingService _reportingService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        ICollectionsReportingService reportingService,
        ILogger<ReportsController> logger)
    {
        _reportingService = reportingService;
        _logger = logger;
    }

    /// <summary>
    /// Gets aging analysis report.
    /// </summary>
    [HttpGet("aging")]
    [ProducesResponseType(typeof(AgingAnalysisReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAgingAnalysis(
        [FromQuery] DateTime? asOfDate,
        CancellationToken cancellationToken)
    {
        var reportDate = asOfDate ?? DateTime.UtcNow.Date;
        var report = await _reportingService.GetAgingAnalysisAsync(reportDate, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Gets Portfolio at Risk (PAR) report.
    /// </summary>
    [HttpGet("par")]
    [ProducesResponseType(typeof(PortfolioAtRiskReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPortfolioAtRisk(
        [FromQuery] DateTime? asOfDate,
        CancellationToken cancellationToken)
    {
        var reportDate = asOfDate ?? DateTime.UtcNow.Date;
        var report = await _reportingService.GetPortfolioAtRiskAsync(reportDate, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Gets provisioning report.
    /// </summary>
    [HttpGet("provisioning")]
    [ProducesResponseType(typeof(ProvisioningReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProvisioning(
        [FromQuery] DateTime? asOfDate,
        CancellationToken cancellationToken)
    {
        var reportDate = asOfDate ?? DateTime.UtcNow.Date;
        var report = await _reportingService.GetProvisioningReportAsync(reportDate, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Gets recovery analytics report.
    /// </summary>
    [HttpGet("recovery")]
    [ProducesResponseType(typeof(RecoveryAnalyticsReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecoveryAnalytics(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var start = startDate ?? DateTime.UtcNow.Date.AddMonths(-1);
        var end = endDate ?? DateTime.UtcNow.Date;

        var report = await _reportingService.GetRecoveryAnalyticsAsync(start, end, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Gets collections dashboard with key metrics.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(CollectionsDashboard), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var dashboard = await _reportingService.GetCollectionsDashboardAsync(cancellationToken);
        return Ok(dashboard);
    }
}
