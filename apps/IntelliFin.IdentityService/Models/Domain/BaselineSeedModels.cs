using System.Text.Json.Serialization;

namespace IntelliFin.IdentityService.Models.Domain;

public class BaselineSeedConfiguration
{
    [JsonPropertyName("roles")]
    public RoleDefinition[] Roles { get; set; } = Array.Empty<RoleDefinition>();

    [JsonPropertyName("sodRules")]
    public SoDRuleDefinition[] SodRules { get; set; } = Array.Empty<SoDRuleDefinition>();
}

public class RoleDefinition
{
    [JsonPropertyName("roleName")]
    public string RoleName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = new();
}

public class SoDRuleDefinition
{
    [JsonPropertyName("ruleName")]
    public string RuleName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("conflictingPermissions")]
    public string[] ConflictingPermissions { get; set; } = Array.Empty<string>();

    [JsonPropertyName("enforcement")]
    public string Enforcement { get; set; } = "strict";
}

public class SeedValidationResult
{
    public List<(string RoleName, bool CanCreate)> RoleChecks { get; set; } = new();
    public List<(string RuleName, bool CanCreate)> SoDChecks { get; set; } = new();

    public void AddRoleCheck(string roleName, bool canCreate) => RoleChecks.Add((roleName, canCreate));
    public void AddSoDCheck(string ruleName, bool canCreate) => SoDChecks.Add((ruleName, canCreate));
}
