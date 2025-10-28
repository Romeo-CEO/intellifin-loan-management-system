using IntelliFin.TreasuryService.Consumers;
using MassTransit;
using Microsoft.Extensions.Configuration;

namespace IntelliFin.TreasuryService.Extensions;

/// <summary>
/// Extension methods for MassTransit configuration in TreasuryService
/// </summary>
public static class MassTransitExtensions
{
    /// <summary>
    /// Adds MassTransit with RabbitMQ for loan disbursement event processing
    /// </summary>
    public static IServiceCollection AddTreasuryMassTransit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            // Register Treasury consumers
            x.AddConsumer<LoanDisbursementConsumer>();

            // Configure RabbitMQ
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host("localhost", 35672, "/", h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });

                // Configure receive endpoint for loan disbursement events
                cfg.ReceiveEndpoint("treasury-disbursement-queue", e =>
                {
                    e.PrefetchCount = 5; // Process up to 5 messages concurrently
                    e.Durable = true; // Ensure queue survives broker restart

                    // Configure consumer
                    e.ConfigureConsumer<LoanDisbursementConsumer>(context);

                    // Basic retry configuration
                    e.UseMessageRetry(r => r.Immediate(3));
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
