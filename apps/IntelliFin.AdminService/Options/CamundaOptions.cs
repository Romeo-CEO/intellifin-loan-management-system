namespace IntelliFin.AdminService.Options;

public sealed class CamundaOptions
{
    public const string SectionName = "Camunda";

    public string? BaseUrl { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Scope { get; set; }
    public bool FailOpen { get; set; }
    public int TokenRefreshBufferSeconds { get; set; } = 60;
}
