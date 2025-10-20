namespace IntelliFin.Contracts.Responses;

public class RolePermissionResult
{
    public string RoleId { get; set; } = string.Empty;
    public PermissionAssignmentResult[] AddedPermissions { get; set; } = Array.Empty<PermissionAssignmentResult>();
}
