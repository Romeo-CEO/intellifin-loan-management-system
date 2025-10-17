namespace IntelliFin.Shared.DomainModels.Entities;

public class SoDRule
{
    public Guid RuleId { get; set; } = Guid.NewGuid();
    public string RuleName { get; set; } = string.Empty;
    public string ConflictingPermissions { get; set; } = string.Empty; // JSON array string
    public string Enforcement { get; set; } = "strict"; // strict | warning
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}