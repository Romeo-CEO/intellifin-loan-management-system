namespace IntelliFin.ClientManagement.Models.Analytics;

/// <summary>
/// KYC performance metrics for a given time period
/// Provides comprehensive statistics on KYC processing
/// </summary>
public class KycPerformanceMetrics
{
    /// <summary>
    /// Start of the reporting period
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// End of the reporting period
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Branch ID (null for system-wide metrics)
    /// </summary>
    public Guid? BranchId { get; set; }

    /// <summary>
    /// Total number of KYC processes started
    /// </summary>
    public int TotalStarted { get; set; }

    /// <summary>
    /// Total number of KYC processes completed
    /// </summary>
    public int TotalCompleted { get; set; }

    /// <summary>
    /// Total number of KYC processes rejected
    /// </summary>
    public int TotalRejected { get; set; }

    /// <summary>
    /// Total number of KYC processes in progress
    /// </summary>
    public int TotalInProgress { get; set; }

    /// <summary>
    /// Total number of KYC processes pending
    /// </summary>
    public int TotalPending { get; set; }

    /// <summary>
    /// Total number of EDD escalations
    /// </summary>
    public int TotalEddEscalations { get; set; }

    /// <summary>
    /// Completion rate (percentage)
    /// </summary>
    public double CompletionRate { get; set; }

    /// <summary>
    /// Average processing time in hours
    /// </summary>
    public double AverageProcessingTimeHours { get; set; }

    /// <summary>
    /// Median processing time in hours
    /// </summary>
    public double MedianProcessingTimeHours { get; set; }

    /// <summary>
    /// EDD escalation rate (percentage)
    /// </summary>
    public double EddEscalationRate { get; set; }

    /// <summary>
    /// SLA compliance rate (percentage)
    /// KYC processes completed within 24 hours
    /// </summary>
    public double SlaComplianceRate { get; set; }

    /// <summary>
    /// Number of processes exceeding SLA
    /// </summary>
    public int SlaBreaches { get; set; }

    /// <summary>
    /// Average SLA breach time in hours (for breached processes)
    /// </summary>
    public double? AverageSlaBreachTimeHours { get; set; }
}

/// <summary>
/// Document verification metrics
/// </summary>
public class DocumentMetrics
{
    /// <summary>
    /// Total documents uploaded
    /// </summary>
    public int TotalUploaded { get; set; }

    /// <summary>
    /// Total documents verified
    /// </summary>
    public int TotalVerified { get; set; }

    /// <summary>
    /// Total documents rejected
    /// </summary>
    public int TotalRejected { get; set; }

    /// <summary>
    /// Total documents pending verification
    /// </summary>
    public int TotalPending { get; set; }

    /// <summary>
    /// Average verification time in hours
    /// </summary>
    public double AverageVerificationTimeHours { get; set; }

    /// <summary>
    /// Verification rate (percentage)
    /// </summary>
    public double VerificationRate { get; set; }

    /// <summary>
    /// Rejection rate (percentage)
    /// </summary>
    public double RejectionRate { get; set; }

    /// <summary>
    /// Dual-control compliance rate (percentage)
    /// Documents verified by different officer than uploader
    /// </summary>
    public double DualControlComplianceRate { get; set; }

    /// <summary>
    /// Most common rejection reasons
    /// </summary>
    public List<RejectionReasonStat> TopRejectionReasons { get; set; } = new();
}

/// <summary>
/// Rejection reason statistics
/// </summary>
public class RejectionReasonStat
{
    /// <summary>
    /// Rejection reason
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Number of occurrences
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Percentage of total rejections
    /// </summary>
    public double Percentage { get; set; }
}

/// <summary>
/// AML screening metrics
/// </summary>
public class AmlMetrics
{
    /// <summary>
    /// Total screenings performed
    /// </summary>
    public int TotalScreenings { get; set; }

    /// <summary>
    /// Number of sanctions hits
    /// </summary>
    public int SanctionsHits { get; set; }

    /// <summary>
    /// Number of PEP matches
    /// </summary>
    public int PepMatches { get; set; }

    /// <summary>
    /// Sanctions hit rate (percentage)
    /// </summary>
    public double SanctionsHitRate { get; set; }

    /// <summary>
    /// PEP match rate (percentage)
    /// </summary>
    public double PepMatchRate { get; set; }

    /// <summary>
    /// Average screening time in seconds
    /// </summary>
    public double AverageScreeningTimeSeconds { get; set; }

    /// <summary>
    /// Risk level distribution
    /// </summary>
    public Dictionary<string, int> RiskLevelDistribution { get; set; } = new();

    /// <summary>
    /// EDD escalations triggered by AML
    /// </summary>
    public int AmlTriggeredEdd { get; set; }
}

/// <summary>
/// EDD workflow metrics
/// </summary>
public class EddMetrics
{
    /// <summary>
    /// Total EDD cases initiated
    /// </summary>
    public int TotalInitiated { get; set; }

