# Story 2.4: Gradual Migration Strategy with Feature Flags

**Epic:** Epic 2 - SMS Provider Migration to Africa's Talking
**Story ID:** COMM-024
**Status:** Draft
**Priority:** High
**Effort:** 7 Story Points

## User Story
**As a** system administrator
**I want** a safe, gradual migration strategy from existing SMS providers to Africa's Talking
**So that** we can migrate with zero downtime and immediate rollback capabilities if issues arise

## Business Value
- **Zero-Downtime Migration**: Seamless transition without service interruption
- **Risk Mitigation**: Immediate rollback capability if issues are detected
- **Gradual Rollout**: Controlled migration with increasing confidence levels
- **Operational Safety**: Fallback mechanisms protect against provider failures
- **Business Continuity**: Maintain SMS functionality throughout migration process
- **Data-Driven Migration**: Monitor metrics to validate migration success

## Acceptance Criteria

### Primary Functionality
- [ ] **Feature Flag System**: Flexible SMS provider routing control
  - Runtime configuration changes without deployment
  - Percentage-based traffic routing (10%, 25%, 50%, 75%, 100%)
  - User-based routing for testing specific scenarios
  - Branch-based routing for regional rollouts
- [ ] **Hybrid Provider Service**: Manage multiple providers simultaneously
  - Primary/fallback provider configuration
  - Dynamic provider selection based on health and performance
  - Seamless switching between providers
- [ ] **Migration Phases**: Structured, safe migration approach
  - Phase 1: Parallel setup and testing
  - Phase 2: Gradual traffic migration (1%, 5%, 25%, 50%, 75%, 100%)
  - Phase 3: Legacy provider deprecation
  - Phase 4: Complete migration with cleanup
- [ ] **Automatic Failover**: Intelligent fallback mechanisms
  - Real-time provider health monitoring
  - Automatic fallback on failure detection
  - Circuit breaker pattern implementation

### Safety and Monitoring
- [ ] **Migration Metrics**: Comprehensive monitoring during migration
  - Success rate comparison between providers
  - Response time and performance metrics
  - Error rate tracking and alerting
  - Cost comparison analysis
- [ ] **Rollback Capabilities**: Quick reversion mechanisms
  - Instant traffic routing back to legacy providers
  - Configuration backup and restore
  - Emergency rollback procedures
- [ ] **Validation Testing**: Continuous migration validation
  - Automated health checks during migration
  - Delivery confirmation validation
  - Performance threshold monitoring

### Configuration Management
- [ ] **Dynamic Configuration**: Runtime configuration updates
  - Feature flag configuration via admin interface
  - Real-time configuration reloading
  - Configuration validation and error handling
- [ ] **Migration States**: Clear migration phase management
  - Migration state tracking and reporting
  - Phase progression controls
  - Migration rollback state management

## Technical Implementation

### Components to Implement

