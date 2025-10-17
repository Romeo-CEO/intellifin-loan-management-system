namespace IntelliFin.Contracts.Responses;

public class PermissionValidationResult
{
    public string PermissionId { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? Message { get; set; }
}
