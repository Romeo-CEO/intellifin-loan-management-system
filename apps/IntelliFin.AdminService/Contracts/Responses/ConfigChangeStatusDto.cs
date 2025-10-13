using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record ConfigChangeStatusDto
{
    public Guid ChangeRequestId { get; init; }
    public string ConfigKey { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Sensitivity { get; init; } = string.Empty;
    public string? ApprovedBy { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime? AppliedAt { get; init; }
    public string? GitCommitSha { get; init; }
    public string? CamundaProcessInstanceId { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
};