#### 1. Feature Flag Service
```csharp
// File: apps/IntelliFin.Communications/Services/SmsFeatureFlagService.cs
public interface ISmsFeatureFlagService
{
    Task<bool> IsAfricasTalkingEnabledAsync(string context = null);
    Task<int> GetAfricasTalkingTrafficPercentageAsync();
    Task<string> GetPrimaryProviderAsync(SmsRoutingContext context);
    Task<MigrationPhase> GetCurrentMigrationPhaseAsync();
    Task UpdateMigrationPhaseAsync(MigrationPhase phase);
    Task<bool> ShouldUseAfricasTalkingAsync(SmsRoutingContext context);
}

public class SmsFeatureFlagService : ISmsFeatureFlagService
{
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _cache;
    private readonly IMigrationStateRepository _migrationStateRepository;
    private readonly ILogger<SmsFeatureFlagService> _logger;

    public async Task<bool> ShouldUseAfricasTalkingAsync(SmsRoutingContext context)
    {
        // Check if Africa's Talking is enabled globally
        if (!await IsAfricasTalkingEnabledAsync())
            return false;

        // Get traffic percentage for gradual rollout
        var trafficPercentage = await GetAfricasTalkingTrafficPercentageAsync();

        // Determine routing based on context and percentage
        return ShouldRouteToAfricasTalking(context, trafficPercentage);
    }

    private bool ShouldRouteToAfricasTalking(SmsRoutingContext context, int trafficPercentage)
    {
        // Priority routing rules:

        // 1. User-specific override (for testing)
        if (context.UserOverrides?.ContainsKey(context.UserId) == true)
        {
            return context.UserOverrides[context.UserId] == "AfricasTalking";
        }

        // 2. Branch-specific override (for regional rollout)
        if (context.BranchOverrides?.ContainsKey(context.BranchId) == true)
        {
            return context.BranchOverrides[context.BranchId] == "AfricasTalking";
        }

        // 3. Percentage-based routing using consistent hashing
        var hash = CalculateConsistentHash(context.RecipientNumber);
        return hash % 100 < trafficPercentage;
    }

    private int CalculateConsistentHash(string input)
    {
        // Use deterministic hash to ensure same number always routes the same way
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Math.Abs(BitConverter.ToInt32(hash, 0));
    }

    public async Task<MigrationPhase> GetCurrentMigrationPhaseAsync()
    {
        var migrationState = await _migrationStateRepository.GetCurrentStateAsync();
        return migrationState?.Phase ?? MigrationPhase.NotStarted;
    }
}

public class SmsRoutingContext
{
    public string RecipientNumber { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public Dictionary<string, string>? UserOverrides { get; set; }
    public Dictionary<int, string>? BranchOverrides { get; set; }
}

public enum MigrationPhase
{
    NotStarted = 0,
    ParallelSetup = 1,
    GradualMigration = 2,
    LegacyDeprecation = 3,
    CompleteMigration = 4,
    Rollback = 5
}
```

#### 2. Hybrid SMS Provider
```csharp
// File: apps/IntelliFin.Communications/Services/HybridSmsProvider.cs
public class HybridSmsProvider : ISmsProvider
{
    private readonly ISmsProviderFactory _providerFactory;
    private readonly ISmsFeatureFlagService _featureFlagService;
    private readonly ICircuitBreakerService _circuitBreakerService;
    private readonly IMigrationMetricsCollector _metricsCollector;
    private readonly ILogger<HybridSmsProvider> _logger;

    public string ProviderName => "Hybrid";

    public async Task<SmsResult> SendAsync(SmsRequest request, CancellationToken cancellationToken = default)
    {
        var context = CreateRoutingContext(request);
        var shouldUseAfricasTalking = await _featureFlagService.ShouldUseAfricasTalkingAsync(context);

        var primaryProvider = shouldUseAfricasTalking ? "AfricasTalking" : "Legacy";
        var fallbackProvider = shouldUseAfricasTalking ? "Legacy" : "AfricasTalking";

        // Try primary provider
        var result = await TryProviderAsync(primaryProvider, request, cancellationToken);
        if (result.Success)
        {
            await _metricsCollector.RecordSuccessAsync(primaryProvider, context);
            return result;
        }

        // Log primary failure and try fallback
        _logger.LogWarning("Primary provider {Provider} failed: {Error}. Trying fallback.",
            primaryProvider, result.ErrorMessage);

        await _metricsCollector.RecordFailureAsync(primaryProvider, context, result.ErrorMessage);

        // Try fallback provider if circuit breaker allows
        if (_circuitBreakerService.IsProviderAvailable(fallbackProvider))
        {
            var fallbackResult = await TryProviderAsync(fallbackProvider, request, cancellationToken);
            if (fallbackResult.Success)
            {
                await _metricsCollector.RecordFallbackSuccessAsync(fallbackProvider, context);
                return fallbackResult;
            }

            await _metricsCollector.RecordFailureAsync(fallbackProvider, context, fallbackResult.ErrorMessage);
        }

        // Both providers failed
        return new SmsResult
        {
            Success = false,
            ErrorMessage = $"Both providers failed. Primary: {result.ErrorMessage}"
        };
    }

    private async Task<SmsResult> TryProviderAsync(string providerName, SmsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var provider = _providerFactory.CreateProvider(providerName);
            return await provider.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred with provider {Provider}", providerName);
            return new SmsResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private SmsRoutingContext CreateRoutingContext(SmsRequest request)
    {
        return new SmsRoutingContext
        {
            RecipientNumber = request.To,
            UserId = request.Metadata?.GetValueOrDefault("UserId", string.Empty) ?? string.Empty,
            BranchId = int.Parse(request.Metadata?.GetValueOrDefault("BranchId", "0") ?? "0"),
            MessageType = request.Metadata?.GetValueOrDefault("MessageType", "Standard") ?? "Standard"
        };
    }
}
```

