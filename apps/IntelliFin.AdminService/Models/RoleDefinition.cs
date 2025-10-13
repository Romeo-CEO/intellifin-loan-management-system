using System;

namespace IntelliFin.AdminService.Models;

public class RoleDefinition
{
    public int Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? RiskLevel { get; set; }
    public bool RequiresApproval { get; set; }
    public string? ApprovalWorkflow { get; set; }
    public int? MaxAssignments { get; set; }
    public DateTime CreatedAt { get; set; }
}
