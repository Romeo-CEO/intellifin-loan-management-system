namespace IntelliFin.AdminService.Options;

public sealed class ElevationOptions
{
    public const string SectionName = "Elevation";

    public int MaxDurationMinutes { get; set; } = 480;
    public int MinJustificationLength { get; set; } = 20;
    public int ApprovalTimeoutHours { get; set; } = 24;
    public int ExpirationCheckIntervalMinutes { get; set; } = 5;
    public bool NotifyUserOnExpiration { get; set; } = true;
}
