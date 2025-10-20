namespace IntelliFin.Contracts.Requests;

public class BulkUpdateRolePermissionsRequest
{
    public string RoleId { get; set; } = string.Empty;
    public string[] Permissions { get; set; } = Array.Empty<string>();
}
