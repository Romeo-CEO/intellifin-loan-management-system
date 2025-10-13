namespace IntelliFin.AdminService.Models;

public class ContainerImage
{
    public long Id { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string ImageDigest { get; set; } = string.Empty;

    public string Registry { get; set; } = string.Empty;

    public string? BuildNumber { get; set; }

    public string? GitCommitSha { get; set; }

    public DateTime BuildTimestamp { get; set; }

    public bool IsSigned { get; set; }

    public bool SignatureVerified { get; set; }

    public DateTime? SignatureTimestamp { get; set; }

    public string? SignedBy { get; set; }

    public bool HasSbom { get; set; }

    public string? SbomPath { get; set; }

    public string? SbomFormat { get; set; }

    public bool VulnerabilityScanCompleted { get; set; }

    public DateTime? VulnerabilityScanTimestamp { get; set; }

    public int CriticalCount { get; set; }

    public int HighCount { get; set; }

    public int MediumCount { get; set; }

    public int LowCount { get; set; }

    public bool DeployedToProduction { get; set; }

    public DateTime? DeploymentTimestamp { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<Vulnerability> Vulnerabilities { get; set; } = new List<Vulnerability>();
}
