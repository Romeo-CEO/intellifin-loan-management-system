namespace IntelliFin.LoanOriginationService.Events;

/// <summary>
/// Event published when a client's KYC status is approved.
/// Consumed by LoanOriginationService to allow loan application processing.
/// </summary>
public record ClientKycApprovedEvent
{
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string NationalId { get; init; } = string.Empty;
    public DateTime ApprovedAt { get; init; }
    public string ApprovedBy { get; init; } = string.Empty;
    public string KycLevel { get; init; } = "Standard"; // Basic, Standard, Enhanced
    public DateTime? ExpiryDate { get; init; }
    public Guid CorrelationId { get; init; }
}

/// <summary>
/// Event published when a client's KYC status is revoked or expired.
/// Consumed by LoanOriginationService to pause/decline active loan applications.
/// </summary>
public record ClientKycRevokedEvent
{
    public Guid ClientId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime RevokedAt { get; init; }
    public string RevokedBy { get; init; } = string.Empty;
    public Guid CorrelationId { get; init; }
}

/// <summary>
/// Event published when a client's profile is updated.
/// Consumed by LoanOriginationService to update cached client data.
/// </summary>
public record ClientProfileUpdatedEvent
{
    public Guid ClientId { get; init; }
    public Dictionary<string, object> UpdatedFields { get; init; } = new();
    public DateTime UpdatedAt { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
    public Guid CorrelationId { get; init; }
}

/// <summary>
/// Event published when a client's AML check is completed.
/// </summary>
public record ClientAmlCheckCompletedEvent
{
    public Guid ClientId { get; init; }
    public string Status { get; init; } = string.Empty; // Cleared, Flagged, Pending
    public string? RiskLevel { get; init; } // Low, Medium, High
    public DateTime CheckedAt { get; init; }
    public string CheckedBy { get; init; } = string.Empty;
    public Guid CorrelationId { get; init; }
}
