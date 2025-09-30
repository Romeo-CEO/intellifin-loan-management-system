# Story 2.6: Configuration Management and Provider Switching

**Epic:** Epic 2 - SMS Provider Migration to Africa's Talking
**Story ID:** COMM-026
**Status:** Draft
**Priority:** High
**Effort:** 5 Story Points

## User Story
**As a** system administrator
**I want** centralized SMS provider configuration management with runtime switching capabilities
**So that** I can manage provider settings, switch providers without downtime, and optimize SMS delivery

## Business Value
- **Operational Agility**: Instant provider switching for optimal service delivery
- **Cost Optimization**: Dynamic provider selection based on cost and performance
- **Risk Management**: Quick response to provider issues or outages
- **Configuration Control**: Centralized management of complex provider settings
- **Audit Compliance**: Complete configuration change tracking and approval workflows
- **System Reliability**: Zero-downtime configuration changes

## Acceptance Criteria

### Primary Functionality
- [ ] **Centralized Configuration**: Single source of truth for SMS provider settings
  - Unified configuration interface for all providers
  - Environment-specific configuration management
  - Configuration validation and testing capabilities
  - Hierarchical configuration (global → branch → user)
- [ ] **Runtime Configuration Changes**: Update settings without application restart
  - Hot configuration reload capabilities
  - Graceful configuration transitions
  - Configuration rollback mechanisms
  - Change validation before application
- [ ] **Provider Switching**: Seamless provider switching capabilities
  - Instant provider priority changes
  - Branch-specific provider assignments
  - User-specific provider overrides
  - Emergency provider switching procedures
- [ ] **Configuration Templates**: Pre-defined configuration templates
  - Provider-specific configuration templates
  - Environment templates (Dev, Staging, Production)
  - Branch-specific templates
  - Backup and disaster recovery configurations

### Management Interface
- [ ] **Web-based Configuration UI**: User-friendly configuration management
  - Provider configuration forms with validation
  - Real-time configuration testing
  - Configuration change approval workflows
  - Configuration history and rollback options
- [ ] **API-based Management**: Programmatic configuration management
  - RESTful configuration API
  - Bulk configuration updates
  - Configuration import/export capabilities
  - Configuration synchronization across environments
- [ ] **Configuration Monitoring**: Real-time configuration health monitoring
  - Configuration validation alerts
  - Provider connectivity testing
  - Configuration drift detection
  - Performance impact monitoring

### Security and Compliance
- [ ] **Secure Configuration Storage**: Encrypted configuration storage
  - Sensitive data encryption (API keys, secrets)
  - Role-based configuration access
  - Configuration change audit trails
  - Approval workflows for sensitive changes
- [ ] **Configuration Validation**: Comprehensive validation and testing
  - Schema validation for all configurations
  - Connectivity testing before activation
  - Cost impact validation
  - Business rule validation

## Technical Implementation

### Components to Implement

#### 1. Configuration Models
```csharp
// File: apps/IntelliFin.Communications/Models/ConfigurationModels.cs
public class SmsProviderConfiguration
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 100;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public Dictionary<string, object> Settings { get; set; } = new();
    public List<BranchOverride> BranchOverrides { get; set; } = new();
    public List<UserOverride> UserOverrides { get; set; } = new();
    public ConfigurationStatus Status { get; set; } = ConfigurationStatus.Draft;
    public string? Notes { get; set; }
}

public class BranchOverride
{
    public int BranchId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Reason { get; set; }
}

public class UserOverride
{
    public string UserId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Reason { get; set; }
}

public enum ConfigurationStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Active = 3,
    Inactive = 4,
    Archived = 5
}

public class ConfigurationTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public Dictionary<string, object> DefaultSettings { get; set; } = new();
    public List<ConfigurationField> Fields { get; set; } = new();
    public bool IsSystem { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class ConfigurationField
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // String, Number, Boolean, Enum
    public bool IsRequired { get; set; } = false;
    public bool IsSecret { get; set; } = false;
    public string? DefaultValue { get; set; }
    public string? ValidationRegex { get; set; }
    public List<string>? AllowedValues { get; set; }
    public string? Description { get; set; }
}

public class ConfigurationChange
{
    public Guid Id { get; set; }
    public Guid ConfigurationId { get; set; }
    public string ChangeType { get; set; } = string.Empty; // Create, Update, Delete, Activate, Deactivate
    public Dictionary<string, object> OldValues { get; set; } = new();
    public Dictionary<string, object> NewValues { get; set; } = new();
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Reason { get; set; }
    public bool IsRollback { get; set; } = false;
}
```

