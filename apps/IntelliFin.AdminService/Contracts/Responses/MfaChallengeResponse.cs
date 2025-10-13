using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public class MfaChallengeResponse
{
    public Guid? ChallengeId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool RequiresEnrollment { get; set; }
    public string? EnrollmentUrl { get; set; }
}
