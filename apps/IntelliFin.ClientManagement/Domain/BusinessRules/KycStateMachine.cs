using IntelliFin.ClientManagement.Domain.Enums;

namespace IntelliFin.ClientManagement.Domain.BusinessRules;

/// <summary>
/// KYC state machine for validating state transitions and business rules
/// Ensures only valid workflow progressions are allowed
/// </summary>
public static class KycStateMachine
{
    /// <summary>
    /// Valid state transitions matrix
    /// Defines which states can transition to which other states
    /// </summary>
    private static readonly Dictionary<KycState, HashSet<KycState>> ValidTransitions = new()
    {
        [KycState.Pending] = new HashSet<KycState>
        {
            KycState.InProgress // Documents being collected
        },
        [KycState.InProgress] = new HashSet<KycState>
        {
            KycState.Completed,     // Successful verification
            KycState.EDD_Required,  // High risk detected
            KycState.Rejected       // Verification failed
        },
        [KycState.EDD_Required] = new HashSet<KycState>
        {
            KycState.Completed,  // EDD approved by compliance + CEO
            KycState.Rejected    // EDD rejected
        },
        // Terminal states - no transitions allowed
        [KycState.Completed] = new HashSet<KycState>(),
        [KycState.Rejected] = new HashSet<KycState>()
    };

    /// <summary>
    /// Validates if a state transition is allowed
    /// </summary>
    /// <param name="from">Current state</param>
    /// <param name="to">Target state</param>
    /// <returns>True if transition is valid, false otherwise</returns>
    public static bool IsValidTransition(KycState from, KycState to)
    {
        if (!ValidTransitions.ContainsKey(from))
            return false;

        return ValidTransitions[from].Contains(to);
    }

    /// <summary>
    /// Gets allowed next states for a given current state
    /// </summary>
    /// <param name="currentState">Current KYC state</param>
    /// <returns>Set of allowed next states</returns>
    public static IEnumerable<KycState> GetAllowedNextStates(KycState currentState)
    {
        return ValidTransitions.ContainsKey(currentState)
            ? ValidTransitions[currentState]
            : Enumerable.Empty<KycState>();
    }

    /// <summary>
    /// Checks if a state is terminal (no further transitions allowed)
    /// </summary>
    /// <param name="state">State to check</param>
    /// <returns>True if state is terminal</returns>
    public static bool IsTerminalState(KycState state)
    {
        return state == KycState.Completed || state == KycState.Rejected;
    }

    /// <summary>
    /// Gets validation message for invalid transition
    /// </summary>
    /// <param name="from">Current state</param>
    /// <param name="to">Target state</param>
    /// <returns>Reason why transition is invalid</returns>
    public static string GetInvalidTransitionReason(KycState from, KycState to)
    {
        if (IsTerminalState(from))
            return $"{from} is a terminal state. KYC must be re-initiated to change state.";

        if (!ValidTransitions.ContainsKey(from))
            return $"Unknown state: {from}";

        var allowedStates = string.Join(", ", GetAllowedNextStates(from));
        return $"Cannot transition from {from} to {to}. Allowed transitions: {allowedStates}";
    }
}
