namespace IntelliFin.AdminService.Options;

public sealed class CamundaOptions
{
    public const string SectionName = "Camunda";

    public string? BaseUrl { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}