#### 2. Configuration Service
```csharp
// File: apps/IntelliFin.Communications/Services/SmsConfigurationService.cs
public interface ISmsConfigurationService
{
    Task<SmsProviderConfiguration> GetConfigurationAsync(string provider, string environment);
    Task<SmsProviderConfiguration> CreateConfigurationAsync(CreateConfigurationRequest request);
    Task<SmsProviderConfiguration> UpdateConfigurationAsync(Guid configurationId, UpdateConfigurationRequest request);
    Task<bool> DeleteConfigurationAsync(Guid configurationId);
    Task<List<SmsProviderConfiguration>> GetActiveConfigurationsAsync(string environment);
    Task<bool> ActivateConfigurationAsync(Guid configurationId);
    Task<bool> DeactivateConfigurationAsync(Guid configurationId);
    Task<ConfigurationValidationResult> ValidateConfigurationAsync(SmsProviderConfiguration configuration);
    Task<bool> TestProviderConnectivityAsync(SmsProviderConfiguration configuration);
    Task ReloadConfigurationsAsync();
}

public class SmsConfigurationService : ISmsConfigurationService
{
    private readonly ISmsConfigurationRepository _configurationRepository;
    private readonly IConfigurationTemplateRepository _templateRepository;
    private readonly IConfigurationChangeRepository _changeRepository;
    private readonly ISecretEncryptionService _encryptionService;
    private readonly IProviderConnectivityTestService _connectivityTestService;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _systemConfiguration;
    private readonly ILogger<SmsConfigurationService> _logger;

    public async Task<SmsProviderConfiguration> CreateConfigurationAsync(CreateConfigurationRequest request)
    {
        // Validate request
        var validationResult = await ValidateCreateRequestAsync(request);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage);
        }

        // Apply template if specified
        var configuration = await CreateFromTemplateAsync(request);

        // Encrypt sensitive settings
        configuration.Settings = await EncryptSensitiveSettingsAsync(configuration.Settings, request.Provider);

        // Save configuration
        configuration = await _configurationRepository.CreateAsync(configuration);

        // Log configuration change
        await LogConfigurationChangeAsync(configuration.Id, "Create", new(), configuration.Settings, request.CreatedBy, request.Reason);

        // Invalidate cache
        InvalidateCache(request.Provider, request.Environment);

        _logger.LogInformation("SMS configuration created for {Provider} in {Environment} by {User}",
            request.Provider, request.Environment, request.CreatedBy);

        return configuration;
    }

    public async Task<SmsProviderConfiguration> UpdateConfigurationAsync(Guid configurationId, UpdateConfigurationRequest request)
    {
        var existingConfiguration = await _configurationRepository.GetByIdAsync(configurationId);
        if (existingConfiguration == null)
        {
            throw new NotFoundException($"Configuration {configurationId} not found");
        }

        // Validate update
        var validationResult = await ValidateUpdateRequestAsync(existingConfiguration, request);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage);
        }

        // Create updated configuration
        var oldSettings = existingConfiguration.Settings;
        var updatedConfiguration = await ApplyUpdateAsync(existingConfiguration, request);

        // Encrypt sensitive settings
        updatedConfiguration.Settings = await EncryptSensitiveSettingsAsync(updatedConfiguration.Settings, updatedConfiguration.Provider);

        // Save configuration
        updatedConfiguration = await _configurationRepository.UpdateAsync(updatedConfiguration);

        // Log configuration change
        await LogConfigurationChangeAsync(configurationId, "Update", oldSettings, updatedConfiguration.Settings, request.UpdatedBy, request.Reason);

        // Invalidate cache
        InvalidateCache(updatedConfiguration.Provider, updatedConfiguration.Environment);

        // Reload configurations if this is an active configuration
        if (updatedConfiguration.Status == ConfigurationStatus.Active)
        {
            await ReloadConfigurationsAsync();
        }

        _logger.LogInformation("SMS configuration updated for {Provider} in {Environment} by {User}",
            updatedConfiguration.Provider, updatedConfiguration.Environment, request.UpdatedBy);

        return updatedConfiguration;
    }

    public async Task<ConfigurationValidationResult> ValidateConfigurationAsync(SmsProviderConfiguration configuration)
    {
        var result = new ConfigurationValidationResult { IsValid = true };

        try
        {
            // Get template for validation rules
            var template = await _templateRepository.GetByProviderAsync(configuration.Provider);
            if (template == null)
            {
                result.IsValid = false;
                result.ErrorMessage = $"No template found for provider {configuration.Provider}";
                return result;
            }

            // Validate required fields
            foreach (var field in template.Fields.Where(f => f.IsRequired))
            {
                if (!configuration.Settings.ContainsKey(field.Name) ||
                    string.IsNullOrEmpty(configuration.Settings[field.Name]?.ToString()))
                {
                    result.IsValid = false;
                    result.ValidationErrors.Add($"Required field '{field.DisplayName}' is missing");
                }
            }

            // Validate field types and patterns
            foreach (var setting in configuration.Settings)
            {
                var field = template.Fields.FirstOrDefault(f => f.Name == setting.Key);
                if (field != null)
                {
                    var validationError = ValidateField(field, setting.Value);
                    if (!string.IsNullOrEmpty(validationError))
                    {
                        result.ValidationErrors.Add(validationError);
                        result.IsValid = false;
                    }
                }
            }

            // Test connectivity if configuration is complete
            if (result.IsValid)
            {
                var connectivityResult = await _connectivityTestService.TestAsync(configuration);
                if (!connectivityResult.IsSuccessful)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Connectivity test failed: {connectivityResult.ErrorMessage}";
                }
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration validation failed for {Provider}", configuration.Provider);
            result.IsValid = false;
            result.ErrorMessage = "Validation error occurred";
        }

        return result;
    }

    public async Task ReloadConfigurationsAsync()
    {
        try
        {
            // Clear cache
            _cache.Remove("sms_configurations");

            // Notify all application instances to reload configurations
            await NotifyConfigurationReloadAsync();

            _logger.LogInformation("SMS configurations reloaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload SMS configurations");
            throw;
        }
    }

    private async Task<Dictionary<string, object>> EncryptSensitiveSettingsAsync(Dictionary<string, object> settings, string provider)
    {
        var template = await _templateRepository.GetByProviderAsync(provider);
        if (template == null) return settings;

        var encryptedSettings = new Dictionary<string, object>(settings);

        foreach (var field in template.Fields.Where(f => f.IsSecret))
        {
            if (encryptedSettings.ContainsKey(field.Name) && encryptedSettings[field.Name] != null)
            {
                var value = encryptedSettings[field.Name].ToString();
                if (!string.IsNullOrEmpty(value) && !value.StartsWith("encrypted:"))
                {
                    encryptedSettings[field.Name] = $"encrypted:{await _encryptionService.EncryptAsync(value)}";
                }
            }
        }

        return encryptedSettings;
    }

    private void InvalidateCache(string provider, string environment)
    {
        var cacheKey = $"sms_config_{provider}_{environment}";
        _cache.Remove(cacheKey);
        _cache.Remove("sms_configurations");
    }
}

public class ConfigurationValidationResult
{
    public bool IsValid { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}
```

