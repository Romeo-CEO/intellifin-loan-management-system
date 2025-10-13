using System;

namespace IntelliFin.AdminService.Models;

public class MfaChallenge
{
    public long Id { get; set; }
    public Guid ChallengeId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string ChallengeCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Initiated";
    public string? CamundaProcessInstanceId { get; set; }
    public DateTime InitiatedAt { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int FailedAttempts { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
}