#### 3. Migration State Management
```csharp
// File: apps/IntelliFin.Communications/Services/MigrationStateService.cs
public interface IMigrationStateService
{
    Task<MigrationState> GetCurrentStateAsync();
    Task<MigrationState> AdvanceToNextPhaseAsync();
    Task<MigrationState> RollbackToPreviousPhaseAsync();
    Task<MigrationState> SetTrafficPercentageAsync(int percentage);
    Task<bool> ValidatePhaseTransitionAsync(MigrationPhase targetPhase);
    Task<MigrationHealthReport> GetMigrationHealthAsync();
}

public class MigrationStateService : IMigrationStateService
{
    private readonly IMigrationStateRepository _repository;
    private readonly IMigrationMetricsCollector _metricsCollector;
    private readonly INotificationService _notificationService;
    private readonly ILogger<MigrationStateService> _logger;

    public async Task<MigrationState> AdvanceToNextPhaseAsync()
    {
        var currentState = await GetCurrentStateAsync();
        var nextPhase = GetNextPhase(currentState.Phase);

        // Validate that it's safe to advance
        if (!await ValidatePhaseTransitionAsync(nextPhase))
        {
            throw new InvalidOperationException($"Cannot advance to {nextPhase}. Health checks failed.");
        }

        var newState = new MigrationState
        {
            Phase = nextPhase,
            TrafficPercentage = GetPhaseTrafficPercentage(nextPhase),
            StartedAt = DateTime.UtcNow,
            InitiatedBy = GetCurrentUser(),
            PreviousPhase = currentState.Phase,
            IsRollbackAvailable = CanRollback(nextPhase)
        };

        await _repository.SaveStateAsync(newState);

        // Notify stakeholders of phase change
        await NotifyPhaseChangeAsync(currentState.Phase, nextPhase);

        _logger.LogInformation("Migration advanced from {FromPhase} to {ToPhase} with {Percentage}% traffic",
            currentState.Phase, nextPhase, newState.TrafficPercentage);

        return newState;
    }

    public async Task<bool> ValidatePhaseTransitionAsync(MigrationPhase targetPhase)
    {
        var healthReport = await GetMigrationHealthAsync();

        // Define health thresholds for each phase
        var requirements = GetPhaseRequirements(targetPhase);

        return healthReport.AfricasTalkingSuccessRate >= requirements.MinSuccessRate &&
               healthReport.AfricasTalkingResponseTime <= requirements.MaxResponseTime &&
               healthReport.ErrorRate <= requirements.MaxErrorRate &&
               healthReport.DeliveryRate >= requirements.MinDeliveryRate;
    }

    private PhaseRequirements GetPhaseRequirements(MigrationPhase phase)
    {
        return phase switch
        {
            MigrationPhase.GradualMigration => new PhaseRequirements
            {
                MinSuccessRate = 95.0m,
                MaxResponseTime = TimeSpan.FromSeconds(3),
                MaxErrorRate = 2.0m,
                MinDeliveryRate = 98.0m
            },
            MigrationPhase.LegacyDeprecation => new PhaseRequirements
            {
                MinSuccessRate = 98.0m,
                MaxResponseTime = TimeSpan.FromSeconds(2),
                MaxErrorRate = 1.0m,
                MinDeliveryRate = 99.0m
            },
            MigrationPhase.CompleteMigration => new PhaseRequirements
            {
                MinSuccessRate = 99.0m,
                MaxResponseTime = TimeSpan.FromSeconds(2),
                MaxErrorRate = 0.5m,
                MinDeliveryRate = 99.5m
            },
            _ => new PhaseRequirements()
        };
    }

    private int GetPhaseTrafficPercentage(MigrationPhase phase)
    {
        return phase switch
        {
            MigrationPhase.NotStarted => 0,
            MigrationPhase.ParallelSetup => 1,
            MigrationPhase.GradualMigration => 25, // Start with 25%, can be adjusted
            MigrationPhase.LegacyDeprecation => 75,
            MigrationPhase.CompleteMigration => 100,
            MigrationPhase.Rollback => 0,
            _ => 0
        };
    }
}

public class MigrationState
{
    public Guid Id { get; set; }
    public MigrationPhase Phase { get; set; }
    public int TrafficPercentage { get; set; }
    public DateTime StartedAt { get; set; }
    public string InitiatedBy { get; set; } = string.Empty;
    public MigrationPhase PreviousPhase { get; set; }
    public bool IsRollbackAvailable { get; set; }
    public string? Notes { get; set; }
}

public class PhaseRequirements
{
    public decimal MinSuccessRate { get; set; }
    public TimeSpan MaxResponseTime { get; set; }
    public decimal MaxErrorRate { get; set; }
    public decimal MinDeliveryRate { get; set; }
}
```

