using IntelliFin.ClientManagement.Domain.Enums;

namespace IntelliFin.ClientManagement.Domain.Exceptions;

/// <summary>
/// Exception thrown when an invalid KYC state transition is attempted
/// </summary>
public class InvalidKycStateTransitionException : Exception
{
    /// <summary>
    /// Current state
    /// </summary>
    public KycState FromState { get; }

    /// <summary>
    /// Attempted target state
    /// </summary>
    public KycState ToState { get; }

    /// <summary>
    /// Reason why transition is invalid
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Creates a new InvalidKycStateTransitionException
    /// </summary>
    public InvalidKycStateTransitionException(KycState from, KycState to, string reason)
        : base($"Invalid KYC state transition from {from} to {to}: {reason}")
    {
        FromState = from;
        ToState = to;
        Reason = reason;
    }
}
