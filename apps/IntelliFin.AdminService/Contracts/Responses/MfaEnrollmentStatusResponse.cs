using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public class MfaEnrollmentStatusResponse
{
    public bool Enrolled { get; set; }
    public DateTime? EnrolledAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
