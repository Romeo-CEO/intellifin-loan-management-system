using System.Text.Json.Serialization;

namespace IntelliFin.UserMigration.Models.Keycloak;

public sealed class KeycloakUserRepresentation
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("emailVerified")]
    public bool EmailVerified { get; set; }

    [JsonPropertyName("attributes")]
    public Dictionary<string, List<string>> Attributes { get; set; } = new();

    [JsonPropertyName("requiredActions")]
    public List<string> RequiredActions { get; set; } = new();
}