#### 4. Migration Metrics Collector
```csharp
// File: apps/IntelliFin.Communications/Services/MigrationMetricsCollector.cs
public interface IMigrationMetricsCollector
{
    Task RecordSuccessAsync(string provider, SmsRoutingContext context);
    Task RecordFailureAsync(string provider, SmsRoutingContext context, string errorMessage);
    Task RecordFallbackSuccessAsync(string provider, SmsRoutingContext context);
    Task<MigrationHealthReport> GetHealthReportAsync(TimeSpan period);
    Task<ProviderComparisonReport> GetProviderComparisonAsync(TimeSpan period);
}

public class MigrationMetricsCollector : IMigrationMetricsCollector
{
    private readonly IMetricsRepository _metricsRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MigrationMetricsCollector> _logger;

    public async Task RecordSuccessAsync(string provider, SmsRoutingContext context)
    {
        var metric = new SmsMetric
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            RecipientNumber = context.RecipientNumber,
            BranchId = context.BranchId,
            MessageType = context.MessageType,
            Status = "Success",
            Timestamp = DateTime.UtcNow,
            ResponseTime = null // Will be set by calling service
        };

        await _metricsRepository.SaveMetricAsync(metric);

        // Update real-time counters
        UpdateRealTimeCounters(provider, true);
    }

    public async Task<MigrationHealthReport> GetHealthReportAsync(TimeSpan period)
    {
        var startTime = DateTime.UtcNow.Subtract(period);
        var metrics = await _metricsRepository.GetMetricsAsync(startTime, DateTime.UtcNow);

        var africasTalkingMetrics = metrics.Where(m => m.Provider == "AfricasTalking").ToList();
        var legacyMetrics = metrics.Where(m => m.Provider != "AfricasTalking").ToList();

        return new MigrationHealthReport
        {
            ReportPeriod = period,
            TotalMessages = metrics.Count,
            AfricasTalkingSuccessRate = CalculateSuccessRate(africasTalkingMetrics),
            LegacySuccessRate = CalculateSuccessRate(legacyMetrics),
            AfricasTalkingResponseTime = CalculateAverageResponseTime(africasTalkingMetrics),
            LegacyResponseTime = CalculateAverageResponseTime(legacyMetrics),
            ErrorRate = CalculateErrorRate(metrics),
            DeliveryRate = CalculateDeliveryRate(metrics),
            FallbackRate = CalculateFallbackRate(metrics)
        };
    }

    private void UpdateRealTimeCounters(string provider, bool success)
    {
        var key = $"sms_metrics_{provider}_{DateTime.UtcNow:yyyy-MM-dd-HH}";
        var counters = _cache.GetOrCreate(key, _ => new HourlyCounters());

        if (success)
            Interlocked.Increment(ref counters.SuccessCount);
        else
            Interlocked.Increment(ref counters.FailureCount);

        _cache.Set(key, counters, TimeSpan.FromHours(2));
    }
}

public class MigrationHealthReport
{
    public TimeSpan ReportPeriod { get; set; }
    public int TotalMessages { get; set; }
    public decimal AfricasTalkingSuccessRate { get; set; }
    public decimal LegacySuccessRate { get; set; }
    public TimeSpan AfricasTalkingResponseTime { get; set; }
    public TimeSpan LegacyResponseTime { get; set; }
    public decimal ErrorRate { get; set; }
    public decimal DeliveryRate { get; set; }
    public decimal FallbackRate { get; set; }
    public List<string> HealthIssues { get; set; } = new();
    public bool IsHealthy => !HealthIssues.Any();
}
```

