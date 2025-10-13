using System;

namespace IntelliFin.AdminService.Models;

public class MfaEnrollment
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool Enrolled { get; set; }
    public DateTime? EnrolledAt { get; set; }
    public string? SecretKey { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
