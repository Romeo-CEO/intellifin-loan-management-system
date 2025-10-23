namespace IntelliFin.CreditAssessmentService.Options;

/// <summary>
/// Feature flags controlling gradual rollout scenarios.
/// </summary>
public class FeatureFlagOptions
{
    public const string SectionName = "FeatureFlags";

    public bool UseStandaloneService { get; set; }
    public bool EnableExplainability { get; set; } = true;
    public bool EnableManualOverrideWorkflow { get; set; } = true;
    public bool EnableEventPublishing { get; set; } = true;
}
