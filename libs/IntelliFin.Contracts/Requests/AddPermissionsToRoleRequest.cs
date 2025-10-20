namespace IntelliFin.Contracts.Requests;

public class AddPermissionsToRoleRequest
{
    public string RoleId { get; set; } = string.Empty;
    public string[] Permissions { get; set; } = Array.Empty<string>();
}
