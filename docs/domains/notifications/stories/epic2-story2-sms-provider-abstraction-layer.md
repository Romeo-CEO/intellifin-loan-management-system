# Story 2.2: SMS Provider Abstraction Layer

**Epic:** Epic 2 - SMS Provider Migration to Africa's Talking
**Story ID:** COMM-022
**Status:** Draft
**Priority:** High
**Effort:** 5 Story Points

## User Story
**As a** software developer
**I want** a flexible SMS provider abstraction layer
**So that** we can easily switch between providers and implement fallback mechanisms without code changes

## Business Value
- **Provider Flexibility**: Easy switching between SMS providers based on requirements
- **Risk Mitigation**: Fallback capabilities reduce dependency on single provider
- **Future-Proofing**: Simple addition of new providers without architectural changes
- **Operational Resilience**: Automated failover during provider outages
- **Cost Optimization**: Dynamic provider selection based on cost and performance

## Acceptance Criteria

### Primary Functionality
- [ ] **Provider Interface**: Define common interface for all SMS providers
  - Standardized methods for sending SMS
  - Consistent error handling and result structures
  - Support for both single and bulk SMS operations
- [ ] **Provider Factory**: Dynamic provider instantiation and configuration
  - Runtime provider selection based on configuration
  - Dependency injection integration
  - Provider health check capabilities
- [ ] **Provider Registry**: Manage multiple provider implementations
  - Register and discover available providers
  - Provider capability metadata
  - Health status tracking
- [ ] **Fallback Mechanism**: Automatic provider switching on failures
  - Primary/secondary provider configuration
  - Failure detection and automatic fallover
  - Circuit breaker pattern implementation

### Configuration Support
- [ ] **Multi-Provider Config**: Support configuration for multiple providers
  - Provider-specific settings
  - Priority and fallback order
  - Feature flags for provider enablement
- [ ] **Runtime Switching**: Change providers without application restart
  - Configuration hot reload
  - Graceful provider transitions
  - Zero-downtime provider switching

### Monitoring and Observability
- [ ] **Provider Metrics**: Track performance per provider
  - Success/failure rates
  - Response times
  - Cost tracking per provider
- [ ] **Health Checks**: Monitor provider availability
  - Periodic health validation
  - Provider status reporting
  - Automatic unhealthy provider exclusion

## Technical Implementation

### Components to Implement

#### 1. SMS Provider Interface
```csharp
// File: apps/IntelliFin.Communications/Interfaces/ISmsProvider.cs
public interface ISmsProvider
{
    string ProviderName { get; }
    Task<SmsResult> SendAsync(SmsRequest request, CancellationToken cancellationToken = default);
    Task<SmsResult> SendBulkAsync(List<SmsRequest> requests, CancellationToken cancellationToken = default);
    Task<SmsStatusResult> GetStatusAsync(string messageId, CancellationToken cancellationToken = default);
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
    Task<ProviderCapabilities> GetCapabilitiesAsync();
}

public class ProviderCapabilities
{
    public bool SupportsBulkSms { get; set; }
    public bool SupportsDeliveryReports { get; set; }
    public bool SupportsScheduledSms { get; set; }
    public int MaxMessageLength { get; set; }
    public int MaxBulkSize { get; set; }
    public List<string> SupportedCountries { get; set; } = new();
}
```

#### 2. Provider Factory
```csharp
// File: apps/IntelliFin.Communications/Services/SmsProviderFactory.cs
public interface ISmsProviderFactory
{
    ISmsProvider CreateProvider(string providerName);
    ISmsProvider CreatePrimaryProvider();
    List<ISmsProvider> GetAvailableProviders();
    Task<ISmsProvider> GetHealthyProviderAsync();
}

public class SmsProviderFactory : ISmsProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SmsProviderConfiguration _configuration;
    private readonly ILogger<SmsProviderFactory> _logger;

    public ISmsProvider CreateProvider(string providerName)
    {
        return providerName switch
        {
            "AfricasTalking" => _serviceProvider.GetRequiredService<AfricasTalkingSmsProvider>(),
            "Airtel" => _serviceProvider.GetRequiredService<AirtelSmsProvider>(),
            "MTN" => _serviceProvider.GetRequiredService<MtnSmsProvider>(),
            "Zamtel" => _serviceProvider.GetRequiredService<ZamtelSmsProvider>(),
            _ => throw new InvalidOperationException($"Unknown SMS provider: {providerName}")
        };
    }

    public async Task<ISmsProvider> GetHealthyProviderAsync()
    {
        foreach (var providerName in _configuration.ProviderPriority)
        {
            if (!_configuration.IsProviderEnabled(providerName))
                continue;

            try
            {
                var provider = CreateProvider(providerName);
                if (await provider.IsHealthyAsync())
                {
                    return provider;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {Provider} health check failed", providerName);
            }
        }

        throw new InvalidOperationException("No healthy SMS providers available");
    }
}
```

