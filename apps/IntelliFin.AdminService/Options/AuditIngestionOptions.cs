namespace IntelliFin.AdminService.Options;

public sealed class AuditIngestionOptions
{
    public const string SectionName = "AuditIngestion";

    public int BatchSize { get; set; } = 1000;
    public int MaxBufferSize { get; set; } = 100_000;
    public int FlushIntervalSeconds { get; set; } = 5;
    public bool EnableRabbitMqConsumer { get; set; } = true;
}

public sealed class AuditRabbitMqOptions
{
    public const string SectionName = "AuditRabbitMq";

    public string HostName { get; set; } = "rabbitmq";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
    public string Exchange { get; set; } = "audit.events";
    public string QueueName { get; set; } = "admin-service.audit.events";
    public string DeadLetterQueue { get; set; } = "admin-service.audit.events.dlq";
    public bool Enabled { get; set; } = true;
}
