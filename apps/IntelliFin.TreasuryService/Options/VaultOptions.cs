namespace IntelliFin.TreasuryService.Options;

public class VaultOptions
{
    public const string SectionName = "Vault";

    public string Address { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}

