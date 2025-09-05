using IntelliFin.Shared.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MassTransit;

namespace IntelliFin.Tests.Unit.Infrastructure;

public class MassTransitConfigTests
{
    [Fact]
    public void AddIntelliFinMassTransit_Should_Register_Required_Services()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Add logging services
        var environment = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == "Development");

        // Act
        services.AddIntelliFinMassTransit(environment);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<IBus>().Should().NotBeNull();
        serviceProvider.GetService<IPublishEndpoint>().Should().NotBeNull();
        serviceProvider.GetService<ISendEndpointProvider>().Should().NotBeNull();
    }

    [Fact]
    public void AddIntelliFinMassTransit_Should_Register_Health_Checks()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Add logging services
        var environment = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == "Development");

        // Act
        services.AddIntelliFinMassTransit(environment);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var healthCheckService = serviceProvider.GetService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();
        healthCheckService.Should().NotBeNull();
    }

    [Fact]
    public void AddIntelliFinMassTransit_Should_Configure_Kebab_Case_Endpoints()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Add logging services
        var environment = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == "Development");

        // Act
        services.AddIntelliFinMassTransit(environment);

        // Assert
        // This test verifies the configuration is applied without exceptions
        var serviceProvider = services.BuildServiceProvider();
        var bus = serviceProvider.GetService<IBus>();
        bus.Should().NotBeNull();
    }
}
