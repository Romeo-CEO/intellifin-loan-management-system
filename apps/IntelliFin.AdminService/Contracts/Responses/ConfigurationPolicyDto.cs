namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record ConfigurationPolicyDto
{
    public int Id { get; init; }
    public string ConfigKey { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool RequiresApproval { get; init; }
    public string Sensitivity { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? CurrentValue { get; init; }
};
