using System;

namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record OperationalIncidentDto
{
    public Guid IncidentId { get; init; }
    public string AlertName { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? Details { get; init; }
    public string? PagerDutyIncidentId { get; init; }
    public string? SlackThreadUrl { get; init; }
    public Guid? PlaybookId { get; init; }
    public string? PlaybookTitle { get; init; }
    public DateTime DetectedAt { get; init; }
    public DateTime? AcknowledgedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public DateTime? PostmortemDueAt { get; init; }
    public DateTime? PostmortemCompletedAt { get; init; }
    public string? AutomationStatus { get; init; }
    public string? CamundaProcessInstanceId { get; init; }
    public DateTime UpdatedAt { get; init; }
}
