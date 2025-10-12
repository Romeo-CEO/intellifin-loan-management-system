using System.Text.Json.Serialization;

namespace IntelliFin.AdminService.Models.Keycloak;

public sealed record KeycloakUserRepresentation
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    [JsonPropertyName("emailVerified")]
    public bool? EmailVerified { get; init; }

    [JsonPropertyName("attributes")]
    public IDictionary<string, IList<string>>? Attributes { get; init; }
};
