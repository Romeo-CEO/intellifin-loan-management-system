using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Options;

public sealed class SbomOptions
{
    public const string SectionName = "Sbom";

    [Required]
    public string BucketName { get; set; } = "intellifin-sboms";

    /// <summary>
    /// Optional prefix inside the bucket where SBOM files are stored.
    /// </summary>
    public string? Prefix { get; set; }

    [Range(1, 3600)]
    public int DownloadTimeoutSeconds { get; set; } = 60;
}
