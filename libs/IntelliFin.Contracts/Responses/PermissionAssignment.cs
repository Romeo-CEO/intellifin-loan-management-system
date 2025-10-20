namespace IntelliFin.Contracts.Responses;

public class PermissionAssignment
{
    public string PermissionId { get; set; } = string.Empty;
    public string AssignedToRoleId { get; set; } = string.Empty;
    public string AssignedBy { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}
