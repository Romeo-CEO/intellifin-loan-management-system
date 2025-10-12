using System.Text.Json.Serialization;

namespace IntelliFin.AdminService.Models.Keycloak;

public sealed record KeycloakRoleRepresentation
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("composite")]
    public bool? Composite { get; init; }

    [JsonPropertyName("clientRole")]
    public bool? ClientRole { get; init; }

    [JsonPropertyName("containerId")]
    public string? ContainerId { get; init; }
};
