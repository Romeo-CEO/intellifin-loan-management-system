using IntelliFin.Communications.Consumers;
using IntelliFin.Shared.Infrastructure.Messaging.Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.RabbitMq;

namespace IntelliFin.Tests.Integration.Messaging;

public class MassTransitIntegrationTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public async Task InitializeAsync()
    {
        await _rabbitMqContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _rabbitMqContainer.DisposeAsync();
    }

    [Fact]
    public async Task Should_Publish_And_Consume_LoanApplicationCreated_Message()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<LoanApplicationCreatedConsumer>();
            
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(_rabbitMqContainer.GetConnectionString());
                cfg.ConfigureEndpoints(context);
            });
        });

        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();

        try
        {
            // Act
            var message = new LoanApplicationCreated(
                Guid.NewGuid(),
                Guid.NewGuid(),
                50000m,
                12,
                "PAYROLL",
                DateTime.UtcNow
            );

            await harness.Bus.Publish(message);

            // Assert
            (await harness.Published.Any<LoanApplicationCreated>()).Should().BeTrue();
            (await harness.Consumed.Any<LoanApplicationCreated>()).Should().BeTrue();

            var consumerHarness = harness.GetConsumerHarness<LoanApplicationCreatedConsumer>();
            (await consumerHarness.Consumed.Any<LoanApplicationCreated>()).Should().BeTrue();
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Should_Handle_Message_Publishing_Without_Consumer()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        services.AddMassTransitTestHarness(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(_rabbitMqContainer.GetConnectionString());
                cfg.ConfigureEndpoints(context);
            });
        });

        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();

        try
        {
            // Act
            var message = new LoanApplicationCreated(
                Guid.NewGuid(),
                Guid.NewGuid(),
                25000m,
                6,
                "SALARY",
                DateTime.UtcNow
            );

            await harness.Bus.Publish(message);

            // Assert
            (await harness.Published.Any<LoanApplicationCreated>()).Should().BeTrue();
        }
        finally
        {
            await harness.Stop();
        }
    }
}
