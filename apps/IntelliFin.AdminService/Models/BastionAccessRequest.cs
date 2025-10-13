namespace IntelliFin.AdminService.Models;

public class BastionAccessRequest
{
    public long Id { get; set; }

    public Guid RequestId { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    public string Environment { get; set; } = string.Empty;

    public string? TargetHosts { get; set; }
        = null;

    public int AccessDurationHours { get; set; }
        = 2;

    public string Justification { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public bool RequiresApproval { get; set; }
        = true;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public string? ApprovedBy { get; set; }
        = null;

    public DateTime? ApprovedAt { get; set; }
        = null;

    public string? DeniedBy { get; set; }
        = null;

    public DateTime? DeniedAt { get; set; }
        = null;

    public string? DenialReason { get; set; }
        = null;

    public bool SshCertificateIssued { get; set; }
        = false;

    public string? VaultCertificatePath { get; set; }
        = null;

    public string? CertificateSerialNumber { get; set; }
        = null;

    public string? CertificateContent { get; set; }
        = null;

    public DateTime? CertificateExpiresAt { get; set; }
        = null;

    public string? CamundaProcessInstanceId { get; set; }
        = null;

    public ICollection<BastionSession> Sessions { get; set; } = new List<BastionSession>();
}
