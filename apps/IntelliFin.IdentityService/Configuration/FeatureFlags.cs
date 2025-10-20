namespace IntelliFin.IdentityService.Configuration;

/// <summary>
/// Feature flags for Identity Service
/// </summary>
public class FeatureFlags
{
    public const string SectionName = "FeatureFlags";

    /// <summary>
    /// Enable automatic user provisioning to Keycloak
    /// </summary>
    public bool EnableUserProvisioning { get; set; } = false;

    /// <summary>
    /// Enable dual-mode JWT authentication (custom + Keycloak)
    /// </summary>
    public bool EnableDualMode { get; set; } = false;

    /// <summary>
    /// Enable OIDC authentication flows
    /// </summary>
    public bool EnableOidc { get; set; } = false;

    /// <summary>
    /// Enable service-to-service authentication
    /// </summary>
    public bool EnableServiceAuth { get; set; } = false;

    /// <summary>
    /// Enable Separation of Duties (SoD) validation
    /// </summary>
    public bool EnableSoDValidation { get; set; } = false;
}

/// <summary>
/// Configuration options for user provisioning to Keycloak
/// </summary>
public class ProvisioningOptions
{
    public const string SectionName = "Provisioning";

    /// <summary>
    /// Allow automatic creation of roles in Keycloak if they don't exist
    /// </summary>
    public bool AllowRoleAutoCreate { get; set; } = false;

    /// <summary>
    /// Batch size for bulk provisioning operations
    /// </summary>
    public int BulkProvisioningBatchSize { get; set; } = 50;

    /// <summary>
    /// Maximum concurrent provisioning operations
    /// </summary>
    public int MaxConcurrentProvisionings { get; set; } = 10;

    /// <summary>
    /// Queue capacity for provisioning commands
    /// </summary>
    public int QueueCapacity { get; set; } = 100;

    /// <summary>
    /// Enable automatic retry of dead-letter queue items
    /// </summary>
    public bool AutoRetryDeadLetter { get; set; } = false;

    /// <summary>
    /// Retry interval for dead-letter queue (in minutes)
    /// </summary>
    public int DeadLetterRetryIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum age of commands in queue before alerting (in minutes)
    /// </summary>
    public int MaxQueueAgeMinutes { get; set; } = 30;

    /// <summary>
    /// Sync user attributes on every provisioning call (slower but ensures consistency)
    /// </summary>
    public bool AlwaysSyncAttributes { get; set; } = true;

    /// <summary>
    /// Sync user roles on every provisioning call
    /// </summary>
    public bool AlwaysSyncRoles { get; set; } = true;
}
