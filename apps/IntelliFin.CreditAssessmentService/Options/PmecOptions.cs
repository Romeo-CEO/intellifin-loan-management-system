namespace IntelliFin.CreditAssessmentService.Options;

/// <summary>
/// Options controlling PMEC integration.
/// </summary>
public class PmecOptions
{
    public const string SectionName = "Pmec";

    public string BaseUrl { get; set; } = "https://pmec.gov.zm/api";
    public string ApiKey { get; set; } = string.Empty;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public int RetryCount { get; set; } = 3;
}
