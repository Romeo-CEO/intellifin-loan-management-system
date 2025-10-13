using System;
using System.Collections.Generic;

namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record ActiveSessionDto
{
    public Guid ElevationId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
    public DateTime ActivatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string ApprovedBy { get; init; } = string.Empty;
    public string ManagerName { get; init; } = string.Empty;
};
