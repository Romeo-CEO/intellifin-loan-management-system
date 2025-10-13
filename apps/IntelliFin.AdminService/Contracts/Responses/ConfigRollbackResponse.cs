using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record ConfigRollbackResponse
{
    public Guid OriginalChangeRequestId { get; init; }
    public Guid NewChangeRequestId { get; init; }
    public Guid RollbackId { get; init; }
    public string Message { get; init; } = string.Empty;
};
