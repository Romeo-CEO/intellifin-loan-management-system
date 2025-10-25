using System.ComponentModel.DataAnnotations;

namespace IntelliFin.Shared.DomainModels.Entities;

/// <summary>
/// Tracks Vault configuration versions used for credit assessments.
/// Ensures complete traceability of which rules were in effect for each assessment.
/// Created in Story 1.2 for Credit Assessment microservice.
/// </summary>
public class AssessmentConfigVersion
{
    /// <summary>
    /// Unique identifier for the configuration version record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Semantic version identifier (e.g., "v1.2.3", "v2.0.0").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Complete configuration snapshot from Vault (JSON format).
    /// Includes all rules, thresholds, weights, and decision matrix.
    /// </summary>
    [Required]
    public string ConfigSnapshot { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this configuration version was loaded from Vault.
    /// </summary>
    [Required]
    public DateTime LoadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Service instance or admin user who loaded this configuration.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string LoadedBy { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if this is the currently active configuration version.
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// Optional effective start date for this configuration version.
    /// </summary>
    public DateTime? EffectiveFrom { get; set; }

    /// <summary>
    /// Optional effective end date for this configuration version.
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>
    /// Optional notes or change description for this configuration version.
    /// </summary>
    [MaxLength(1000)]
    public string? ChangeNotes { get; set; }
}
