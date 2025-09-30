using IntelliFin.FinancialService.Models;

namespace IntelliFin.FinancialService.Services;

/// <summary>
/// Interface for compliance monitoring and alerting
/// </summary>
public interface IComplianceMonitoringService
{
    /// <summary>
    /// Monitor and check all compliance rules
    /// </summary>
    Task<ComplianceMonitoringResult> MonitorComplianceAsync(string branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check specific compliance rule
    /// </summary>
    Task<ComplianceRuleResult> CheckComplianceRuleAsync(string ruleId, string branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all compliance alerts
    /// </summary>
    Task<List<ComplianceAlert>> GetComplianceAlertsAsync(string branchId, ComplianceAlertStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get compliance dashboard metrics
    /// </summary>
    Task<ComplianceDashboardMetrics> GetComplianceDashboardAsync(string branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledge a compliance alert
    /// </summary>
    Task AcknowledgeAlertAsync(string alertId, string acknowledgedBy, string? notes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve a compliance alert
    /// </summary>
    Task ResolveAlertAsync(string alertId, string resolvedBy, string resolutionNotes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a manual compliance alert
    /// </summary>
    Task<string> CreateManualAlertAsync(CreateComplianceAlertRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get compliance rule definitions
    /// </summary>
    Task<List<ComplianceRule>> GetComplianceRulesAsync(ComplianceRuleCategory? category = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update compliance rule configuration
    /// </summary>
    Task UpdateComplianceRuleAsync(string ruleId, UpdateComplianceRuleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate compliance report
    /// </summary>
    Task<ComplianceReport> GenerateComplianceReportAsync(DateTime startDate, DateTime endDate, string branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedule automated compliance monitoring
    /// </summary>
    Task ScheduleComplianceMonitoringAsync(string branchId, ComplianceMonitoringSchedule schedule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get compliance history
    /// </summary>
    Task<List<ComplianceHistoryEntry>> GetComplianceHistoryAsync(string branchId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for BoZ compliance rules specifically
/// </summary>
public interface IBozComplianceService
{
    /// <summary>
    /// Check capital adequacy ratio compliance
    /// </summary>
    Task<ComplianceRuleResult> CheckCapitalAdequacyRatioAsync(string branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check loan classification compliance
    /// </summary>
    Task<ComplianceRuleResult> CheckLoanClassificationComplianceAsync(string branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check provision coverage compliance
    /// </summary>
    Task<ComplianceRuleResult> CheckProvisionCoverageAsync(string branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check large exposure limits
    /// </summary>
    Task<ComplianceRuleResult> CheckLargeExposureLimitsAsync(string branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check regulatory reporting deadlines
    /// </summary>
    Task<ComplianceRuleResult> CheckReportingDeadlinesAsync(string branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check liquidity ratios
    /// </summary>
    Task<ComplianceRuleResult> CheckLiquidityRatiosAsync(string branchId, CancellationToken cancellationToken = default);
}

// Compliance models
public class ComplianceMonitoringResult
{
    public string BranchId { get; set; } = string.Empty;
    public DateTime MonitoringDate { get; set; } = DateTime.UtcNow;
    public ComplianceStatus OverallStatus { get; set; }
    public int TotalRulesChecked { get; set; }
    public int RulesPassed { get; set; }
    public int RulesWarning { get; set; }
    public int RulesFailed { get; set; }
    public List<ComplianceRuleResult> RuleResults { get; set; } = new();
    public List<ComplianceAlert> NewAlerts { get; set; } = new();
    public TimeSpan MonitoringDuration { get; set; }
}

public class ComplianceRuleResult
{
    public string RuleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public ComplianceRuleCategory Category { get; set; }
    public ComplianceStatus Status { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public double? Threshold { get; set; }
    public double? ActualValue { get; set; }
    public ComplianceSeverity Severity { get; set; }
}

public class ComplianceAlert
{
    public string Id { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public ComplianceRuleCategory Category { get; set; }
    public ComplianceSeverity Severity { get; set; }
    public ComplianceAlertStatus Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? RecommendedAction { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public string? AcknowledgementNotes { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsRegulatory { get; set; }
    public List<string> NotificationsSent { get; set; } = new();
}

public class ComplianceDashboardMetrics
{
    public string BranchId { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public ComplianceStatus OverallStatus { get; set; }
    public int ActiveAlerts { get; set; }
    public int CriticalAlerts { get; set; }
    public int WarningAlerts { get; set; }
    public int ResolvedAlertsToday { get; set; }
    public double ComplianceScore { get; set; } // Overall compliance percentage
    public Dictionary<ComplianceRuleCategory, int> AlertsByCategory { get; set; } = new();
    public List<ComplianceAlert> RecentAlerts { get; set; } = new();
    public List<ComplianceTrend> Trends { get; set; } = new();
    public Dictionary<string, object> KeyMetrics { get; set; } = new();
}

public class ComplianceTrend
{
    public DateTime Date { get; set; }
    public ComplianceRuleCategory Category { get; set; }
    public int AlertCount { get; set; }
    public double ComplianceScore { get; set; }
}

public class CreateComplianceAlertRequest
{
    public string BranchId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ComplianceRuleCategory Category { get; set; }
    public ComplianceSeverity Severity { get; set; }
    public string? RecommendedAction { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime? DueDate { get; set; }
    public bool IsRegulatory { get; set; }
}

public class ComplianceRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ComplianceRuleCategory Category { get; set; }
    public ComplianceSeverity DefaultSeverity { get; set; }
    public bool IsEnabled { get; set; } = true;
    public double? WarningThreshold { get; set; }
    public double? CriticalThreshold { get; set; }
    public string? ThresholdUnit { get; set; }
    public TimeSpan CheckFrequency { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModified { get; set; }
    public string? SqlQuery { get; set; } // For database-based rules
    public string? ApiEndpoint { get; set; } // For API-based rules
}

public class UpdateComplianceRuleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsEnabled { get; set; }
    public double? WarningThreshold { get; set; }
    public double? CriticalThreshold { get; set; }
    public TimeSpan? CheckFrequency { get; set; }
    public Dictionary<string, object>? Configuration { get; set; }
}

public class ComplianceReport
{
    public string Id { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public ComplianceStatus OverallStatus { get; set; }
    public double ComplianceScore { get; set; }
    public int TotalAlertsGenerated { get; set; }
    public int AlertsResolved { get; set; }
    public Dictionary<ComplianceRuleCategory, ComplianceMetrics> CategoryMetrics { get; set; } = new();
    public List<ComplianceAlert> CriticalAlerts { get; set; } = new();
    public List<ComplianceRuleResult> RuleSummary { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class ComplianceMetrics
{
    public int TotalAlerts { get; set; }
    public int ResolvedAlerts { get; set; }
    public int PendingAlerts { get; set; }
    public double AverageResolutionTime { get; set; } // In hours
    public double CompliancePercentage { get; set; }
}

public class ComplianceMonitoringSchedule
{
    public string Id { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public string? RuleId { get; set; } // If null, monitor all rules
    public TimeSpan Frequency { get; set; }
    public TimeSpan? StartTime { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastRun { get; set; }
    public DateTime? NextRun { get; set; }
    public List<string> NotificationRecipients { get; set; } = new();
}

public class ComplianceHistoryEntry
{
    public string Id { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? RuleId { get; set; }
    public string? AlertId { get; set; }
    public ComplianceStatus? StatusBefore { get; set; }
    public ComplianceStatus? StatusAfter { get; set; }
}

public enum ComplianceStatus
{
    Compliant,
    Warning,
    NonCompliant,
    Unknown,
    NotApplicable
}

public enum ComplianceRuleCategory
{
    CapitalAdequacy,
    LoanClassification,
    Provisioning,
    LargeExposures,
    LiquidityRatio,
    RegulatoryReporting,
    RiskManagement,
    AntiMoneyLaundering,
    KnowYourCustomer,
    DataProtection,
    OperationalRisk,
    CreditRisk,
    MarketRisk
}

public enum ComplianceSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum ComplianceAlertStatus
{
    Active,
    Acknowledged,
    InProgress,
    Resolved,
    Dismissed,
    Escalated
}