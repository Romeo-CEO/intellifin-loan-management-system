using IntelliFin.ClientManagement.Consumers;
using IntelliFin.ClientManagement.Infrastructure.Configuration;
using MassTransit;
using Microsoft.Extensions.Options;

namespace IntelliFin.ClientManagement.Extensions;

/// <summary>
/// Extension methods for MassTransit configuration
/// </summary>
public static class MassTransitExtensions
{
    /// <summary>
    /// Adds MassTransit with RabbitMQ for event-driven notifications
    /// </summary>
    public static IServiceCollection AddMassTransitMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<RabbitMqOptions>(
            configuration.GetSection(RabbitMqOptions.SectionName));

        var rabbitMqOptions = configuration
            .GetSection(RabbitMqOptions.SectionName)
            .Get<RabbitMqOptions>();

        if (rabbitMqOptions?.Enabled != true)
        {
            // RabbitMQ disabled - skip MassTransit registration
            return services;
        }

        // Add RabbitMQ health check
        services.AddHealthChecks()
            .AddCheck<Infrastructure.HealthChecks.RabbitMqHealthCheck>(
                "rabbitmq",
                tags: new[] { "ready", "messaging" });

        services.AddMassTransit(x =>
        {
            // Register consumers
            x.AddConsumer<KycCompletedEventConsumer>();
            x.AddConsumer<KycRejectedEventConsumer>();
            x.AddConsumer<EddEscalatedEventConsumer>();
            x.AddConsumer<EddApprovedEventConsumer>();
            x.AddConsumer<EddRejectedEventConsumer>();
            x.AddConsumer<DeadLetterQueueConsumer>();

            // Configure RabbitMQ
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitMqOptions.Host, rabbitMqOptions.Port, rabbitMqOptions.VirtualHost, h =>
                {
                    h.Username(rabbitMqOptions.Username);
                    h.Password(rabbitMqOptions.Password);
                });

                // Configure receive endpoint for KYC notifications
                cfg.ReceiveEndpoint(rabbitMqOptions.QueueName, e =>
                {
                    e.PrefetchCount = rabbitMqOptions.PrefetchCount;
                    e.Durable = true; // Ensure queue survives broker restart

                    // Bind to exchange with routing key patterns
                    e.Bind(rabbitMqOptions.ExchangeName, s =>
                    {
                        s.RoutingKey = "client.kyc.*";
                        s.ExchangeType = "topic";
                    });

                    e.Bind(rabbitMqOptions.ExchangeName, s =>
                    {
                        s.RoutingKey = "client.edd.*";
                        s.ExchangeType = "topic";
                    });

                    // Configure consumers
                    e.ConfigureConsumer<KycCompletedEventConsumer>(context);
                    e.ConfigureConsumer<KycRejectedEventConsumer>(context);
                    e.ConfigureConsumer<EddEscalatedEventConsumer>(context);
                    e.ConfigureConsumer<EddApprovedEventConsumer>(context);
                    e.ConfigureConsumer<EddRejectedEventConsumer>(context);

                    // Retry policy: exponential backoff
                    e.UseMessageRetry(r => r.Exponential(
                        rabbitMqOptions.RetryCount,
                        TimeSpan.FromSeconds(rabbitMqOptions.InitialRetryIntervalSeconds),
                        TimeSpan.FromSeconds(rabbitMqOptions.InitialRetryIntervalSeconds * 10),
                        TimeSpan.FromSeconds(rabbitMqOptions.RetryIntervalIncrement)));

                    // Configure dead letter queue
                    e.ConfigureDeadLetterQueueDeadLetterTransport();
                    e.ConfigureDeadLetterQueueErrorTransport();
                });

                // Configure DLQ endpoint
                cfg.ReceiveEndpoint(rabbitMqOptions.DeadLetterQueueName, e =>
                {
                    e.PrefetchCount = 5; // Lower prefetch for DLQ
                    e.ConfigureConsumer<DeadLetterQueueConsumer>(context);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    /// <summary>
    /// Publishes a domain event to RabbitMQ
    /// Helper method for workers and services
    /// </summary>
    public static async Task PublishDomainEventAsync<TEvent>(
        this IPublishEndpoint publishEndpoint,
        TEvent domainEvent,
        string routingKey,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        await publishEndpoint.Publish(domainEvent, ctx =>
        {
            // Set routing key for topic exchange
            ctx.SetRoutingKey(routingKey);

            // Set correlation ID
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                ctx.CorrelationId = Guid.Parse(correlationId);
            }

            // Set message headers
            ctx.Headers.Set("EventType", typeof(TEvent).Name);
            ctx.Headers.Set("PublishedAt", DateTime.UtcNow);
        }, cancellationToken);
    }
}
