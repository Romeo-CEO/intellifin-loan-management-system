using IntelliFin.Shared.DomainModels.Enums;
using System.Linq;

namespace IntelliFin.IdentityService.Models;

public class SoDConflict
{
    public Guid RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public SoDEnforcementLevel Enforcement { get; set; }
    public IReadOnlyCollection<string> ConflictingPermissions { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<string> TriggeringPermissions { get; set; } = Array.Empty<string>();
    public string? AttemptedRoleId { get; set; }
    public string? AttemptedRoleName { get; set; }
}

public class SoDValidationResult
{
    public bool IsAllowed { get; set; } = true;
    public bool WasApplied { get; set; }
    public List<SoDConflict> Conflicts { get; set; } = new();

    public bool HasConflicts => Conflicts.Count > 0;
    public bool HasBlockingConflicts => Conflicts.Any(c => c.Enforcement == SoDEnforcementLevel.Strict);
    public bool HasWarnings => Conflicts.Any(c => c.Enforcement == SoDEnforcementLevel.Warning);
}

public class SoDViolation
{
    public string UserId { get; set; } = string.Empty;
    public string? Username { get; set; }
    public Guid RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public SoDEnforcementLevel Enforcement { get; set; }
    public IReadOnlyCollection<string> ConflictingPermissions { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<string> TriggeringPermissions { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
}

public class SoDViolationReport
{
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public IReadOnlyCollection<SoDViolation> Violations { get; set; } = Array.Empty<SoDViolation>();
    public int TotalViolations => Violations.Count;
}
