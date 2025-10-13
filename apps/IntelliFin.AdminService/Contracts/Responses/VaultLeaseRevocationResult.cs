namespace IntelliFin.AdminService.Contracts.Responses;

public class VaultLeaseRevocationResult
{
    public string LeaseId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime RevokedAtUtc { get; set; }
        = DateTime.UtcNow;

    public string CorrelationId { get; set; } = string.Empty;
}
