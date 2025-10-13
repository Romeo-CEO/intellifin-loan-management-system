using System;

namespace IntelliFin.AdminService.Models;

public class ConfigurationChange
{
    public long Id { get; set; }
    public Guid ChangeRequestId { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string NewValue { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Sensitivity { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string? GitCommitSha { get; set; }
    public string? GitRepository { get; set; }
    public string? GitBranch { get; set; }
    public string? KubernetesNamespace { get; set; }
    public string? KubernetesConfigMap { get; set; }
    public string? ConfigMapKey { get; set; }
    public string? CamundaProcessInstanceId { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