    /// <summary>
    /// Total EDD cases approved
    /// </summary>
    public int TotalApproved { get; set; }

    /// <summary>
    /// Total EDD cases rejected
    /// </summary>
    public int TotalRejected { get; set; }

    /// <summary>
    /// Total EDD cases in progress
    /// </summary>
    public int TotalInProgress { get; set; }

    /// <summary>
    /// Average EDD processing time in days
    /// </summary>
    public double AverageProcessingTimeDays { get; set; }

    /// <summary>
    /// EDD approval rate (percentage)
    /// </summary>
    public double ApprovalRate { get; set; }

    /// <summary>
    /// EDD rejection rate (percentage)
    /// </summary>
    public double RejectionRate { get; set; }

    /// <summary>
    /// Average time to compliance review in hours
    /// </summary>
    public double AverageTimeToComplianceHours { get; set; }

    /// <summary>
    /// Average time to CEO approval in hours (after compliance)
    /// </summary>
    public double AverageTimeToCeoHours { get; set; }

    /// <summary>
    /// Risk acceptance level distribution
    /// </summary>
    public Dictionary<string, int> RiskAcceptanceDistribution { get; set; } = new();

    /// <summary>
    /// EDD escalation reasons distribution
    /// </summary>
    public List<EscalationReasonStat> TopEscalationReasons { get; set; } = new();
}

/// <summary>
/// EDD escalation reason statistics
/// </summary>
public class EscalationReasonStat
{
    /// <summary>
    /// Escalation reason
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Number of occurrences
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Percentage of total escalations
    /// </summary>
    public double Percentage { get; set; }
}

/// <summary>
/// Officer performance metrics
/// </summary>
public class OfficerPerformanceMetrics
{
    /// <summary>
    /// Officer user ID
    /// </summary>
    public string OfficerId { get; set; } = string.Empty;

    /// <summary>
    /// Officer name
    /// </summary>
    public string OfficerName { get; set; } = string.Empty;

    /// <summary>
    /// Total clients processed
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Total clients completed
    /// </summary>
    public int TotalCompleted { get; set; }

    /// <summary>
    /// Total clients rejected
    /// </summary>
    public int TotalRejected { get; set; }

    /// <summary>
    /// Average processing time in hours
    /// </summary>
    public double AverageProcessingTimeHours { get; set; }

    /// <summary>
    /// SLA compliance rate (percentage)
    /// </summary>
    public double SlaComplianceRate { get; set; }

    /// <summary>
    /// Documents uploaded
    /// </summary>
    public int DocumentsUploaded { get; set; }

    /// <summary>
    /// Documents verified
    /// </summary>
    public int DocumentsVerified { get; set; }

    /// <summary>
    /// Completion rate (percentage)
    /// </summary>
    public double CompletionRate { get; set; }
}

/// <summary>
/// Risk distribution metrics
/// </summary>
public class RiskDistributionMetrics
{
    /// <summary>
    /// Number of low risk clients
    /// </summary>
    public int LowRisk { get; set; }

    /// <summary>
    /// Number of medium risk clients
    /// </summary>
    public int MediumRisk { get; set; }

    /// <summary>
    /// Number of high risk clients
    /// </summary>
    public int HighRisk { get; set; }

    /// <summary>
    /// Average risk score
    /// </summary>
    public double AverageRiskScore { get; set; }

    /// <summary>
    /// Median risk score
    /// </summary>
    public double MedianRiskScore { get; set; }

    /// <summary>
    /// Percentage of clients requiring EDD
    /// </summary>
    public double EddRequiredPercentage { get; set; }
}

/// <summary>
/// KYC funnel metrics (conversion rates through stages)
/// </summary>
public class KycFunnelMetrics
{
    /// <summary>
    /// Clients created
    /// </summary>
    public int ClientsCreated { get; set; }

    /// <summary>
    /// Clients with documents uploaded
    /// </summary>
    public int DocumentsUploaded { get; set; }

    /// <summary>
    /// Clients with documents verified
    /// </summary>
    public int DocumentsVerified { get; set; }

    /// <summary>
    /// Clients passed AML screening
    /// </summary>
    public int AmlScreeningPassed { get; set; }

    /// <summary>
    /// Clients with risk assessment complete
    /// </summary>
    public int RiskAssessmentComplete { get; set; }

    /// <summary>
    /// Clients KYC completed
    /// </summary>
    public int KycCompleted { get; set; }

    /// <summary>
    /// Conversion rate from creation to document upload (%)
    /// </summary>
    public double DocumentUploadConversion { get; set; }

    /// <summary>
    /// Conversion rate from upload to verification (%)
    /// </summary>
    public double VerificationConversion { get; set; }

    /// <summary>
    /// Conversion rate from verification to AML pass (%)
    /// </summary>
    public double AmlPassConversion { get; set; }

    /// <summary>
    /// Overall conversion rate (creation to completion) (%)
    /// </summary>
    public double OverallConversionRate { get; set; }
}
