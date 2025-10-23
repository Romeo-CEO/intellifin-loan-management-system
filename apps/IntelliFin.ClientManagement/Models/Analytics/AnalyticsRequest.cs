namespace IntelliFin.ClientManagement.Models.Analytics;

/// <summary>
/// Base request for analytics queries
/// </summary>
public class AnalyticsRequest
{
    /// <summary>
    /// Start date for analytics period
    /// </summary>
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(-30);

    /// <summary>
    /// End date for analytics period
    /// </summary>
    public DateTime EndDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Branch ID (null for system-wide metrics)
    /// </summary>
    public Guid? BranchId { get; set; }

    /// <summary>
    /// Granularity for time-series data
    /// </summary>
    public TimeGranularity? Granularity { get; set; }
}

/// <summary>
/// Time granularity for aggregations
/// </summary>
public enum TimeGranularity
{
    /// <summary>
    /// Daily aggregation
    /// </summary>
    Daily,

    /// <summary>
    /// Weekly aggregation
    /// </summary>
    Weekly,

    /// <summary>
    /// Monthly aggregation
    /// </summary>
    Monthly
}

/// <summary>
/// Request for officer performance metrics
/// </summary>
public class OfficerPerformanceRequest : AnalyticsRequest
{
    /// <summary>
    /// Specific officer ID (null for all officers)
    /// </summary>
    public string? OfficerId { get; set; }

    /// <summary>
    /// Minimum number of processed clients to include officer
    /// </summary>
    public int MinimumProcessed { get; set; } = 1;

    /// <summary>
    /// Sort by field
    /// </summary>
    public OfficerSortBy SortBy { get; set; } = OfficerSortBy.TotalProcessed;

    /// <summary>
    /// Sort direction
    /// </summary>
    public SortDirection SortDirection { get; set; } = SortDirection.Descending;
}

/// <summary>
/// Officer performance sort options
/// </summary>
public enum OfficerSortBy
{
    TotalProcessed,
    CompletionRate,
    AverageProcessingTime,
    SlaComplianceRate
}

/// <summary>
/// Sort direction
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}
