namespace IntelliFin.Shared.Audit;

public sealed class AuditClientOptions
{
    public const string SectionName = "AuditService";

    public Uri? BaseAddress { get; set; }
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