#### 3. Configuration Template Service
```csharp
// File: apps/IntelliFin.Communications/Services/ConfigurationTemplateService.cs
public interface IConfigurationTemplateService
{
    Task<ConfigurationTemplate> GetTemplateAsync(string provider);
    Task<ConfigurationTemplate> CreateTemplateAsync(CreateTemplateRequest request);
    Task<ConfigurationTemplate> UpdateTemplateAsync(Guid templateId, UpdateTemplateRequest request);
    Task<List<ConfigurationTemplate>> GetAvailableTemplatesAsync();
    Task<SmsProviderConfiguration> CreateConfigurationFromTemplateAsync(Guid templateId, CreateFromTemplateRequest request);
}

public class ConfigurationTemplateService : IConfigurationTemplateService
{
    private readonly IConfigurationTemplateRepository _templateRepository;
    private readonly ISmsConfigurationService _configurationService;
    private readonly ILogger<ConfigurationTemplateService> _logger;

    public async Task<ConfigurationTemplate> CreateTemplateAsync(CreateTemplateRequest request)
    {
        var template = new ConfigurationTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Provider = request.Provider,
            Environment = request.Environment,
            DefaultSettings = request.DefaultSettings,
            Fields = request.Fields,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy
        };

        template = await _templateRepository.CreateAsync(template);

        _logger.LogInformation("Configuration template created: {TemplateName} for {Provider}",
            template.Name, template.Provider);

        return template;
    }

    public async Task<SmsProviderConfiguration> CreateConfigurationFromTemplateAsync(Guid templateId, CreateFromTemplateRequest request)
    {
        var template = await _templateRepository.GetByIdAsync(templateId);
        if (template == null)
        {
            throw new NotFoundException($"Template {templateId} not found");
        }

        var configuration = new SmsProviderConfiguration
        {
            Id = Guid.NewGuid(),
            Provider = template.Provider,
            Environment = request.Environment,
            Settings = new Dictionary<string, object>(template.DefaultSettings),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy,
            Status = ConfigurationStatus.Draft,
            Notes = $"Created from template: {template.Name}"
        };

        // Override with provided settings
        foreach (var setting in request.SettingOverrides)
        {
            configuration.Settings[setting.Key] = setting.Value;
        }

        return await _configurationService.CreateConfigurationAsync(new CreateConfigurationRequest
        {
            Provider = configuration.Provider,
            Environment = configuration.Environment,
            Settings = configuration.Settings,
            CreatedBy = configuration.CreatedBy,
            Reason = configuration.Notes
        });
    }
}
```

