using System;

namespace IntelliFin.LoanOriginationService.Exceptions;

/// <summary>
/// Exception thrown when a client's KYC verification has expired (>12 months old).
/// </summary>
public class KycExpiredException : Exception
{
    /// <summary>
    /// The client ID whose KYC has expired.
    /// </summary>
    public Guid ClientId { get; }

    /// <summary>
    /// The date when KYC was approved.
    /// </summary>
    public DateTime KycApprovedAt { get; }

    /// <summary>
    /// The date when KYC expired (12 months from approval).
    /// </summary>
    public DateTime ExpiryDate { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KycExpiredException"/> class.
    /// </summary>
    /// <param name="clientId">The client ID.</param>
    /// <param name="kycApprovedAt">The date when KYC was approved.</param>
    public KycExpiredException(Guid clientId, DateTime kycApprovedAt)
        : base($"Client {clientId} KYC verification expired. Approved on {kycApprovedAt:yyyy-MM-dd}, valid for 12 months. Renewal required.")
    {
        ClientId = clientId;
        KycApprovedAt = kycApprovedAt;
        ExpiryDate = kycApprovedAt.AddMonths(12);
    }
}
