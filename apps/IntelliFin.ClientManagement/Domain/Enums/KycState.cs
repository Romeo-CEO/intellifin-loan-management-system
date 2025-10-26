namespace IntelliFin.ClientManagement.Domain.Enums;

/// <summary>
/// KYC (Know Your Customer) compliance states
/// Tracks progression through KYC/AML verification workflow
/// </summary>
public enum KycState
{
    /// <summary>
    /// Initial state when KYC is initiated
    /// Documents not yet collected
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Document collection and verification in progress
    /// Officers are collecting and verifying client documents
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// KYC successfully completed
    /// All documents verified, AML screening passed
    /// TERMINAL STATE - cannot transition from here
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Enhanced Due Diligence required
    /// Triggered by: PEP status, sanctions list, high risk, tampered documents
    /// Requires compliance officer and CEO approval
    /// </summary>
    EDD_Required = 4,

    /// <summary>
    /// KYC rejected - client cannot proceed
    /// TERMINAL STATE - requires re-initiation to restart
    /// </summary>
    Rejected = 5
}
