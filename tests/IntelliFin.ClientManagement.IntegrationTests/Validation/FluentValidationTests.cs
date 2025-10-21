using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text;
using System.Text.Json;

namespace IntelliFin.ClientManagement.IntegrationTests.Validation;

/// <summary>
/// Integration tests for FluentValidation
/// </summary>
public class FluentValidationTests : IAsyncLifetime
{
    private IHost? _host;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    [Fact]
    public async Task InvalidRequest_Should_Return400WithValidationErrors()
    {
        // Arrange
        _host = await CreateTestHostWithValidation();
        var client = _host.GetTestClient();

        var invalidRequest = new TestRequest
        {
            Name = "", // Required field
            Age = 150 // Out of range
        };

        var content = new StringContent(
            JsonSerializer.Serialize(invalidRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/test", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Name");
        responseContent.Should().Contain("Age");
    }

    [Fact]
    public async Task ValidRequest_Should_Return200OK()
    {
        // Arrange
        _host = await CreateTestHostWithValidation();
        var client = _host.GetTestClient();

        var validRequest = new TestRequest
        {
            Name = "John Doe",
            Age = 30
        };

        var content = new StringContent(
            JsonSerializer.Serialize(validRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/test", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<IHost> CreateTestHostWithValidation()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddValidatorsFromAssemblyContaining<TestRequestValidator>();
                    services.AddRouting();
                });

                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPost("/api/test", async (
                            [FromBody] TestRequest request,
                            [FromServices] IValidator<TestRequest> validator) =>
                        {
                            var validationResult = await validator.ValidateAsync(request);
                            if (!validationResult.IsValid)
                            {
                                return Results.ValidationProblem(
                                    validationResult.ToDictionary());
                            }
                            return Results.Ok(new { message = "Valid request" });
                        });
                    });
                });
            });

        return await hostBuilder.StartAsync();
    }

    public class TestRequest
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    public class TestRequestValidator : AbstractValidator<TestRequest>
    {
        public TestRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters");

            RuleFor(x => x.Age)
                .GreaterThan(0).WithMessage("Age must be greater than 0")
                .LessThanOrEqualTo(120).WithMessage("Age must be 120 or less");
        }
    }
}
