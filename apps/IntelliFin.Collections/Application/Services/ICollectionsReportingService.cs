using IntelliFin.Collections.Application.DTOs;

namespace IntelliFin.Collections.Application.Services;

public interface ICollectionsReportingService
{
    /// <summary>
    /// Gets aging analysis report showing loans by days past due buckets.
    /// </summary>
    Task<AgingAnalysisReport> GetAgingAnalysisAsync(
        DateTime asOfDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets Portfolio at Risk (PAR) metrics.
    /// </summary>
    Task<PortfolioAtRiskReport> GetPortfolioAtRiskAsync(
        DateTime asOfDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets provisioning report showing required provisions by classification.
    /// </summary>
    Task<ProvisioningReport> GetProvisioningReportAsync(
        DateTime asOfDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recovery analytics showing collection performance.
    /// </summary>
    Task<RecoveryAnalyticsReport> GetRecoveryAnalyticsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets collections performance dashboard metrics.
    /// </summary>
    Task<CollectionsDashboard> GetCollectionsDashboardAsync(
        CancellationToken cancellationToken = default);
}
