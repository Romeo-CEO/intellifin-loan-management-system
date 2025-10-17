namespace IntelliFin.Contracts.Responses;

public class PermissionAssignmentResult
{
    public string PermissionId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
}