#### 5. Migration Controller
```csharp
// File: apps/IntelliFin.Communications/Controllers/MigrationController.cs
[ApiController]
[Route("api/sms/migration")]
[Authorize(Roles = "Admin,SMS_Admin")]
public class MigrationController : ControllerBase
{
    private readonly IMigrationStateService _migrationStateService;
    private readonly IMigrationMetricsCollector _metricsCollector;
    private readonly ISmsFeatureFlagService _featureFlagService;

    [HttpGet("status")]
    public async Task<ActionResult<MigrationStatusResponse>> GetMigrationStatusAsync()
    {
        var state = await _migrationStateService.GetCurrentStateAsync();
        var healthReport = await _metricsCollector.GetHealthReportAsync(TimeSpan.FromHours(1));

        return Ok(new MigrationStatusResponse
        {
            CurrentPhase = state.Phase,
            TrafficPercentage = state.TrafficPercentage,
            StartedAt = state.StartedAt,
            InitiatedBy = state.InitiatedBy,
            HealthReport = healthReport,
            IsRollbackAvailable = state.IsRollbackAvailable
        });
    }

    [HttpPost("advance")]
    public async Task<ActionResult<MigrationState>> AdvancePhaseAsync()
    {
        try
        {
            var newState = await _migrationStateService.AdvanceToNextPhaseAsync();
            return Ok(newState);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("rollback")]
    public async Task<ActionResult<MigrationState>> RollbackPhaseAsync()
    {
        try
        {
            var newState = await _migrationStateService.RollbackToPreviousPhaseAsync();
            return Ok(newState);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("traffic-percentage")]
    public async Task<ActionResult<MigrationState>> SetTrafficPercentageAsync(
        [FromBody] SetTrafficPercentageRequest request)
    {
        if (request.Percentage < 0 || request.Percentage > 100)
        {
            return BadRequest("Traffic percentage must be between 0 and 100");
        }

        var newState = await _migrationStateService.SetTrafficPercentageAsync(request.Percentage);
        return Ok(newState);
    }

    [HttpGet("health")]
    public async Task<ActionResult<MigrationHealthReport>> GetMigrationHealthAsync(
        [FromQuery] int hours = 1)
    {
        var period = TimeSpan.FromHours(hours);
        var healthReport = await _metricsCollector.GetHealthReportAsync(period);
        return Ok(healthReport);
    }
}
```

