namespace IntelliFin.ClientManagement.Domain.Entities;

/// <summary>
/// Represents a client's communication consent preferences for different channels
/// Tracks consent lifecycle for regulatory compliance (GDPR/POPIA)
/// </summary>
public class CommunicationConsent
{
    /// <summary>
    /// Unique identifier for the consent record
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to Client entity
    /// </summary>
    public Guid ClientId { get; set; }

    // ========== Consent Classification ==========

    /// <summary>
    /// Type of consent (Marketing, Operational, Regulatory)
    /// - Marketing: Promotional messages, product offers, newsletters
    /// - Operational: KYC status, loan updates, payment reminders
    /// - Regulatory: Legal notices, compliance notifications (cannot be disabled)
    /// </summary>
    public string ConsentType { get; set; } = string.Empty;

    // ========== Channel Preferences ==========

    /// <summary>
    /// Customer consent for SMS/text message notifications
    /// </summary>
    public bool SmsEnabled { get; set; }

    /// <summary>
    /// Customer consent for email notifications
    /// </summary>
    public bool EmailEnabled { get; set; }

    /// <summary>
    /// Customer consent for in-app notifications (mobile/web app)
    /// </summary>
    public bool InAppEnabled { get; set; }

    /// <summary>
    /// Customer consent for phone call notifications
    /// </summary>
    public bool CallEnabled { get; set; }

    // ========== Consent Lifecycle ==========

    /// <summary>
    /// Timestamp when consent was granted
    /// </summary>
    public DateTime ConsentGivenAt { get; set; }

    /// <summary>
    /// Who granted the consent (ClientSelf, Officer, System)
    /// - ClientSelf: Customer granted consent themselves
    /// - Officer: Loan officer granted on behalf of customer
    /// - System: System-generated (e.g., regulatory consent)
    /// </summary>
    public string ConsentGivenBy { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when consent was revoked (null if still active)
    /// </summary>
    public DateTime? ConsentRevokedAt { get; set; }

    /// <summary>
    /// Reason for revoking consent (optional)
    /// </summary>
    public string? RevocationReason { get; set; }

    // ========== Audit ==========

    /// <summary>
    /// Correlation ID for distributed tracing
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Timestamp when record was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when record was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    // ========== Navigation Properties ==========

    /// <summary>
    /// Navigation property to parent Client
    /// </summary>
    public Client Client { get; set; } = null!;

    // ========== Helper Methods ==========

    /// <summary>
    /// Checks if consent is currently active (granted and not revoked)
    /// </summary>
    public bool IsActive => ConsentRevokedAt == null;

    /// <summary>
    /// Checks if a specific channel is enabled
    /// </summary>
    public bool IsChannelEnabled(string channel)
    {
        return channel.ToLower() switch
        {
            "sms" => SmsEnabled,
            "email" => EmailEnabled,
            "inapp" => InAppEnabled,
            "call" => CallEnabled,
            _ => false
        };
    }
}

/// <summary>
/// Consent type enumeration values
/// </summary>
public static class ConsentType
{
    /// <summary>
    /// Marketing consent for promotional messages, product offers, newsletters
    /// </summary>
    public const string Marketing = "Marketing";

    /// <summary>
    /// Operational consent for KYC status updates, loan application status, payment reminders
    /// </summary>
    public const string Operational = "Operational";

    /// <summary>
    /// Regulatory consent for compliance notifications, legal notices
    /// This type cannot be disabled by customers (legally required)
    /// </summary>
    public const string Regulatory = "Regulatory";
}

/// <summary>
/// Consent grantor enumeration values
/// </summary>
public static class ConsentGrantor
{
    /// <summary>
    /// Customer granted consent themselves
    /// </summary>
    public const string ClientSelf = "ClientSelf";

    /// <summary>
    /// Loan officer granted consent on behalf of customer
    /// </summary>
    public const string Officer = "Officer";

    /// <summary>
    /// System-generated consent (e.g., regulatory consent)
    /// </summary>
    public const string System = "System";
}

/// <summary>
/// Communication channel enumeration values
/// </summary>
public static class CommunicationChannel
{
    public const string SMS = "SMS";
    public const string Email = "Email";
    public const string InApp = "InApp";
    public const string Call = "Call";
}
