namespace IntelliFin.ClientManagement.Infrastructure.Configuration;

/// <summary>
/// Configuration options for RabbitMQ messaging
/// Used for event-driven notifications and inter-service communication
/// </summary>
public class RabbitMqOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// RabbitMQ host address
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// RabbitMQ port
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// RabbitMQ username
    /// </summary>
    public string Username { get; set; } = "guest";

    /// <summary>
    /// RabbitMQ password
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Virtual host
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Whether RabbitMQ integration is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Exchange name for client events
    /// </summary>
    public string ExchangeName { get; set; } = "client.events";

    /// <summary>
    /// Queue name for KYC notifications
    /// </summary>
    public string QueueName { get; set; } = "client-management.kyc-notifications";

    /// <summary>
    /// Dead letter queue name
    /// </summary>
    public string DeadLetterQueueName { get; set; } = "client-management.kyc-notifications.dlq";

    /// <summary>
    /// Number of retry attempts before sending to DLQ
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Initial retry interval in seconds
    /// </summary>
    public int InitialRetryIntervalSeconds { get; set; } = 1;

    /// <summary>
    /// Retry interval increment factor
    /// </summary>
    public double RetryIntervalIncrement { get; set; } = 2.0;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Prefetch count (messages to prefetch)
    /// </summary>
    public ushort PrefetchCount { get; set; } = 16;

    /// <summary>
    /// Connection string (overrides individual properties if set)
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets the connection URI
    /// </summary>
    public string GetConnectionUri()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
            return ConnectionString;

        return $"amqp://{Username}:{Password}@{Host}:{Port}{VirtualHost}";
    }

    /// <summary>
    /// Validates configuration
    /// </summary>
    public bool IsValid()
    {
        if (!Enabled)
            return true; // Valid if disabled

        if (string.IsNullOrWhiteSpace(Host))
            return false;

        if (Port <= 0 || Port > 65535)
            return false;

        return true;
    }
}
