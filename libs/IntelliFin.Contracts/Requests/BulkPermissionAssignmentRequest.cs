using System.Collections.Generic;

namespace IntelliFin.Contracts.Requests;

public class BulkPermissionAssignmentRequest
{
    public string RoleId { get; set; } = string.Empty;
    public List<string> PermissionIds { get; set; } = new();
}
