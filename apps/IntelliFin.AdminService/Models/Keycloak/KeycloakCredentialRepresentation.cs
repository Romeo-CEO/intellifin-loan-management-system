using System.Text.Json.Serialization;

namespace IntelliFin.AdminService.Models.Keycloak;

public sealed record KeycloakCredentialRepresentation
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "password";

    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("temporary")]
    public bool Temporary { get; init; } = true;
};