#### 3. Resilient SMS Service
```csharp
// File: apps/IntelliFin.Communications/Services/ResilientSmsService.cs
public class ResilientSmsService : ISmsService
{
    private readonly ISmsProviderFactory _providerFactory;
    private readonly SmsProviderConfiguration _configuration;
    private readonly ICircuitBreakerService _circuitBreaker;
    private readonly ILogger<ResilientSmsService> _logger;

    public async Task<SmsResult> SendAsync(SmsRequest request)
    {
        var providers = GetOrderedProviders();
        Exception lastException = null;

        foreach (var providerName in providers)
        {
            if (!_circuitBreaker.IsProviderAvailable(providerName))
                continue;

            try
            {
                var provider = _providerFactory.CreateProvider(providerName);
                var result = await provider.SendAsync(request);

                if (result.Success)
                {
                    _circuitBreaker.RecordSuccess(providerName);
                    return result;
                }
                else
                {
                    _circuitBreaker.RecordFailure(providerName);
                    _logger.LogWarning("SMS send failed via {Provider}: {Error}",
                        providerName, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                _circuitBreaker.RecordFailure(providerName);
                _logger.LogError(ex, "SMS provider {Provider} threw exception", providerName);
            }
        }

        return new SmsResult
        {
            Success = false,
            ErrorMessage = $"All SMS providers failed. Last error: {lastException?.Message}"
        };
    }

    private List<string> GetOrderedProviders()
    {
        return _configuration.ProviderPriority
            .Where(p => _configuration.IsProviderEnabled(p))
            .ToList();
    }
}
```

#### 4. Circuit Breaker Service
```csharp
// File: apps/IntelliFin.Communications/Services/CircuitBreakerService.cs
public interface ICircuitBreakerService
{
    bool IsProviderAvailable(string providerName);
    void RecordSuccess(string providerName);
    void RecordFailure(string providerName);
    Task<Dictionary<string, ProviderHealth>> GetProviderHealthAsync();
}

public class CircuitBreakerService : ICircuitBreakerService
{
    private readonly ConcurrentDictionary<string, ProviderCircuitBreaker> _circuitBreakers;
    private readonly SmsProviderConfiguration _configuration;

    public bool IsProviderAvailable(string providerName)
    {
        var breaker = _circuitBreakers.GetOrAdd(providerName,
            name => new ProviderCircuitBreaker(_configuration.CircuitBreakerOptions));

        return breaker.State != CircuitBreakerState.Open;
    }

    public void RecordSuccess(string providerName)
    {
        if (_circuitBreakers.TryGetValue(providerName, out var breaker))
        {
            breaker.RecordSuccess();
        }
    }

    public void RecordFailure(string providerName)
    {
        var breaker = _circuitBreakers.GetOrAdd(providerName,
            name => new ProviderCircuitBreaker(_configuration.CircuitBreakerOptions));

        breaker.RecordFailure();
    }
}

public class ProviderCircuitBreaker
{
    public CircuitBreakerState State { get; private set; }
    public DateTime LastFailureTime { get; private set; }
    public int ConsecutiveFailures { get; private set; }

    // Circuit breaker logic implementation
}
```

#### 5. Provider Configuration
```csharp
// File: apps/IntelliFin.Communications/Configuration/SmsProviderConfiguration.cs
public class SmsProviderConfiguration
{
    public const string SectionName = "SmsProviders";

    public List<string> ProviderPriority { get; set; } = new();
    public Dictionary<string, bool> EnabledProviders { get; set; } = new();
    public CircuitBreakerOptions CircuitBreakerOptions { get; set; } = new();
    public int HealthCheckIntervalSeconds { get; set; } = 60;
    public bool EnableAutoFailover { get; set; } = true;

    public bool IsProviderEnabled(string providerName)
    {
        return EnabledProviders.GetValueOrDefault(providerName, false);
    }
}

public class CircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int HalfOpenRetryCount { get; set; } = 3;
}
```

### Configuration Structure
```json
{
  "SmsProviders": {
    "ProviderPriority": ["AfricasTalking", "Airtel", "MTN", "Zamtel"],
    "EnabledProviders": {
      "AfricasTalking": true,
      "Airtel": true,
      "MTN": true,
      "Zamtel": false
    },
    "CircuitBreakerOptions": {
      "FailureThreshold": 5,
      "OpenTimeoutMinutes": 5,
      "HalfOpenRetryCount": 3
    },
    "HealthCheckIntervalSeconds": 60,
    "EnableAutoFailover": true
  }
}
```

