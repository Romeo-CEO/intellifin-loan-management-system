using System;

namespace IntelliFin.AdminService.Models;

public class ConfigurationPolicy
{
    public int Id { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool RequiresApproval { get; set; }
    public string? ApprovalWorkflow { get; set; }
    public string Sensitivity { get; set; } = string.Empty;
    public string? AllowedValuesRegex { get; set; }
    public string? AllowedValuesList { get; set; }
    public string? Description { get; set; }
    public string? CurrentValue { get; set; }
    public string? KubernetesNamespace { get; set; }
    public string? KubernetesConfigMap { get; set; }
    public string? ConfigMapKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
