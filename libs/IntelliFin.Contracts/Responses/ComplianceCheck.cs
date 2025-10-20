namespace IntelliFin.Contracts.Responses;

public class ComplianceCheck
{
    public string CheckId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IssueSeverity Severity { get; set; }
}
