using System.Collections.Generic;

namespace IntelliFin.Contracts.Requests;

public class ReplacePermissionsRequest
{
    public string RoleId { get; set; } = string.Empty;
    public List<string> PermissionIds { get; set; } = new();
}
