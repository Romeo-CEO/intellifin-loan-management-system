using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IntelliFin.Shared.Infrastructure.Messaging;

public static class MassTransitConfig
{
    public static IServiceCollection AddIntelliFinMassTransit(this IServiceCollection services, IHostEnvironment env)
    {
        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host("localhost", 35672, "/", h =>
                {
                    // Default guest/guest; change when hardening
                    h.Username("guest");
                    h.Password("guest");
                });

                // Enable durable endpoints by default
                cfg.AutoStart = true;
                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddHealthChecks().AddCheck<BusHealthCheck>("masstransit");

        return services;
    }
}

