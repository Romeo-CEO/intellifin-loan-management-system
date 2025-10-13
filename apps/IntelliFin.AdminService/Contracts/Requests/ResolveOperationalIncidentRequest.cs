using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public sealed class ResolveOperationalIncidentRequest
{
    public DateTime? ResolvedAt { get; set; }

    [StringLength(2000)]
    public string? ResolutionSummary { get; set; }

    [StringLength(50)]
    public string? AutomationStatus { get; set; }

    public DateTime? PostmortemDueAt { get; set; }

    public bool TriggerPostmortemWorkflow { get; set; } = true;
}
