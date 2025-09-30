# Hybrid Provider Migration Strategy

**Story ID:** SMS-2.4
**Epic:** Epic 2 - SMS Provider Migration to Africa's Talking
**Status:** Draft
**Story Points:** 8
**Priority:** High

## User Story
**As a** system administrator
**I want** a gradual migration strategy from legacy SMS providers to Africa's Talking
**So that** we can migrate safely without service disruption and with immediate rollback capability

## Background
Implement a hybrid provider system that allows gradual migration from existing Zambian carrier integrations (Airtel, MTN, Zamtel) to Africa's Talking while maintaining fallback capabilities and enabling percentage-based traffic routing.

## Acceptance Criteria

### ✅ Hybrid Provider Implementation
- [ ] Create `HybridSmsProvider` that manages multiple provider instances
- [ ] Support percentage-based traffic routing between providers
- [ ] Implement automatic fallback to legacy providers on failures
- [ ] Enable configuration-driven rollout percentages
- [ ] Support immediate rollback via configuration changes

### ✅ Traffic Routing Logic
- [ ] Implement deterministic routing based on phone number hash
- [ ] Support gradual percentage increase (10%, 25%, 50%, 75%, 100%)
- [ ] Ensure consistent routing for same phone numbers during migration
- [ ] Support VIP customer routing preferences
- [ ] Enable override routing for testing specific scenarios

### ✅ Fallback Mechanisms
- [ ] Automatic fallback to legacy providers on Africa's Talking failures
- [ ] Circuit breaker pattern for provider health monitoring
- [ ] Configurable fallback triggers (timeouts, error rates, specific errors)
- [ ] Graceful degradation with provider unavailability
- [ ] Manual override capabilities for emergency situations

### ✅ Migration Monitoring
- [ ] Real-time metrics for provider success rates
- [ ] Delivery time comparison between providers
- [ ] Cost analysis for migration phases
- [ ] Error rate monitoring and alerting
- [ ] Migration progress dashboards

### ✅ Feature Flag Integration
- [ ] Integration with feature flag system for instant rollback
- [ ] Environment-specific migration configurations
- [ ] A/B testing capabilities for provider comparison
- [ ] Gradual rollout controls with safety limits
- [ ] Emergency stop functionality

## Technical Implementation

### Hybrid Provider Class
```csharp
public class HybridSmsProvider : ISmsProvider
{
    private readonly AfricasTalkingSmsProvider _africasTalking;
    private readonly LegacySmsProvider _legacy;
    private readonly IConfiguration _configuration;
    private readonly IMetrics _metrics;
    private readonly ILogger<HybridSmsProvider> _logger;

    public string ProviderName => "Hybrid";

    public async Task<SmsResult> SendAsync(SmsRequest request, CancellationToken cancellationToken = default)
    {
        var routingDecision = DetermineProvider(request);

        try
        {
            if (routingDecision.UseAfricasTalking)
            {
                var result = await _africasTalking.SendAsync(request, cancellationToken);

                // Track metrics
                _metrics.RecordProviderUsage("AfricasTalking", result.Success);

                // Fallback on failure if enabled
                if (!result.Success && routingDecision.EnableFallback)
                {
                    _logger.LogWarning("Africa's Talking failed, falling back to legacy provider");
                    return await _legacy.SendAsync(request, cancellationToken);
                }

                return result;
            }
            else
            {
                var result = await _legacy.SendAsync(request, cancellationToken);
                _metrics.RecordProviderUsage("Legacy", result.Success);
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provider failure, attempting fallback");

            // Try fallback provider
            if (routingDecision.UseAfricasTalking && routingDecision.EnableFallback)
            {
                return await _legacy.SendAsync(request, cancellationToken);
            }
            else if (!routingDecision.UseAfricasTalking)
            {
                // Legacy failed, try Africa's Talking if available
                return await _africasTalking.SendAsync(request, cancellationToken);
            }

            throw;
        }
    }

    private RoutingDecision DetermineProvider(SmsRequest request)
    {
        var config = _configuration.GetSection("SMS:Migration").Get<MigrationConfig>();

        // Check for VIP override
        if (IsVipCustomer(request.To))
        {
            return new RoutingDecision
            {
                UseAfricasTalking = config.VipCustomersUseAfricasTalking,
                EnableFallback = true,
                Reason = "VIP customer routing"
            };
        }

        // Check feature flags
        if (!config.EnableAfricasTalkingMigration)
        {
            return new RoutingDecision
            {
                UseAfricasTalking = false,
                EnableFallback = false,
                Reason = "Migration disabled"
            };
        }

        // Deterministic percentage-based routing
        var hash = Math.Abs(request.To.GetHashCode());
        var routingPercentage = hash % 100;
        var useAfricasTalking = routingPercentage < config.AfricasTalkingPercentage;

        return new RoutingDecision
        {
            UseAfricasTalking = useAfricasTalking,
            EnableFallback = config.EnableFallback,
            Reason = $"Percentage routing: {routingPercentage} < {config.AfricasTalkingPercentage}"
        };
    }
}
```

### Migration Configuration
```csharp
public class MigrationConfig
{
    public const string SectionName = "SMS:Migration";

    public bool EnableAfricasTalkingMigration { get; set; } = true;
    public int AfricasTalkingPercentage { get; set; } = 10;
    public bool EnableFallback { get; set; } = true;
    public bool VipCustomersUseAfricasTalking { get; set; } = false;
    public List<string> VipPhoneNumbers { get; set; } = new();
    public int CircuitBreakerThreshold { get; set; } = 5;
    public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan ProviderTimeoutSeconds { get; set; } = TimeSpan.FromSeconds(30);
}

public class RoutingDecision
{
    public bool UseAfricasTalking { get; set; }
    public bool EnableFallback { get; set; }
    public string Reason { get; set; } = string.Empty;
}
```

