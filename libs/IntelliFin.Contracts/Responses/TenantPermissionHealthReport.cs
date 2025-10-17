using System.Collections.Generic;

namespace IntelliFin.Contracts.Responses;

public class TenantPermissionHealthReport
{
    public string TenantId { get; set; } = string.Empty;
    public int TotalPermissions { get; set; }
    public int CompliantPermissions { get; set; }
    public List<PermissionComplianceIssue> Issues { get; set; } = new();
}