### Configuration Structure
```json
{
  "SmsFeatureFlags": {
    "EnableAfricasTalking": true,
    "AfricasTalkingTrafficPercentage": 25,
    "EnableAutomaticRollback": true,
    "RollbackThresholds": {
      "MaxErrorRate": 5.0,
      "MinSuccessRate": 90.0,
      "MaxResponseTimeMs": 5000
    },
    "UserOverrides": {
      "admin@intellifin.com": "AfricasTalking",
      "test@intellifin.com": "AfricasTalking"
    },
    "BranchOverrides": {
      "1": "AfricasTalking",
      "2": "Legacy"
    }
  },
  "MigrationSettings": {
    "AutoAdvanceEnabled": false,
    "HealthCheckIntervalMinutes": 5,
    "MinimumPhaseTimeMinutes": 30,
    "RequireApprovalForAdvance": true,
    "NotificationRecipients": ["admin@intellifin.com"],
    "MetricsRetentionDays": 30
  }
}
```

## Dependencies
- **Story 2.1**: Africa's Talking provider implementation
- **Story 2.2**: Provider abstraction layer
- **Story 2.3**: Cost tracking for migration validation
- **Infrastructure**: Feature flag configuration system

## Risks and Mitigation

### Technical Risks
- **Configuration Errors**: Comprehensive validation and testing of feature flag logic
- **Provider Health Detection**: Accurate health monitoring and alerting
- **Traffic Routing Logic**: Extensive testing of percentage-based routing
- **State Management**: Reliable migration state persistence and recovery

### Business Risks
- **Service Disruption**: Immediate rollback capabilities and monitoring
- **Data Loss**: Comprehensive logging and audit trails
- **Migration Failures**: Automated rollback triggers and manual override capabilities

## Testing Strategy

### Unit Tests
- [ ] Feature flag logic and routing decisions
- [ ] Migration state transitions
- [ ] Health check calculations
- [ ] Traffic percentage calculations
- [ ] Rollback mechanisms

### Integration Tests
- [ ] End-to-end migration workflows
- [ ] Provider switching scenarios
- [ ] Health monitoring integration
- [ ] Configuration management
- [ ] Automatic rollback triggers

### Migration Testing
- [ ] Gradual traffic migration scenarios
- [ ] Provider failure simulation
- [ ] Rollback procedures validation
- [ ] Health threshold validation
- [ ] Performance impact assessment

## Success Metrics
- **Zero Downtime**: 100% SMS service availability during migration
- **Rollback Time**: <2 minutes for emergency rollback
- **Migration Health**: Continuous monitoring with 5-minute intervals
- **Success Rate Maintenance**: >99% SMS delivery rate throughout migration
- **Performance**: No degradation in response times during migration

## Definition of Done
- [ ] All acceptance criteria implemented and tested
- [ ] Feature flag system operational
- [ ] Migration phases defined and configurable
- [ ] Health monitoring and alerting functional
- [ ] Rollback procedures tested and documented
- [ ] Configuration management operational
- [ ] Performance testing completed
- [ ] Security review completed
- [ ] Migration runbook created
- [ ] Team training completed

## Related Stories
- **Prerequisite**: Story 2.1 (Africa's Talking provider), Story 2.2 (Provider abstraction)
- **Related**: Story 2.3 (Cost tracking), Story 2.5 (Enhanced delivery tracking)
- **Successor**: Story 2.6 (Configuration management)

## Migration Runbook Reference

### Phase Progression
1. **Phase 1: Parallel Setup** (1% traffic) - Validate integration works
2. **Phase 2: Gradual Migration** (5%, 10%, 25%, 50%, 75%) - Monitor health metrics
3. **Phase 3: Legacy Deprecation** (90%, 95%, 98%) - Prepare for complete migration
4. **Phase 4: Complete Migration** (100%) - Full Africa's Talking usage

### Emergency Procedures
- **Immediate Rollback**: Set traffic percentage to 0%
- **Health Alert Response**: Investigate within 5 minutes
- **Provider Failure**: Automatic fallback activation
- **Configuration Errors**: Revert to last known good configuration

This migration strategy ensures safe, controlled transition to Africa's Talking with comprehensive monitoring and rollback capabilities.