namespace IntelliFin.AdminService.Models;

public class ElevationRequest
{
    public long Id { get; set; }
    public Guid ElevationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string RequestedRoles { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public int RequestedDuration { get; set; }
    public int? ApprovedDuration { get; set; }
    public string ManagerId { get; set; } = string.Empty;
    public string ManagerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CamundaProcessInstanceId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public string? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }
    public string? RevokedBy { get; set; }
    public string? RevocationReason { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