#### 4. Provider Switching Service
```csharp
// File: apps/IntelliFin.Communications/Services/ProviderSwitchingService.cs
public interface IProviderSwitchingService
{
    Task<ProviderSwitchResult> SwitchPrimaryProviderAsync(SwitchProviderRequest request);
    Task<ProviderSwitchResult> SetBranchProviderAsync(int branchId, string provider, string reason);
    Task<ProviderSwitchResult> SetUserProviderAsync(string userId, string provider, string reason);
    Task<ProviderSwitchResult> EmergencySwitchAsync(EmergencySwitchRequest request);
    Task<List<ProviderOverride>> GetActiveOverridesAsync();
    Task<bool> RemoveOverrideAsync(Guid overrideId);
}

public class ProviderSwitchingService : IProviderSwitchingService
{
    private readonly ISmsConfigurationService _configurationService;
    private readonly IProviderOverrideRepository _overrideRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ProviderSwitchingService> _logger;

    public async Task<ProviderSwitchResult> SwitchPrimaryProviderAsync(SwitchProviderRequest request)
    {
        try
        {
            // Validate target provider
            var targetConfig = await _configurationService.GetConfigurationAsync(request.TargetProvider, request.Environment);
            if (targetConfig == null || targetConfig.Status != ConfigurationStatus.Active)
            {
                return new ProviderSwitchResult
                {
                    Success = false,
                    ErrorMessage = $"Target provider {request.TargetProvider} is not available or active"
                };
            }

            // Test connectivity
            if (!await _configurationService.TestProviderConnectivityAsync(targetConfig))
            {
                return new ProviderSwitchResult
                {
                    Success = false,
                    ErrorMessage = $"Connectivity test failed for {request.TargetProvider}"
                };
            }

            // Update provider priorities
            await UpdateProviderPrioritiesAsync(request.TargetProvider, request.Environment);

            // Reload configurations
            await _configurationService.ReloadConfigurationsAsync();

            // Notify stakeholders
            await NotifyProviderSwitchAsync(request);

            _logger.LogInformation("Primary SMS provider switched to {Provider} in {Environment} by {User}",
                request.TargetProvider, request.Environment, request.SwitchedBy);

            return new ProviderSwitchResult
            {
                Success = true,
                Message = $"Successfully switched primary provider to {request.TargetProvider}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch primary provider to {Provider}", request.TargetProvider);
            return new ProviderSwitchResult
            {
                Success = false,
                ErrorMessage = "Provider switch failed due to system error"
            };
        }
    }

    public async Task<ProviderSwitchResult> EmergencySwitchAsync(EmergencySwitchRequest request)
    {
        try
        {
            _logger.LogWarning("Emergency provider switch initiated: {Reason}", request.Reason);

            // Create emergency override
            var emergencyOverride = new ProviderOverride
            {
                Id = Guid.NewGuid(),
                Type = OverrideType.Emergency,
                TargetProvider = request.TargetProvider,
                Reason = request.Reason,
                CreatedBy = request.InitiatedBy,
                CreatedAt = DateTime.UtcNow,
                EffectiveFrom = DateTime.UtcNow,
                IsActive = true,
                Priority = 1000 // Highest priority
            };

            await _overrideRepository.CreateAsync(emergencyOverride);

            // Reload configurations immediately
            await _configurationService.ReloadConfigurationsAsync();

            // Send emergency notifications
            await SendEmergencyNotificationsAsync(request);

            _logger.LogWarning("Emergency provider switch completed: Switched to {Provider}",
                request.TargetProvider);

            return new ProviderSwitchResult
            {
                Success = true,
                Message = $"Emergency switch to {request.TargetProvider} completed",
                OverrideId = emergencyOverride.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Emergency provider switch failed");
            return new ProviderSwitchResult
            {
                Success = false,
                ErrorMessage = "Emergency switch failed"
            };
        }
    }

    private async Task UpdateProviderPrioritiesAsync(string newPrimaryProvider, string environment)
    {
        var configurations = await _configurationService.GetActiveConfigurationsAsync(environment);

        foreach (var config in configurations)
        {
            var newPriority = config.Provider == newPrimaryProvider ? 1 : config.Priority + 1;

            if (config.Priority != newPriority)
            {
                await _configurationService.UpdateConfigurationAsync(config.Id, new UpdateConfigurationRequest
                {
                    Priority = newPriority,
                    UpdatedBy = "System",
                    Reason = $"Priority updated due to primary provider switch to {newPrimaryProvider}"
                });
            }
        }
    }
}

public class ProviderSwitchResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? OverrideId { get; set; }
}
```

