namespace IntelliFin.AdminService.Contracts.Responses;

public class MfaConfigDto
{
    public string OperationName { get; set; } = string.Empty;
    public bool RequiresMfa { get; set; }
    public int TimeoutMinutes { get; set; }
    public string? Description { get; set; }
}
