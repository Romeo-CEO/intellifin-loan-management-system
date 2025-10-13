using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record ConfigChangeApprovalDto
{
    [MaxLength(500)]
    public string? Comments { get; init; }
};