### Circuit Breaker Implementation
```csharp
public class ProviderCircuitBreaker
{
    private readonly CircuitBreakerPolicy _circuitBreaker;
    private readonly ILogger<ProviderCircuitBreaker> _logger;

    public ProviderCircuitBreaker(MigrationConfig config, ILogger<ProviderCircuitBreaker> logger)
    {
        _logger = logger;
        _circuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: config.CircuitBreakerThreshold,
                durationOfBreak: config.CircuitBreakerDuration,
                onBreak: OnCircuitBreakerOpened,
                onReset: OnCircuitBreakerClosed);
    }

    public async Task<SmsResult> ExecuteAsync(Func<Task<SmsResult>> operation)
    {
        return await _circuitBreaker.ExecuteAsync(operation);
    }

    private void OnCircuitBreakerOpened(Exception exception, TimeSpan duration)
    {
        _logger.LogWarning("Circuit breaker opened for {Duration}ms due to: {Exception}",
            duration.TotalMilliseconds, exception.Message);
    }

    private void OnCircuitBreakerClosed()
    {
        _logger.LogInformation("Circuit breaker closed - provider restored");
    }
}
```

## Files to Create/Modify

### New Files
- `apps/IntelliFin.Communications/Providers/HybridSmsProvider.cs`
- `apps/IntelliFin.Communications/Configuration/MigrationConfig.cs`
- `apps/IntelliFin.Communications/Services/ProviderCircuitBreaker.cs`
- `apps/IntelliFin.Communications/Services/MigrationMetricsService.cs`
- `apps/IntelliFin.Communications/Models/RoutingModels.cs`

### Modified Files
- `apps/IntelliFin.Communications/Services/SmsProviderFactory.cs` - Add hybrid provider support
- `apps/IntelliFin.Communications/Extensions/ServiceCollectionExtensions.cs` - Register services
- `appsettings.json` - Add migration configuration

## Migration Configuration Example
```json
{
  "SMS": {
    "Provider": "Hybrid",
    "Migration": {
      "EnableAfricasTalkingMigration": true,
      "AfricasTalkingPercentage": 10,
      "EnableFallback": true,
      "VipCustomersUseAfricasTalking": false,
      "VipPhoneNumbers": ["+260971234567", "+260966789012"],
      "CircuitBreakerThreshold": 5,
      "CircuitBreakerDurationMinutes": 5,
      "ProviderTimeoutSeconds": 30
    }
  },
  "FeatureFlags": {
    "EnableAfricasTalkingMigration": true,
    "EnableSmsProviderFallback": true,
    "AfricasTalkingEmergencyStop": false
  }
}
```

## Migration Phases

### Phase 1: Setup (10% Traffic)
```json
{
  "AfricasTalkingPercentage": 10,
  "EnableFallback": true,
  "VipCustomersUseAfricasTalking": false
}
```

### Phase 2: Gradual Increase (25% Traffic)
```json
{
  "AfricasTalkingPercentage": 25,
  "EnableFallback": true,
  "VipCustomersUseAfricasTalking": true
}
```

### Phase 3: Majority Migration (75% Traffic)
```json
{
  "AfricasTalkingPercentage": 75,
  "EnableFallback": true,
  "VipCustomersUseAfricasTalking": true
}
```

### Phase 4: Complete Migration (100% Traffic)
```json
{
  "AfricasTalkingPercentage": 100,
  "EnableFallback": true,
  "VipCustomersUseAfricasTalking": true
}
```

## Testing Requirements

### Unit Tests
- [ ] Routing logic for various percentage configurations
- [ ] Fallback mechanism triggering and provider switching
- [ ] Circuit breaker behavior under failure conditions
- [ ] VIP customer routing preferences
- [ ] Configuration validation and edge cases

### Integration Tests
- [ ] End-to-end hybrid provider message delivery
- [ ] Provider switching during simulated failures
- [ ] Performance comparison between providers
- [ ] Metrics collection accuracy
- [ ] Feature flag integration and rollback scenarios

### Load Testing
- [ ] Hybrid provider performance under mixed traffic
- [ ] Failover response times during provider issues
- [ ] Circuit breaker effectiveness under load
- [ ] Memory and resource usage during provider switching

## Dependencies
- Africa's Talking provider implementation (Story SMS-2.2)
- Provider abstraction layer (Story SMS-2.1)
- Legacy SMS provider implementations
- Configuration management and feature flag systems
- Metrics and monitoring infrastructure

## Success Criteria
- Gradual migration completes without service disruption
- Automatic fallback prevents SMS delivery failures
- Provider switching adds <100ms additional latency
- Real-time monitoring provides migration visibility
- Rollback capability works within 5 minutes

## Risk Mitigation
- **Provider Failures**: Multi-layer fallback with circuit breakers
- **Configuration Errors**: Configuration validation and safe defaults
- **Performance Impact**: Benchmarking and optimization at each phase
- **Data Loss**: Comprehensive logging and audit trails
- **Rollback Issues**: Automated rollback triggers and manual overrides

## Definition of Done
- [ ] All acceptance criteria implemented and tested
- [ ] Unit test coverage ≥90% for hybrid provider logic
- [ ] Integration tests validate all migration phases
- [ ] Load testing confirms performance requirements
- [ ] Migration runbooks created for operations team
- [ ] Monitoring dashboards deployed for migration tracking
- [ ] Emergency rollback procedures tested and documented
- [ ] Code review completed and approved