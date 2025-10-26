using IntelliFin.ClientManagement.Models.Analytics;
using IntelliFin.Shared.Infrastructure;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service for KYC performance analytics and metrics
/// Provides comprehensive reporting and dashboards
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Gets KYC performance metrics for a given period
    /// </summary>
    Task<Result<KycPerformanceMetrics>> GetKycPerformanceAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets document verification metrics
    /// </summary>
    Task<Result<DocumentMetrics>> GetDocumentMetricsAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets AML screening metrics
    /// </summary>
    Task<Result<AmlMetrics>> GetAmlMetricsAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets EDD workflow metrics
    /// </summary>
    Task<Result<EddMetrics>> GetEddMetricsAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets officer performance metrics
    /// </summary>
    Task<Result<List<OfficerPerformanceMetrics>>> GetOfficerPerformanceAsync(
        OfficerPerformanceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets risk distribution metrics
    /// </summary>
    Task<Result<RiskDistributionMetrics>> GetRiskDistributionAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets KYC funnel metrics (conversion rates)
    /// </summary>
    Task<Result<KycFunnelMetrics>> GetKycFunnelMetricsAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets time-series KYC performance data
    /// </summary>
    Task<Result<List<TimeSeriesDataPoint>>> GetKycTimeSeriesAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Time-series data point for charts
/// </summary>
public class TimeSeriesDataPoint
{
    /// <summary>
    /// Time period
    /// </summary>
    public DateTime Period { get; set; }

    /// <summary>
    /// Number of KYC processes started
    /// </summary>
    public int Started { get; set; }

    /// <summary>
    /// Number of KYC processes completed
    /// </summary>
    public int Completed { get; set; }

    /// <summary>
    /// Number of KYC processes rejected
    /// </summary>
    public int Rejected { get; set; }

    /// <summary>
    /// Number of EDD escalations
    /// </summary>
    public int EddEscalations { get; set; }

    /// <summary>
    /// Completion rate for this period (%)
    /// </summary>
    public double CompletionRate { get; set; }
}
