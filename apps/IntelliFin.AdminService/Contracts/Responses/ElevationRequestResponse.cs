using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record ElevationRequestResponse
{
    public Guid ElevationId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime? EstimatedApprovalTime { get; init; }
};
