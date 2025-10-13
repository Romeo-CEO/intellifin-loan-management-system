namespace IntelliFin.AdminService.Models;

public class VaultLeaseRecord
{
    public long Id { get; set; }

    public string LeaseId { get; set; } = default!;

    public string ServiceName { get; set; } = default!;

    public string DatabaseName { get; set; } = default!;

    public string Username { get; set; } = default!;

    public bool Renewable { get; set; }

    public string Status { get; set; } = VaultLeaseStatus.Active;

    public DateTime IssuedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? LastRenewedAtUtc { get; set; }

    public DateTime? LastSeenAtUtc { get; set; }

    public string? MetadataJson { get; set; }

    public string? CorrelationId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedBy { get; set; }

    public string? RevocationReason { get; set; }
}

public static class VaultLeaseStatus
{
    public const string Active = "Active";
    public const string PendingRevocation = "PendingRevocation";
    public const string Revoked = "Revoked";
    public const string Expired = "Expired";
}
