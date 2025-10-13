namespace IntelliFin.AdminService.Models;

public class SignatureVerificationAudit
{
    public long Id { get; set; }

    public string ImageDigest { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public DateTime VerificationTimestamp { get; set; }

    public string VerificationResult { get; set; } = string.Empty;

    public string VerificationMethod { get; set; } = string.Empty;

    public string? VerifiedBy { get; set; }

    public string? VerificationContext { get; set; }

    public string? ErrorMessage { get; set; }
}
