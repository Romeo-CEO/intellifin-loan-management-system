using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed record ElevationApprovalDto
{
    [Range(1, 480)]
    public int ApprovedDuration { get; init; }
};
