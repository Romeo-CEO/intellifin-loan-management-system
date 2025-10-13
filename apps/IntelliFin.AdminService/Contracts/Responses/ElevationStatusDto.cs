using System;
using System.Collections.Generic;

namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record ElevationStatusDto
{
    public Guid ElevationId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public IReadOnlyCollection<string> RequestedRoles { get; init; } = Array.Empty<string>();
    public int RequestedDuration { get; init; }
    public int? ApprovedDuration { get; init; }
    public DateTime RequestedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime? ActivatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string ManagerId { get; init; } = string.Empty;
    public string ManagerName { get; init; } = string.Empty;
    public string Justification { get; init; } = string.Empty;
};
