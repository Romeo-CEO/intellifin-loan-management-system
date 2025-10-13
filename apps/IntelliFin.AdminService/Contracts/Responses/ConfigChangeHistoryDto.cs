using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record ConfigChangeHistoryDto
{
    public Guid ChangeRequestId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? OldValue { get; init; }
    public string NewValue { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
    public DateTime? AppliedAt { get; init; }
    public string RequestedBy { get; init; } = string.Empty;
    public string? ApprovedBy { get; init; }
    public string? GitCommitSha { get; init; }
};
