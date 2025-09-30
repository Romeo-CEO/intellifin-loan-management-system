# Legacy SMS Provider Deprecation Strategy

**Story ID:** SMS-2.7
**Epic:** Epic 2 - SMS Provider Migration to Africa's Talking
**Status:** Draft
**Story Points:** 3
**Priority:** Low

## User Story
**As a** system architect
**I want** a safe strategy to deprecate legacy SMS providers
**So that** we can complete the migration to Africa's Talking while maintaining emergency fallback capabilities

## Background
Define and implement the final phase of SMS provider migration by safely deprecating legacy Zambian carrier integrations (Airtel, MTN, Zamtel) while maintaining emergency fallback capabilities and ensuring complete system cleanup.

## Acceptance Criteria

### ✅ Deprecation Planning
- [ ] Create deprecation timeline with safety milestones
- [ ] Define criteria for safe legacy provider removal
- [ ] Establish emergency rollback procedures
- [ ] Document configuration changes for each deprecation phase
- [ ] Plan staff training for new provider-only operations

### ✅ Gradual Deprecation Implementation
- [ ] Phase 1: Mark legacy providers as deprecated (warning logs)
- [ ] Phase 2: Disable legacy providers for new traffic (emergency only)
- [ ] Phase 3: Remove legacy provider code while keeping interfaces
- [ ] Phase 4: Complete removal of legacy provider infrastructure
- [ ] Maintain rollback capability until final sign-off

### ✅ Emergency Fallback Preservation
- [ ] Maintain emergency manual override capability
- [ ] Keep legacy provider configurations for disaster recovery
- [ ] Preserve legacy provider activation procedures
- [ ] Document emergency escalation procedures
- [ ] Test emergency fallback activation quarterly

### ✅ Code Cleanup and Documentation
- [ ] Remove deprecated legacy provider implementations
- [ ] Clean up unused configuration sections
- [ ] Update documentation to reflect new architecture
- [ ] Archive legacy provider documentation for reference
- [ ] Update deployment and operational procedures

### ✅ Validation and Sign-off
- [ ] Validate Africa's Talking handles 100% of SMS traffic
- [ ] Confirm cost savings and performance improvements
- [ ] Obtain business stakeholder approval for final cleanup
- [ ] Complete security review of remaining code
- [ ] Document lessons learned and migration outcomes

## Technical Implementation

### Deprecation Service
```csharp
public class ProviderDeprecationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProviderDeprecationService> _logger;

    public bool IsProviderDeprecated(string providerName)
    {
        var deprecatedProviders = _configuration.GetSection("SMS:DeprecatedProviders").Get<List<string>>();
        return deprecatedProviders?.Contains(providerName) == true;
    }

    public async Task<SmsResult> HandleDeprecatedProviderAsync(string providerName, SmsRequest request)
    {
        var deprecationPolicy = _configuration.GetSection("SMS:DeprecationPolicy").Get<DeprecationPolicy>();

        _logger.LogWarning("Attempted use of deprecated SMS provider {Provider}", providerName);

        switch (deprecationPolicy.Action)
        {
            case "Warn":
                _logger.LogWarning("Provider {Provider} is deprecated but still functional", providerName);
                return await ExecuteLegacyProviderAsync(providerName, request);

            case "Block":
                _logger.LogError("Provider {Provider} is blocked - redirecting to current provider", providerName);
                return await RedirectToCurrentProviderAsync(request);

            case "Emergency":
                if (IsEmergencyOverrideActive())
                {
                    _logger.LogCritical("Emergency override active - using deprecated provider {Provider}", providerName);
                    return await ExecuteLegacyProviderAsync(providerName, request);
                }
                else
                {
                    throw new InvalidOperationException($"Provider {providerName} is deprecated and emergency override is not active");
                }

            default:
                throw new InvalidOperationException($"Unknown deprecation action: {deprecationPolicy.Action}");
        }
    }

    private bool IsEmergencyOverrideActive()
    {
        return _configuration.GetValue<bool>("SMS:EmergencyOverride:Active", false);
    }
}
```

### Legacy Provider Wrapper
```csharp
[Obsolete("Legacy SMS providers are deprecated. Use Africa's Talking provider instead.")]
public class LegacySmsProviderWrapper : ISmsProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ProviderDeprecationService _deprecationService;

    public string ProviderName => "Legacy";

    public async Task<SmsResult> SendAsync(SmsRequest request, CancellationToken cancellationToken = default)
    {
        return await _deprecationService.HandleDeprecatedProviderAsync("Legacy", request);
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        // Legacy providers always report unhealthy during deprecation
        return Task.FromResult(false);
    }
}
```

