namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record ConfigurationValueDto
{
    public string ConfigKey { get; init; } = string.Empty;
    public string? CurrentValue { get; init; }
    public string Sensitivity { get; init; } = string.Empty;
    public bool RequiresApproval { get; init; }
};
