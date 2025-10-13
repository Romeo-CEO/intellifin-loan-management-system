using System;

namespace IntelliFin.AdminService.Models;

public class SodException
{
    public long Id { get; set; }
    public Guid ExceptionId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string RequestedRole { get; set; } = string.Empty;
    public string ConflictingRolesJson { get; set; } = string.Empty;
    public string BusinessJustification { get; set; } = string.Empty;
    public int ExceptionDuration { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComments { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedBy { get; set; }
    public string? RevocationReason { get; set; }
    public string? CamundaProcessInstanceId { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
