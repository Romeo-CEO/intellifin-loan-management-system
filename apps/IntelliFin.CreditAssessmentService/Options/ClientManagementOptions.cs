namespace IntelliFin.CreditAssessmentService.Options;

/// <summary>
/// Options for interacting with the Client Management API.
/// </summary>
public class ClientManagementOptions
{
    public const string SectionName = "ClientManagement";

    public string BaseUrl { get; set; } = "https://client-management.intellifin.local";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public int RetryCount { get; set; } = 3;
}
