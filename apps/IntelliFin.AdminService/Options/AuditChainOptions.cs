namespace IntelliFin.AdminService.Options;

public sealed class AuditChainOptions
{
    public const string SectionName = "AuditChain";
    public int VerificationIntervalHours { get; set; } = 24;
}
