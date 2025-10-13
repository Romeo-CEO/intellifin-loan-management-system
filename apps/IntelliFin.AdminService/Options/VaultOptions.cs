using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Options;

public class VaultOptions
{
    public const string SectionName = "Vault";

    [Required]
    [Url]
    public string Address { get; set; } = "http://vault.vault.svc.cluster.local:8200";

    /// <summary>
    /// Token used by the Admin Service to interact with Vault's administrative endpoints.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Optional Vault namespace when using Vault Enterprise.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Timeout in seconds for outbound Vault API calls.
    /// </summary>
    [Range(5, 120)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Indicates whether Vault integration is enabled. Disabled in local/dev environments by default.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Optional logical name of the Vault audit mount that stores database lease events.
    /// </summary>
    public string? AuditMountPath { get; set; }

    /// <summary>
    /// Optional base path for database secrets (defaults to "database").
    /// </summary>
    public string SecretsEnginePath { get; set; } = "database";
}
