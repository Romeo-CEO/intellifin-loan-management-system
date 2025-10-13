namespace IntelliFin.AdminService.Options;

public sealed class ConfigurationManagementOptions
{
    public string? GitRepository { get; set; }
    public string GitBranch { get; set; } = "main";
    public string? GitUsername { get; set; }
    public string? GitToken { get; set; }
    public string? GitAuthorName { get; set; }
    public string? GitAuthorEmail { get; set; }
    public string? LocalRepoPath { get; set; }
    public bool InCluster { get; set; } = true;
    public string? KubeConfigPath { get; set; }
}
