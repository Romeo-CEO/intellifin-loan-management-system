namespace IntelliFin.CreditAssessmentService.Options;

/// <summary>
/// Options governing TransUnion integration.
/// </summary>
public class TransUnionOptions
{
    public const string SectionName = "TransUnion";

    public string BaseUrl { get; set; } = "https://sandbox.transunion.com";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);
    public int RetryCount { get; set; } = 3;
}
