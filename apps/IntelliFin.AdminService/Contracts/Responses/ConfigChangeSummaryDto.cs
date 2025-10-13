using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record ConfigChangeSummaryDto
{
    public Guid ChangeRequestId { get; init; }
    public string ConfigKey { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
    public string Sensitivity { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
};
