namespace IntelliFin.ClientManagement.Domain.Events;

/// <summary>
/// Domain event published when EDD report is generated
/// </summary>
public class EddReportGeneratedEvent
{
    /// <summary>
    /// Client unique identifier
    /// </summary>
    public Guid ClientId { get; set; }

    /// <summary>
    /// KYC status unique identifier
    /// </summary>
    public Guid KycStatusId { get; set; }

    /// <summary>
    /// Client full name
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// MinIO object key for the EDD report
    /// </summary>
    public string ReportObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// Overall risk level determined by report
    /// </summary>
    public string OverallRiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Reason EDD was triggered
    /// </summary>
    public string EddReason { get; set; } = string.Empty;

    /// <summary>
    /// When the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Correlation ID for tracking
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Camunda process instance ID for the EDD workflow
    /// </summary>
    public string? ProcessInstanceId { get; set; }
}