#### 5. Configuration Management Controller
```csharp
// File: apps/IntelliFin.Communications/Controllers/SmsConfigurationController.cs
[ApiController]
[Route("api/sms/configuration")]
[Authorize(Roles = "Admin,SMS_Admin")]
public class SmsConfigurationController : ControllerBase
{
    private readonly ISmsConfigurationService _configurationService;
    private readonly IConfigurationTemplateService _templateService;
    private readonly IProviderSwitchingService _switchingService;

    [HttpGet]
    public async Task<ActionResult<List<SmsProviderConfiguration>>> GetConfigurationsAsync(
        [FromQuery] string? environment = null,
        [FromQuery] bool activeOnly = false)
    {
        var configurations = activeOnly
            ? await _configurationService.GetActiveConfigurationsAsync(environment ?? "Production")
            : await _configurationService.GetAllConfigurationsAsync(environment);

        return Ok(configurations);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SmsProviderConfiguration>> GetConfigurationAsync(Guid id)
    {
        var configuration = await _configurationService.GetConfigurationByIdAsync(id);
        if (configuration == null)
        {
            return NotFound();
        }

        return Ok(configuration);
    }

    [HttpPost]
    public async Task<ActionResult<SmsProviderConfiguration>> CreateConfigurationAsync(
        [FromBody] CreateConfigurationRequest request)
    {
        try
        {
            var configuration = await _configurationService.CreateConfigurationAsync(request);
            return CreatedAtAction(nameof(GetConfigurationAsync), new { id = configuration.Id }, configuration);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SmsProviderConfiguration>> UpdateConfigurationAsync(
        Guid id,
        [FromBody] UpdateConfigurationRequest request)
    {
        try
        {
            var configuration = await _configurationService.UpdateConfigurationAsync(id, request);
            return Ok(configuration);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("{id}/activate")]
    public async Task<ActionResult> ActivateConfigurationAsync(Guid id)
    {
        var result = await _configurationService.ActivateConfigurationAsync(id);
        if (!result)
        {
            return BadRequest(new { Error = "Failed to activate configuration" });
        }

        return Ok(new { Message = "Configuration activated successfully" });
    }

    [HttpPost("{id}/test")]
    public async Task<ActionResult<ConfigurationTestResult>> TestConfigurationAsync(Guid id)
    {
        var configuration = await _configurationService.GetConfigurationByIdAsync(id);
        if (configuration == null)
        {
            return NotFound();
        }

        var validationResult = await _configurationService.ValidateConfigurationAsync(configuration);
        var connectivityResult = await _configurationService.TestProviderConnectivityAsync(configuration);

        return Ok(new ConfigurationTestResult
        {
            IsValid = validationResult.IsValid,
            ValidationErrors = validationResult.ValidationErrors,
            ConnectivitySuccess = connectivityResult,
            TestedAt = DateTime.UtcNow
        });
    }

    [HttpPost("switch-provider")]
    public async Task<ActionResult<ProviderSwitchResult>> SwitchProviderAsync(
        [FromBody] SwitchProviderRequest request)
    {
        var result = await _switchingService.SwitchPrimaryProviderAsync(request);
        return Ok(result);
    }

    [HttpPost("emergency-switch")]
    public async Task<ActionResult<ProviderSwitchResult>> EmergencySwitchAsync(
        [FromBody] EmergencySwitchRequest request)
    {
        var result = await _switchingService.EmergencySwitchAsync(request);
        return Ok(result);
    }

    [HttpPost("reload")]
    public async Task<ActionResult> ReloadConfigurationsAsync()
    {
        await _configurationService.ReloadConfigurationsAsync();
        return Ok(new { Message = "Configurations reloaded successfully" });
    }
}
```

