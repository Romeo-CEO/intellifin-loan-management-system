using System.Collections.Generic;

namespace IntelliFin.Contracts.Responses;

public class TenantRoleSummary
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public List<string> AssignedUserIds { get; set; } = new();
}
