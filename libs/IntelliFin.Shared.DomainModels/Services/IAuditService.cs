using IntelliFin.Shared.DomainModels.Entities;

namespace IntelliFin.Shared.DomainModels.Services;

/// <summary>
/// Interface for comprehensive audit trail services
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Log an audit event asynchronously
    /// </summary>
    Task LogEventAsync(string actor, string action, string entityType, string entityId, object? data = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Log an audit event with detailed context
    /// </summary>
    Task LogEventAsync(AuditEventContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query audit events with filtering
    /// </summary>
    Task<AuditQueryResult> QueryEventsAsync(AuditQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate audit report
    /// </summary>
    Task<AuditReport> GenerateReportAsync(AuditReportRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit statistics
    /// </summary>
    Task<AuditStatistics> GetStatisticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify audit trail integrity
    /// </summary>
    Task<AuditIntegrityResult> VerifyIntegrityAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Audit event context with detailed information
/// </summary>
public class AuditEventContext
{
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public string Source { get; set; } = string.Empty;
    public AuditEventCategory Category { get; set; }
    public AuditEventSeverity Severity { get; set; } = AuditEventSeverity.Information;
    public Dictionary<string, object> Data { get; set; } = new();
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public DateTime? OccurredAt { get; set; }
}

/// <summary>
/// Audit query parameters
/// </summary>
public class AuditQuery
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Actor { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public AuditEventCategory? Category { get; set; }
    public AuditEventSeverity? Severity { get; set; }
    public bool? Success { get; set; }
    public string? SearchText { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string SortBy { get; set; } = "OccurredAtUtc";
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Audit query result
/// </summary>
public class AuditQueryResult
{
    public List<AuditEvent> Events { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}

/// <summary>
/// Audit report request
/// </summary>
public class AuditReportRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public AuditReportType ReportType { get; set; }
    public List<AuditEventCategory> Categories { get; set; } = new();
    public List<string> Actors { get; set; } = new();
    public string? EntityType { get; set; }
    public AuditReportFormat Format { get; set; } = AuditReportFormat.Pdf;
    public bool IncludeStatistics { get; set; } = true;
    public bool IncludeCharts { get; set; } = true;
    public string? Title { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Audit report
/// </summary>
public class AuditReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public AuditReportType ReportType { get; set; }
    public AuditReportFormat Format { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public AuditStatistics Statistics { get; set; } = new();
    public List<AuditEvent> SampleEvents { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Audit statistics
/// </summary>
public class AuditStatistics
{
    public int TotalEvents { get; set; }
    public Dictionary<AuditEventCategory, int> EventsByCategory { get; set; } = new();
    public Dictionary<AuditEventSeverity, int> EventsBySeverity { get; set; } = new();
    public Dictionary<string, int> EventsByAction { get; set; } = new();
    public Dictionary<string, int> EventsByActor { get; set; } = new();
    public Dictionary<string, int> EventsByEntityType { get; set; } = new();
    public Dictionary<DateTime, int> EventsByDay { get; set; } = new();
    public Dictionary<int, int> EventsByHour { get; set; } = new();
    public int SuccessfulEvents { get; set; }
    public int FailedEvents { get; set; }
    public double SuccessRate { get; set; }
    public List<string> TopActors { get; set; } = new();
    public List<string> TopActions { get; set; } = new();
    public List<AuditAnomaly> Anomalies { get; set; } = new();
}

/// <summary>
/// Audit integrity verification result
/// </summary>
public class AuditIntegrityResult
{
    public bool IsIntact { get; set; }
    public int TotalRecords { get; set; }
    public int VerifiedRecords { get; set; }
    public List<string> IntegrityViolations { get; set; } = new();
    public DateTime VerificationDate { get; set; } = DateTime.UtcNow;
    public string VerificationHash { get; set; } = string.Empty;
}

/// <summary>
/// Audit anomaly detection
/// </summary>
public class AuditAnomaly
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AuditEventSeverity Severity { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Audit event categories
/// </summary>
public enum AuditEventCategory
{
    Authentication,
    Authorization,
    DataAccess,
    DataModification,
    SystemActivity,
    UserActivity,
    ComplianceEvent,
    SecurityEvent,
    FinancialTransaction,
    LoanActivity,
    ClientActivity,
    AdminActivity,
    ReportGeneration,
    BackupRestore,
    ConfigurationChange,
    IntegrationEvent,
    ErrorEvent,
    PerformanceEvent
}

/// <summary>
/// Audit event severity levels
/// </summary>
public enum AuditEventSeverity
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Audit report types
/// </summary>
public enum AuditReportType
{
    Comprehensive,
    Compliance,
    Security,
    UserActivity,
    SystemActivity,
    Financial,
    DataAccess,
    ErrorSummary,
    PerformanceSummary
}

/// <summary>
/// Audit report formats
/// </summary>
public enum AuditReportFormat
{
    Pdf,
    Excel,
    Json,
    Csv,
    Html
}