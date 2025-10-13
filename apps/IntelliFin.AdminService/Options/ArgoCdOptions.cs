using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Options;

public class ArgoCdOptions
{
    public const string SectionName = "ArgoCD";

    [Url]
    public string? Url { get; set; }

    public string? Token { get; set; }

    [Range(5, 600)]
    public int TimeoutSeconds { get; set; } = 30;

    public string? DefaultProject { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Token);
}