### Deprecation Configuration
```csharp
public class DeprecationPolicy
{
    public string Action { get; set; } = "Warn"; // Warn, Block, Emergency
    public DateTime DeprecationDate { get; set; }
    public DateTime RemovalDate { get; set; }
    public List<string> DeprecatedProviders { get; set; } = new();
    public bool AllowEmergencyOverride { get; set; } = true;
    public string EmergencyContact { get; set; } = string.Empty;
}

public class EmergencyOverride
{
    public bool Active { get; set; } = false;
    public DateTime ActivatedAt { get; set; }
    public string ActivatedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
```

## Files to Create/Modify

### New Files
- `apps/IntelliFin.Communications/Services/ProviderDeprecationService.cs`
- `apps/IntelliFin.Communications/Configuration/DeprecationPolicy.cs`
- `apps/IntelliFin.Communications/Providers/LegacySmsProviderWrapper.cs`
- `docs/operations/sms-provider-emergency-procedures.md`

### Modified Files
- `apps/IntelliFin.Communications/Services/SmsProviderFactory.cs` - Add deprecation checks
- `apps/IntelliFin.Communications/Extensions/ServiceCollectionExtensions.cs` - Update registrations

## Deprecation Timeline

### Phase 1: Deprecation Warning (Week 1-2)
```json
{
  "SMS": {
    "DeprecationPolicy": {
      "Action": "Warn",
      "DeprecationDate": "2024-03-01",
      "RemovalDate": "2024-04-01"
    },
    "DeprecatedProviders": ["Airtel", "MTN", "Zamtel"]
  }
}
```

### Phase 2: Block New Traffic (Week 3-4)
```json
{
  "SMS": {
    "DeprecationPolicy": {
      "Action": "Block",
      "AllowEmergencyOverride": true
    }
  }
}
```

### Phase 3: Emergency Only (Week 5-8)
```json
{
  "SMS": {
    "DeprecationPolicy": {
      "Action": "Emergency",
      "AllowEmergencyOverride": true
    },
    "EmergencyOverride": {
      "Active": false,
      "RequiresManagerApproval": true
    }
  }
}
```

### Phase 4: Complete Removal (Week 9+)
- Remove legacy provider code
- Clean up configuration sections
- Archive documentation
- Update operational procedures

## Emergency Procedures

### Emergency Override Activation
```bash
# Emergency activation command
curl -X POST /api/sms/emergency-override \
  -H "Authorization: Bearer <admin-token>" \
  -d '{
    "reason": "Africa'\''s Talking service outage",
    "duration": "PT2H",
    "activatedBy": "john.admin@intellifin.com"
  }'
```

### Emergency Provider Configuration
```json
{
  "SMS": {
    "EmergencyProvider": {
      "Type": "Legacy",
      "Configuration": {
        "Airtel": {
          "ApiKey": "<encrypted-key>",
          "BaseUrl": "https://api.airtel.zm/sms"
        },
        "MTN": {
          "ApiKey": "<encrypted-key>",
          "BaseUrl": "https://api.mtn.zm/sms"
        }
      }
    }
  }
}
```

## Testing Requirements

### Unit Tests
- [ ] Deprecation policy enforcement logic
- [ ] Emergency override activation and expiration
- [ ] Configuration validation for deprecated providers
- [ ] Warning and error message generation
- [ ] Provider wrapper behavior in various states

### Integration Tests
- [ ] End-to-end deprecation workflow testing
- [ ] Emergency override activation and SMS delivery
- [ ] Configuration changes and system behavior
- [ ] Rollback procedures and recovery testing
- [ ] Documentation accuracy validation

### Disaster Recovery Testing
- [ ] Emergency provider activation under simulated outage
- [ ] Manual override procedures and escalation
- [ ] Recovery time objectives and procedures
- [ ] Staff training and procedure validation

## Dependencies
- Hybrid provider migration implementation (Story SMS-2.4)
- Provider health monitoring system (Story SMS-2.6)
- Administrative access controls and permissions
- Change management procedures

## Success Criteria
- Legacy providers successfully deprecated without service disruption
- Emergency fallback procedures tested and validated
- 100% SMS traffic flows through Africa's Talking
- Code cleanup completed with no remaining technical debt
- Business stakeholders approve final migration completion

## Risk Mitigation
- **Emergency Situations**: Maintain emergency override capability
- **Staff Training**: Comprehensive training on new procedures
- **Documentation**: Complete operational documentation updates
- **Rollback Capability**: Preserved until final business approval
- **Monitoring**: Enhanced monitoring during deprecation phases

## Definition of Done
- [ ] All acceptance criteria implemented and tested
- [ ] Deprecation phases completed successfully
- [ ] Emergency procedures tested and documented
- [ ] Code cleanup completed and reviewed
- [ ] Business stakeholder sign-off obtained
- [ ] Operational documentation updated
- [ ] Staff training completed
- [ ] Migration retrospective conducted and documented