### Configuration Structure
```json
{
  "SmsConfigurationManagement": {
    "EnableHotReload": true,
    "RequireApprovalForChanges": true,
    "EncryptSensitiveSettings": true,
    "ConfigurationCacheTTLMinutes": 15,
    "ConnectivityTestTimeoutSeconds": 30,
    "ApprovalWorkflow": {
      "RequireApproval": true,
      "ApprovalRoles": ["SMS_Admin", "System_Admin"],
      "AutoApprovalRules": {
        "PriorityChanges": false,
        "SettingUpdates": false,
        "EmergencyChanges": true
      }
    },
    "NotificationSettings": {
      "NotifyOnConfigChange": true,
      "NotifyOnProviderSwitch": true,
      "NotificationRecipients": ["admin@intellifin.com"]
    }
  }
}
```

## Dependencies
- **All Previous Stories**: Provider implementations, abstraction layer, migration strategy
- **Security Infrastructure**: Encryption services, role-based access control
- **UI Framework**: Configuration management interface components

## Risks and Mitigation

### Technical Risks
- **Configuration Corruption**: Comprehensive validation and backup mechanisms
- **Hot Reload Failures**: Graceful fallback to cached configurations
- **Encryption Key Management**: Secure key storage and rotation procedures
- **Performance Impact**: Efficient caching and lazy loading strategies

### Operational Risks
- **Unauthorized Changes**: Role-based access control and approval workflows
- **Configuration Drift**: Automated configuration monitoring and alerts
- **Provider Outages**: Emergency switching procedures and fallback configurations

## Testing Strategy

### Unit Tests
- [ ] Configuration validation logic
- [ ] Encryption/decryption services
- [ ] Provider switching algorithms
- [ ] Template application logic
- [ ] Hot reload mechanisms

### Integration Tests
- [ ] End-to-end configuration workflows
- [ ] Provider switching scenarios
- [ ] Template-based configuration creation
- [ ] Configuration change approval workflows
- [ ] Emergency switching procedures

### UI Tests
- [ ] Configuration management interface
- [ ] Real-time configuration testing
- [ ] Approval workflow UI
- [ ] Configuration history and rollback

## Success Metrics
- **Configuration Reload Time**: <5 seconds for hot reload
- **Provider Switch Time**: <10 seconds for planned switches, <2 seconds for emergency
- **Configuration Accuracy**: 100% validation before activation
- **Change Audit**: 100% of configuration changes logged and traceable
- **Uptime**: 99.9% availability during configuration changes

## Definition of Done
- [ ] All acceptance criteria implemented and tested
- [ ] Configuration management UI operational
- [ ] Hot reload functionality validated
- [ ] Provider switching capabilities tested
- [ ] Security measures implemented and validated
- [ ] Approval workflows operational
- [ ] Performance requirements met
- [ ] Documentation completed
- [ ] Team training completed

## Related Stories
- **Prerequisite**: All previous Epic 2 stories (foundation for configuration management)
- **Related**: Story 2.4 (Migration strategy using configuration management)
- **Successor**: Story 2.7 (Testing framework)

This configuration management system provides comprehensive control over SMS provider settings with enterprise-grade security, validation, and operational capabilities.