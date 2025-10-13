using System;
using System.Collections.Generic;

namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record IncidentPlaybookDto
{
    public Guid PlaybookId { get; init; }
    public string AlertName { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string DiagnosisSteps { get; init; } = string.Empty;
    public string ResolutionSteps { get; init; } = string.Empty;
    public string EscalationPath { get; init; } = string.Empty;
    public string? LinkedRunbookUrl { get; init; }
    public string Owner { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string? AutomationProcessKey { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public IReadOnlyCollection<IncidentPlaybookRunDto> RecentRuns { get; init; } = Array.Empty<IncidentPlaybookRunDto>();
}

public sealed record IncidentPlaybookRunDto
{
    public Guid RunId { get; init; }
    public Guid IncidentId { get; init; }
    public string AlertName { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public DateTime? AcknowledgedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public double? ResolutionMinutes { get; init; }
    public bool AutomationInvoked { get; init; }
    public string? AutomationOutcome { get; init; }
    public string? ResolutionSummary { get; init; }
    public string? RecordedBy { get; init; }
    public string? CamundaProcessInstanceId { get; init; }
    public string? PagerDutyIncidentId { get; init; }
}
