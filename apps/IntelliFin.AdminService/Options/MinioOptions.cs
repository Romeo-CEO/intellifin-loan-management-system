namespace IntelliFin.AdminService.Options;

public sealed class MinioOptions
{
    public const string SectionName = "Minio";

    public string Endpoint { get; set; } = "minio:9000";
    public bool UseSsl { get; set; }
        = false;
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string? Region { get; set; }
        = null;
    public int PresignedUrlExpirySeconds { get; set; } = 3_600;
}
