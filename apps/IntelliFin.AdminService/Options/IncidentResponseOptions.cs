namespace IntelliFin.AdminService.Options;

public sealed class IncidentResponseOptions
{
    public const string SectionName = "IncidentResponse";

    public string AlertmanagerBaseUrl { get; set; } = "http://kube-prometheus-stack-alertmanager:9093";
    public string IncidentWorkflowProcessId { get; set; } = "incident-response";
    public string PostmortemWorkflowProcessId { get; set; } = "incident-postmortem";
    public int DefaultSilenceDurationMinutes { get; set; } = 120;
    public string PlaybookBaseUrl { get; set; } = "https://admin.intellifin.com/playbooks";
}
