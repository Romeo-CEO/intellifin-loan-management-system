namespace IntelliFin.CreditAssessmentService.Services.Models;

/// <summary>
/// Represents rule configuration retrieved from Vault.
/// </summary>
public sealed class VaultRuleConfiguration
{
    public string Version { get; init; } = "v0";
    public IReadOnlyCollection<VaultRule> Rules { get; init; } = Array.Empty<VaultRule>();
}

public sealed class VaultRule
{
    public string Key { get; init; } = string.Empty;
    public string Expression { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    public string Category { get; init; } = string.Empty;
}

/// <summary>
/// Represents risk thresholds from Vault.
/// </summary>
public sealed class VaultThresholdConfiguration
{
    public string Version { get; init; } = "v0";
    public decimal DebtToIncomeThreshold { get; init; } = 0.4m;
    public Dictionary<string, decimal> GradeThresholds { get; init; } = new()
    {
        ["A"] = 850,
        ["B"] = 750,
        ["C"] = 650,
        ["D"] = 550,
        ["E"] = 450,
        ["F"] = 0
    };
    public Dictionary<string, string> DecisionMatrix { get; init; } = new()
    {
        ["A"] = "Approved",
        ["B"] = "Approved",
        ["C"] = "ConditionalApproval",
        ["D"] = "ManualReview",
        ["E"] = "ManualReview",
        ["F"] = "Rejected"
    };
}

/// <summary>
/// Represents TransUnion credentials retrieved from Vault.
/// </summary>
public sealed class VaultTransUnionCredential
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string Version { get; init; } = "v0";
}
