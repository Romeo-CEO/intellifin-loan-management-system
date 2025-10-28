using System;

namespace IntelliFin.LoanOriginationService.Exceptions;

/// <summary>
/// Exception thrown when a client's KYC status is not "Approved" and loan application is attempted.
/// </summary>
public class KycNotVerifiedException : Exception
{
    /// <summary>
    /// The client ID whose KYC is not verified.
    /// </summary>
    public Guid ClientId { get; }

    /// <summary>
    /// The current KYC status of the client.
    /// </summary>
    public string KycStatus { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KycNotVerifiedException"/> class.
    /// </summary>
    /// <param name="clientId">The client ID.</param>
    /// <param name="kycStatus">The current KYC status.</param>
    public KycNotVerifiedException(Guid clientId, string kycStatus)
        : base($"Client {clientId} KYC status is '{kycStatus}'. KYC approval required before loan application.")
    {
        ClientId = clientId;
        KycStatus = kycStatus;
    }
}
