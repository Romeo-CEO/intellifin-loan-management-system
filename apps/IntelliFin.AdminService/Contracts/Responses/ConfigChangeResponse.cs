using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record ConfigChangeResponse
{
    public Guid ChangeRequestId { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool RequiresApproval { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime? EstimatedApprovalTime { get; init; }
};
