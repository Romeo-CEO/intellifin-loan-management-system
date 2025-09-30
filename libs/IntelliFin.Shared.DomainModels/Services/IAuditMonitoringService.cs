using IntelliFin.Shared.DomainModels.Entities;

namespace IntelliFin.Shared.DomainModels.Services;

/// <summary>
/// Interface for audit trail monitoring and alerting
/// </summary>
public interface IAuditMonitoringService
{
    /// <summary>
    /// Start monitoring audit trail for anomalies
    /// </summary>
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop monitoring
    /// </summary>
    Task StopMonitoringAsync();

    /// <summary>
    /// Process a new audit event for real-time monitoring
    /// </summary>
    Task ProcessEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get active monitoring alerts
    /// </summary>
    Task<List<AuditMonitoringAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledge an alert
    /// </summary>
    Task AcknowledgeAlertAsync(string alertId, string acknowledgedBy, string? notes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configure monitoring rules
    /// </summary>
    Task ConfigureMonitoringRulesAsync(List<AuditMonitoringRule> rules, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get monitoring statistics
    /// </summary>
    Task<AuditMonitoringStatistics> GetMonitoringStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Test monitoring configuration
    /// </summary>
    Task<AuditMonitoringTestResult> TestMonitoringAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Audit monitoring alert
/// </summary>
public class AuditMonitoringAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RuleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public AuditMonitoringAlertType Type { get; set; }
    public AuditEventSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Recommendation { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public string? AcknowledgementNotes { get; set; }
    public AuditMonitoringAlertStatus Status { get; set; } = AuditMonitoringAlertStatus.Active;
    public Dictionary<string, object> Data { get; set; } = new();
    public List<string> RelatedEventIds { get; set; } = new();
    public int EventCount { get; set; }
    public DateTime? LastEventAt { get; set; }
}

/// <summary>
/// Audit monitoring rule
/// </summary>
public class AuditMonitoringRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AuditMonitoringRuleType Type { get; set; }
    public bool IsEnabled { get; set; } = true;
    public AuditEventSeverity AlertSeverity { get; set; } = AuditEventSeverity.Warning;
    public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(15);
    public int Threshold { get; set; } = 10;
    public AuditEventCategory? CategoryFilter { get; set; }
    public string? ActorFilter { get; set; }
    public string? ActionFilter { get; set; }
    public string? EntityTypeFilter { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTriggered { get; set; }
    public int TriggerCount { get; set; }
}

/// <summary>
/// Audit monitoring statistics
/// </summary>
public class AuditMonitoringStatistics
{
    public DateTime StatisticsDate { get; set; } = DateTime.UtcNow;
    public int ActiveRules { get; set; }
    public int ActiveAlerts { get; set; }
    public int AcknowledgedAlerts { get; set; }
    public int TotalAlertsToday { get; set; }
    public Dictionary<AuditMonitoringAlertType, int> AlertsByType { get; set; } = new();
    public Dictionary<AuditEventSeverity, int> AlertsBySeverity { get; set; } = new();
    public List<AuditMonitoringRule> TopTriggeredRules { get; set; } = new();
    public double AverageResponseTime { get; set; }
    public List<AuditTrendPoint> AlertTrend { get; set; } = new();
}

/// <summary>
/// Audit monitoring test result
/// </summary>
public class AuditMonitoringTestResult
{
    public bool IsHealthy { get; set; }
    public DateTime TestDate { get; set; } = DateTime.UtcNow;
    public List<string> TestResults { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, object> TestMetrics { get; set; } = new();
}

/// <summary>
/// Audit trend point for analytics
/// </summary>
public class AuditTrendPoint
{
    public DateTime Date { get; set; }
    public int AlertCount { get; set; }
    public int EventCount { get; set; }
    public double AverageResponseTime { get; set; }
}

/// <summary>
/// Audit monitoring alert types
/// </summary>
public enum AuditMonitoringAlertType
{
    HighVolumeActivity,
    SuspiciousActivity,
    FailedOperations,
    SecurityViolation,
    ComplianceViolation,
    SystemAnomaly,
    DataIntegrityIssue,
    UnauthorizedAccess,
    ConfigurationChange,
    PerformanceDegradation
}

/// <summary>
/// Audit monitoring alert status
/// </summary>
public enum AuditMonitoringAlertStatus
{
    Active,
    Acknowledged,
    Resolved,
    Dismissed,
    Escalated
}

/// <summary>
/// Audit monitoring rule types
/// </summary>
public enum AuditMonitoringRuleType
{
    VolumeThreshold,
    FailureRate,
    SuspiciousPattern,
    TimeBasedAnomaly,
    ComplianceViolation,
    SecurityThreat,
    DataIntegrityCheck,
    PerformanceMonitoring,
    CustomRule
}