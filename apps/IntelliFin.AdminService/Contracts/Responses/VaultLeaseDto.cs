namespace IntelliFin.AdminService.Contracts.Responses;

public class VaultLeaseDto
{
    public string LeaseId { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public bool Renewable { get; set; }
        = true;

    public string Status { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? LastRenewedAtUtc { get; set; }
        = DateTime.UtcNow;

    public int RemainingSeconds { get; set; }
        = 0;

    public string? CorrelationId { get; set; }
        = string.Empty;
}
