using IntelliFin.Shared.Infrastructure.Messaging;
using MassTransit;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IntelliFin.Tests.Unit.Infrastructure;

public class BusHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_Should_Return_Healthy_When_Bus_Available()
    {
        // Arrange
        var mockBus = Mock.Of<IBus>();
        var healthCheck = new BusHealthCheck(mockBus);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("MassTransit bus is available");
    }

    [Fact]
    public async Task CheckHealthAsync_Should_Return_Unhealthy_When_Bus_Null()
    {
        // Arrange
        var healthCheck = new BusHealthCheck(null!);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("MassTransit bus is not available");
    }

    [Fact]
    public async Task CheckHealthAsync_Should_Complete_Synchronously()
    {
        // Arrange
        var mockBus = Mock.Of<IBus>();
        var healthCheck = new BusHealthCheck(mockBus);
        var context = new HealthCheckContext();

        // Act
        var task = healthCheck.CheckHealthAsync(context);

        // Assert
        task.IsCompleted.Should().BeTrue();
        var result = await task;
        result.Should().NotBeNull();
    }
}