### Dependency Injection Setup
```csharp
// File: apps/IntelliFin.Communications/Extensions/ServiceCollectionExtensions.cs
public static IServiceCollection AddSmsProviders(this IServiceCollection services, IConfiguration configuration)
{
    // Configure provider settings
    services.Configure<SmsProviderConfiguration>(
        configuration.GetSection(SmsProviderConfiguration.SectionName));

    // Register provider implementations
    services.AddScoped<AfricasTalkingSmsProvider>();
    services.AddScoped<AirtelSmsProvider>();
    services.AddScoped<MtnSmsProvider>();
    services.AddScoped<ZamtelSmsProvider>();

    // Register abstraction services
    services.AddScoped<ISmsProviderFactory, SmsProviderFactory>();
    services.AddSingleton<ICircuitBreakerService, CircuitBreakerService>();
    services.AddScoped<ISmsService, ResilientSmsService>();

    // Health checks for providers
    services.AddHealthChecks()
        .AddCheck<SmsProviderHealthCheck>("sms-providers");

    return services;
}
```

## Dependencies
- **Story 2.1**: Africa's Talking provider implementation
- **Epic 1**: Base SMS infrastructure and models
- **Existing Providers**: Current Airtel, MTN, Zamtel implementations

## Risks and Mitigation

### Technical Risks
- **Provider Interface Changes**: Version provider interfaces and maintain backward compatibility
- **Circuit Breaker Complexity**: Thoroughly test circuit breaker state transitions
- **Configuration Complexity**: Validate configuration on startup
- **Dependency Injection**: Ensure proper scoping and lifecycle management

### Operational Risks
- **Fallback Delays**: Monitor and optimize failover times
- **Provider Selection Logic**: Test provider selection under various failure scenarios
- **Configuration Errors**: Implement configuration validation and health checks

## Testing Strategy

### Unit Tests
- [ ] Provider factory logic
- [ ] Circuit breaker state management
- [ ] Provider selection algorithms
- [ ] Configuration validation
- [ ] Health check implementations

### Integration Tests
- [ ] Multi-provider scenarios
- [ ] Failover mechanisms
- [ ] Circuit breaker behavior
- [ ] Configuration hot reload
- [ ] Provider health monitoring

### End-to-End Tests
- [ ] Complete failover scenarios
- [ ] Provider recovery testing
- [ ] Configuration change impacts
- [ ] Performance under load

## Success Metrics
- **Failover Time**: <5 seconds for automatic provider switching
- **Provider Health Detection**: Health check accuracy >99%
- **Configuration Reload**: <2 seconds for configuration updates
- **Circuit Breaker Response**: <1 second for state changes
- **Provider Selection**: Consistent selection based on priority and health

## Definition of Done
- [ ] All acceptance criteria implemented and tested
- [ ] Provider interface documented and validated
- [ ] Circuit breaker logic tested under failure scenarios
- [ ] Configuration validation implemented
- [ ] Health checks operational for all providers
- [ ] Dependency injection properly configured
- [ ] Performance testing completed
- [ ] Code review completed and approved
- [ ] Integration tests passing
- [ ] Documentation updated

## Related Stories
- **Prerequisite**: Story 2.1 (Africa's Talking provider)
- **Successor**: Story 2.4 (Migration strategy)
- **Related**: Story 2.6 (Configuration management)

## Technical Notes

### Provider Health Check Implementation
```csharp
public class SmsProviderHealthCheck : IHealthCheck
{
    private readonly ISmsProviderFactory _providerFactory;
    private readonly SmsProviderConfiguration _configuration;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var unhealthyProviders = new List<string>();

        foreach (var providerName in _configuration.ProviderPriority)
        {
            if (!_configuration.IsProviderEnabled(providerName))
                continue;

            try
            {
                var provider = _providerFactory.CreateProvider(providerName);
                if (!await provider.IsHealthyAsync(cancellationToken))
                {
                    unhealthyProviders.Add(providerName);
                }
            }
            catch (Exception)
            {
                unhealthyProviders.Add(providerName);
            }
        }

        if (unhealthyProviders.Any())
        {
            return HealthCheckResult.Degraded(
                $"Unhealthy providers: {string.Join(", ", unhealthyProviders)}");
        }

        return HealthCheckResult.Healthy("All enabled SMS providers are healthy");
    }
}
```

This abstraction layer provides the foundation for flexible provider management and reliable SMS delivery across multiple providers.