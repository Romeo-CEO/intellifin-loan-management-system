using IntelliFin.ClientManagement.Models.Analytics;
using IntelliFin.ClientManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.ClientManagement.Controllers;

/// <summary>
/// API endpoints for KYC performance analytics and metrics
/// Provides comprehensive dashboards for branch managers and operations
/// </summary>
[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        IAnalyticsService analyticsService,
        ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    /// <summary>
    /// Gets comprehensive dashboard metrics
    /// Combines all key metrics for executive dashboards
    /// </summary>
    /// <param name="startDate">Period start date</param>
    /// <param name="endDate">Period end date</param>
    /// <param name="branchId">Optional branch filter</param>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? branchId = null)
    {
        var request = new AnalyticsRequest
        {
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
            EndDate = endDate ?? DateTime.UtcNow,
            BranchId = branchId
        };

        _logger.LogInformation(
            "Getting dashboard metrics: Period={Start} to {End}, BranchId={BranchId}",
            request.StartDate, request.EndDate, request.BranchId);

        // Fetch all metrics in parallel
        var kycTask = _analyticsService.GetKycPerformanceAsync(request);
        var docTask = _analyticsService.GetDocumentMetricsAsync(request);
        var amlTask = _analyticsService.GetAmlMetricsAsync(request);
        var eddTask = _analyticsService.GetEddMetricsAsync(request);
        var riskTask = _analyticsService.GetRiskDistributionAsync(request);
        var funnelTask = _analyticsService.GetKycFunnelMetricsAsync(request);

        await Task.WhenAll(kycTask, docTask, amlTask, eddTask, riskTask, funnelTask);

        var dashboard = new DashboardResponse
        {
            PeriodStart = request.StartDate,
            PeriodEnd = request.EndDate,
            BranchId = request.BranchId,
            KycMetrics = kycTask.Result.IsSuccess ? kycTask.Result.Value : null,
            DocumentMetrics = docTask.Result.IsSuccess ? docTask.Result.Value : null,
            AmlMetrics = amlTask.Result.IsSuccess ? amlTask.Result.Value : null,
            EddMetrics = eddTask.Result.IsSuccess ? eddTask.Result.Value : null,
            RiskDistribution = riskTask.Result.IsSuccess ? riskTask.Result.Value : null,
            FunnelMetrics = funnelTask.Result.IsSuccess ? funnelTask.Result.Value : null,
            GeneratedAt = DateTime.UtcNow
        };

        return Ok(dashboard);
    }

    /// <summary>
    /// Gets KYC performance metrics
    /// </summary>
    [HttpGet("kyc/performance")]
    [ProducesResponseType(typeof(KycPerformanceMetrics), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKycPerformance(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? branchId = null)
    {
        var request = new AnalyticsRequest
        {
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
            EndDate = endDate ?? DateTime.UtcNow,
            BranchId = branchId
        };

        var result = await _analyticsService.GetKycPerformanceAsync(request);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets document verification metrics
    /// </summary>
    [HttpGet("documents")]
    [ProducesResponseType(typeof(DocumentMetrics), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocumentMetrics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? branchId = null)
    {
        var request = new AnalyticsRequest
        {
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
            EndDate = endDate ?? DateTime.UtcNow,
            BranchId = branchId
        };

        var result = await _analyticsService.GetDocumentMetricsAsync(request);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets AML screening metrics
    /// </summary>
    [HttpGet("aml")]
    [ProducesResponseType(typeof(AmlMetrics), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAmlMetrics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? branchId = null)
    {
        var request = new AnalyticsRequest
        {
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
            EndDate = endDate ?? DateTime.UtcNow,
            BranchId = branchId
        };

        var result = await _analyticsService.GetAmlMetricsAsync(request);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets EDD workflow metrics
    /// </summary>
    [HttpGet("edd")]
    [ProducesResponseType(typeof(EddMetrics), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEddMetrics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? branchId = null)
    {
        var request = new AnalyticsRequest
        {
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
            EndDate = endDate ?? DateTime.UtcNow,
            BranchId = branchId
        };

        var result = await _analyticsService.GetEddMetricsAsync(request);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets officer performance metrics
    /// </summary>
    [HttpGet("officers")]
    [ProducesResponseType(typeof(List<OfficerPerformanceMetrics>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOfficerPerformance(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? branchId = null,
        [FromQuery] string? officerId = null,
        [FromQuery] OfficerSortBy sortBy = OfficerSortBy.TotalProcessed,
        [FromQuery] SortDirection sortDirection = SortDirection.Descending,
        [FromQuery] int minimumProcessed = 1)
    {
        var request = new OfficerPerformanceRequest
        {
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
            EndDate = endDate ?? DateTime.UtcNow,
            BranchId = branchId,
            OfficerId = officerId,
            SortBy = sortBy,
            SortDirection = sortDirection,
            MinimumProcessed = minimumProcessed
        };

        var result = await _analyticsService.GetOfficerPerformanceAsync(request);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets risk distribution metrics
    /// </summary>
    [HttpGet("risk")]
    [ProducesResponseType(typeof(RiskDistributionMetrics), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRiskDistribution(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? branchId = null)
    {
        var request = new AnalyticsRequest
        {
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
            EndDate = endDate ?? DateTime.UtcNow,
            BranchId = branchId
        };

        var result = await _analyticsService.GetRiskDistributionAsync(request);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets KYC funnel conversion metrics
    /// </summary>
    [HttpGet("funnel")]
    [ProducesResponseType(typeof(KycFunnelMetrics), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKycFunnel(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? branchId = null)
    {
        var request = new AnalyticsRequest
        {
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
            EndDate = endDate ?? DateTime.UtcNow,
            BranchId = branchId
        };

        var result = await _analyticsService.GetKycFunnelMetricsAsync(request);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets time-series KYC data for charts
    /// </summary>
    [HttpGet("timeseries")]
    [ProducesResponseType(typeof(List<TimeSeriesDataPoint>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimeSeries(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? branchId = null,
        [FromQuery] TimeGranularity granularity = TimeGranularity.Daily)
    {
        var request = new AnalyticsRequest
        {
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
            EndDate = endDate ?? DateTime.UtcNow,
            BranchId = branchId,
            Granularity = granularity
        };

        var result = await _analyticsService.GetKycTimeSeriesAsync(request);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }
}

/// <summary>
/// Comprehensive dashboard response
/// Aggregates all key metrics for executive dashboards
/// </summary>
public class DashboardResponse
{
    /// <summary>
    /// Start of reporting period
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// End of reporting period
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Branch ID (null for system-wide)
    /// </summary>
    public Guid? BranchId { get; set; }

    /// <summary>
    /// KYC performance metrics
    /// </summary>
    public KycPerformanceMetrics? KycMetrics { get; set; }

    /// <summary>
    /// Document verification metrics
    /// </summary>
    public DocumentMetrics? DocumentMetrics { get; set; }

    /// <summary>
    /// AML screening metrics
    /// </summary>
    public AmlMetrics? AmlMetrics { get; set; }

    /// <summary>
    /// EDD workflow metrics
    /// </summary>
    public EddMetrics? EddMetrics { get; set; }

    /// <summary>
    /// Risk distribution
    /// </summary>
    public RiskDistributionMetrics? RiskDistribution { get; set; }

    /// <summary>
    /// KYC funnel metrics
    /// </summary>
    public KycFunnelMetrics? FunnelMetrics { get; set; }

    /// <summary>
    /// Timestamp when dashboard was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}
