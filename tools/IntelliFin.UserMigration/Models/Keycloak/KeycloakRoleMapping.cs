using System.Text.Json.Serialization;

namespace IntelliFin.UserMigration.Models.Keycloak;

public sealed class KeycloakRoleMapping
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
