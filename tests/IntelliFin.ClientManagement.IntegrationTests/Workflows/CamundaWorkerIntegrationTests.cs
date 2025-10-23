using FluentAssertions;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Infrastructure.Configuration;
using IntelliFin.ClientManagement.Infrastructure.HealthChecks;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Workflows.CamundaWorkers;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;
using Xunit;

namespace IntelliFin.ClientManagement.IntegrationTests.Workflows;

/// <summary>
/// Integration tests for Camunda worker infrastructure
/// Tests worker registration, health checks, and background service lifecycle
/// </summary>
public class CamundaWorkerIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private ClientManagementDbContext? _context;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseSqlServer(_msSqlContainer.GetConnectionString())
            .Options;

        _context = new ClientManagementDbContext(options);
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    [Fact]
    public void HealthCheckWorker_ShouldImplementICamundaJobHandler()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<HealthCheckWorker>();

        // Act
        var worker = new HealthCheckWorker(logger, _context!);

        // Assert
        worker.Should().BeAssignableTo<ICamundaJobHandler>();
        worker.GetTopicName().Should().Be("client.health.check");
        worker.GetJobType().Should().Be("io.intellifin.health.check");
    }

    [Fact]
    public void CamundaWorkerRegistration_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var registration = new CamundaWorkerRegistration
        {
            TopicName = "client.test.topic",
            JobType = "io.intellifin.test.job",
            HandlerType = typeof(HealthCheckWorker),
            MaxJobsToActivate = 20,
            TimeoutSeconds = 45
        };

        // Assert
        registration.TopicName.Should().Be("client.test.topic");
        registration.JobType.Should().Be("io.intellifin.test.job");
        registration.HandlerType.Should().Be(typeof(HealthCheckWorker));
        registration.MaxJobsToActivate.Should().Be(20);
        registration.TimeoutSeconds.Should().Be(45);
    }

    [Fact]
    public void CamundaOptions_ShouldBindFromConfiguration()
    {
        // Arrange
        var configData = new Dictionary<string, string>
        {
            ["Camunda:GatewayAddress"] = "http://test-gateway:26500",
            ["Camunda:WorkerName"] = "TestWorker",
            ["Camunda:MaxJobsToActivate"] = "50",
            ["Camunda:PollingIntervalSeconds"] = "10",
            ["Camunda:RequestTimeoutSeconds"] = "60",
            ["Camunda:Enabled"] = "true",
            ["Camunda:MaxRetries"] = "5"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act
        var options = configuration
            .GetSection(CamundaOptions.SectionName)
            .Get<CamundaOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.GatewayAddress.Should().Be("http://test-gateway:26500");
        options.WorkerName.Should().Be("TestWorker");
        options.MaxJobsToActivate.Should().Be(50);
        options.PollingIntervalSeconds.Should().Be(10);
        options.RequestTimeoutSeconds.Should().Be(60);
        options.Enabled.Should().BeTrue();
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void CamundaOptions_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var options = new CamundaOptions();

        // Assert
        options.GatewayAddress.Should().Be("http://localhost:26500");
        options.WorkerName.Should().Be("IntelliFin.ClientManagement");
        options.MaxJobsToActivate.Should().Be(32);
        options.PollingIntervalSeconds.Should().Be(5);
        options.RequestTimeoutSeconds.Should().Be(30);
        options.Enabled.Should().BeTrue();
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public async Task CamundaHealthCheck_WhenCamundaDisabled_ShouldReturnDegraded()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<CamundaHealthCheck>();

        var options = Options.Create(new CamundaOptions
        {
            Enabled = false
        });

        var healthCheck = new CamundaHealthCheck(logger, options);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("disabled");
    }

    [Fact]
    public async Task CamundaHealthCheck_WhenGatewayUnavailable_ShouldReturnUnhealthy()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<CamundaHealthCheck>();

        var options = Options.Create(new CamundaOptions
        {
            Enabled = true,
            GatewayAddress = "http://nonexistent-gateway:26500"
        });

        var healthCheck = new CamundaHealthCheck(logger, options);

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        // Assert - Should timeout or fail to connect
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("Failed to connect");
    }

    [Fact]
    public async Task HealthCheckWorker_ShouldCheckDatabaseConnectivity()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<HealthCheckWorker>();
        var worker = new HealthCheckWorker(logger, _context!);

        // Create a test client to ensure database works
        var testClient = new Client
        {
            Id = Guid.NewGuid(),
            Nrc = "999999/99/9",
            FirstName = "Test",
            LastName = "Worker",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = "M",
            MaritalStatus = "Single",
            PrimaryPhone = "+260977999999",
            PhysicalAddress = "123 Worker St",
            City = "Lusaka",
            Province = "Lusaka",
            BranchId = Guid.NewGuid(),
            CreatedBy = "test-worker",
            UpdatedBy = "test-worker"
        };

        _context!.Clients.Add(testClient);
        await _context.SaveChangesAsync();

        // Act - Test database connectivity
        var canConnect = await _context.Database.CanConnectAsync();

        // Assert
        canConnect.Should().BeTrue("database should be accessible");

        // Verify worker properties
        worker.GetTopicName().Should().Be("client.health.check");
        worker.GetJobType().Should().Be("io.intellifin.health.check");
    }

    [Fact]
    public void CamundaWorkerHostedService_ShouldAcceptWorkerRegistrations()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<CamundaWorkerHostedService>();

        var options = Options.Create(new CamundaOptions
        {
            Enabled = false // Disabled to prevent actual connection attempt
        });

        var workerRegistrations = new List<CamundaWorkerRegistration>
        {
            new CamundaWorkerRegistration
            {
                TopicName = "client.test.topic",
                JobType = "io.intellifin.test.job",
                HandlerType = typeof(HealthCheckWorker),
                MaxJobsToActivate = 10,
                TimeoutSeconds = 30
            }
        };

        var serviceProvider = new ServiceCollection()
            .AddScoped<ICamundaJobHandler, HealthCheckWorker>()
            .AddDbContext<ClientManagementDbContext>(opts =>
                opts.UseSqlServer(_msSqlContainer.GetConnectionString()))
            .BuildServiceProvider();

        // Act
        var hostedService = new CamundaWorkerHostedService(
            logger,
            serviceProvider,
            options,
            workerRegistrations);

        // Assert
        hostedService.Should().NotBeNull();
    }

    [Fact]
    public async Task CamundaWorkerHostedService_WhenDisabled_ShouldNotStartWorkers()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<CamundaWorkerHostedService>();

        var options = Options.Create(new CamundaOptions
        {
            Enabled = false // Disabled
        });

        var workerRegistrations = new List<CamundaWorkerRegistration>();

        var serviceProvider = new ServiceCollection()
            .BuildServiceProvider();

        var hostedService = new CamundaWorkerHostedService(
            logger,
            serviceProvider,
            options,
            workerRegistrations);

        // Act - Start and immediately stop
        using var cts = new CancellationTokenSource();
        var executeTask = hostedService.StartAsync(cts.Token);
        await Task.Delay(100); // Give it a moment
        cts.Cancel();
        await hostedService.StopAsync(CancellationToken.None);

        // Assert - Should complete without error
        executeTask.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void WorkerRegistration_ShouldSupportMultipleWorkers()
    {
        // Arrange & Act
        var registrations = new List<CamundaWorkerRegistration>
        {
            new CamundaWorkerRegistration
            {
                TopicName = "client.kyc.verify-documents",
                JobType = "io.intellifin.kyc.verify",
                HandlerType = typeof(HealthCheckWorker),
                MaxJobsToActivate = 32,
                TimeoutSeconds = 30
            },
            new CamundaWorkerRegistration
            {
                TopicName = "client.kyc.aml-screening",
                JobType = "io.intellifin.kyc.aml",
                HandlerType = typeof(HealthCheckWorker),
                MaxJobsToActivate = 16,
                TimeoutSeconds = 60
            },
            new CamundaWorkerRegistration
            {
                TopicName = "client.health.check",
                JobType = "io.intellifin.health.check",
                HandlerType = typeof(HealthCheckWorker),
                MaxJobsToActivate = 10,
                TimeoutSeconds = 30
            }
        };

        // Assert
        registrations.Should().HaveCount(3);
        registrations.Select(r => r.TopicName).Should().OnlyHaveUniqueItems();
        registrations.Select(r => r.JobType).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void CamundaOptions_ShouldSupportTopicList()
    {
        // Arrange
        var configData = new Dictionary<string, string>
        {
            ["Camunda:Topics:0"] = "client.health.check",
            ["Camunda:Topics:1"] = "client.kyc.verify-documents",
            ["Camunda:Topics:2"] = "client.kyc.aml-screening"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act
        var options = configuration
            .GetSection(CamundaOptions.SectionName)
            .Get<CamundaOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Topics.Should().HaveCount(3);
        options.Topics.Should().Contain("client.health.check");
        options.Topics.Should().Contain("client.kyc.verify-documents");
        options.Topics.Should().Contain("client.kyc.aml-screening");
    }

    [Fact]
    public async Task ServiceIntegration_ShouldRegisterAllComponents()
    {
        // Arrange
        var configData = new Dictionary<string, string>
        {
            ["Camunda:Enabled"] = "false", // Disabled to prevent actual connection
            ["Camunda:GatewayAddress"] = "http://localhost:26500",
            ["Camunda:WorkerName"] = "TestWorker"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddDbContext<ClientManagementDbContext>(opts =>
            opts.UseSqlServer(_msSqlContainer.GetConnectionString()));

        // Act - Register Camunda services
        services.Configure<CamundaOptions>(
            configuration.GetSection(CamundaOptions.SectionName));

        services.AddScoped<ICamundaJobHandler, HealthCheckWorker>();

        var workerRegistrations = new List<CamundaWorkerRegistration>
        {
            new CamundaWorkerRegistration
            {
                TopicName = "client.health.check",
                JobType = "io.intellifin.health.check",
                HandlerType = typeof(HealthCheckWorker),
                MaxJobsToActivate = 10,
                TimeoutSeconds = 30
            }
        };

        services.AddSingleton<IEnumerable<CamundaWorkerRegistration>>(workerRegistrations);
        services.AddHostedService<CamundaWorkerHostedService>();
        services.AddHealthChecks()
            .AddCheck<CamundaHealthCheck>("camunda");

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var worker = serviceProvider.GetService<ICamundaJobHandler>();
        worker.Should().NotBeNull();

        var registrations = serviceProvider.GetService<IEnumerable<CamundaWorkerRegistration>>();
        registrations.Should().NotBeNull();
        registrations.Should().HaveCount(1);

        var healthCheck = serviceProvider.GetService<CamundaHealthCheck>();
        healthCheck.Should().NotBeNull();

        // Verify health check works
        var healthCheckResult = await healthCheck!.CheckHealthAsync(new HealthCheckContext());
        healthCheckResult.Status.Should().Be(HealthStatus.Degraded); // Disabled
    }
}
