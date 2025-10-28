using System;

namespace IntelliFin.LoanOriginationService.Models;

/// <summary>
/// Response model from Client Management Service containing KYC and AML verification status.
/// </summary>
public class ClientVerificationResponse
{
    /// <summary>
    /// Unique identifier of the client.
    /// </summary>
    public Guid ClientId { get; set; }

    /// <summary>
    /// KYC verification status: Approved, Pending, Expired, or Revoked.
    /// </summary>
    public string KycStatus { get; set; } = string.Empty;

    /// <summary>
    /// AML verification status: Cleared, Pending, or Flagged.
    /// </summary>
    public string AmlStatus { get; set; } = string.Empty;

    /// <summary>
    /// Date and time when KYC was approved.
    /// </summary>
    public DateTime? KycApprovedAt { get; set; }

    /// <summary>
    /// Date when KYC verification expires (typically 12 months from approval).
    /// </summary>
    public DateTime? KycExpiryDate { get; set; }

    /// <summary>
    /// Level of verification performed: Basic, Enhanced, or Full.
    /// </summary>
    public string VerificationLevel { get; set; } = string.Empty;

    /// <summary>
    /// Risk rating assigned to the client: Low, Medium, or High.
    /// </summary>
    public string RiskRating { get; set; } = string.Empty;
}
