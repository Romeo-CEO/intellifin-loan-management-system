namespace IntelliFin.AdminService.Options;

public sealed class AuditArchiveOptions
{
    public const string SectionName = "AuditArchive";

    public string BucketName { get; set; } = "audit-logs";
    public string AccessLogBucketName { get; set; } = "audit-access-logs";
    public int RetentionDays { get; set; } = 3_654; // 10 years
    public int ExportHourUtc { get; set; } = 0;
    public int ExportMinuteUtc { get; set; } = 5;
    public bool EnableExports { get; set; } = true;
    public int CleanupRetentionDays { get; set; } = 90;
    public int MetadataRetentionGraceDays { get; set; } = 1;
    public int ReplicationCheckIntervalMinutes { get; set; } = 30;
}
