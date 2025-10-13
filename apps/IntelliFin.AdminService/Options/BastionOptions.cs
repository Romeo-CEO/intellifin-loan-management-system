namespace IntelliFin.AdminService.Options;

public sealed class BastionOptions
{
    public const string SectionName = "Bastion";

    public string SessionBucketName { get; set; } = "bastion-sessions";

    public string? SessionPrefix { get; set; }
        = string.Empty;

    public string BastionHostname { get; set; } = "bastion.intellifin.local";

    public string DefaultEnvironment { get; set; } = "staging";

    public string AccessWorkflowProcessId { get; set; } = "bastion-access-request";

    public string EmergencyWorkflowProcessId { get; set; } = "bastion-emergency-access";

    public int MaxAccessDurationHours { get; set; } = 8;

    public int MinAccessDurationHours { get; set; } = 1;

    public int DefaultAccessDurationHours { get; set; } = 2;

    public int SessionRetentionDays { get; set; } = 365 * 3;

    public int EmergencyAccessHours { get; set; } = 1;

    public int SessionAssociationLookbackHours { get; set; } = 24;
}
