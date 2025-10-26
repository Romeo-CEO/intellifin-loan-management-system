using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Request DTO for initiating KYC process
/// </summary>
public class InitiateKycRequest
{
    /// <summary>
    /// Optional notes about KYC initiation
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
