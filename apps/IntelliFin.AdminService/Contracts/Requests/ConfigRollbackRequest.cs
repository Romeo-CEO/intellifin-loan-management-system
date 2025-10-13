using System;
using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record ConfigRollbackRequest
{
    public Guid? ChangeRequestId { get; init; }

    [MaxLength(100)]
    public string? GitCommitSha { get; init; }

    [Required]
    [MaxLength(500)]
    public required string Reason { get; init; }
};
