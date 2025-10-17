using System.Linq;
using System.Text.Json;

namespace IntelliFin.Shared.DomainModels.Entities;

public class ServiceAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ClientId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string ScopesJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "system";
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }
    public string? KeycloakClientId { get; set; }
    public string? KeycloakSecretVaultPath { get; set; }

    public virtual ICollection<ServiceCredential> Credentials { get; set; } = new List<ServiceCredential>();

    public IReadOnlyCollection<string> GetScopes()
    {
        if (string.IsNullOrWhiteSpace(ScopesJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            var scopes = JsonSerializer.Deserialize<string[]>(ScopesJson);
            return scopes is null
                ? Array.Empty<string>()
                : Array.AsReadOnly(scopes);
        }
        catch
        {
            return Array.AsReadOnly(ScopesJson
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }

    public void SetScopes(IEnumerable<string>? scopes)
    {
        var scopeArray = scopes?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        ScopesJson = JsonSerializer.Serialize(scopeArray);
    }
}
