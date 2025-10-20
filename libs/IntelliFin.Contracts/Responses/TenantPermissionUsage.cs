namespace IntelliFin.Contracts.Responses;

public class TenantPermissionUsage
{
    public string PermissionId { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}
