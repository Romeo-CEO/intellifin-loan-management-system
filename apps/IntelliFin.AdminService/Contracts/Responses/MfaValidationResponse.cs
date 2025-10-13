using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public class MfaValidationResponse
{
    public bool Success { get; set; }
    public string? MfaToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int FailedAttempts { get; set; }
    public int RemainingAttempts { get; set; }
    public string? Message { get; set; }
}
