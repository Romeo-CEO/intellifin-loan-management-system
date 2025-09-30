# Epic 3, Story 4: Customer Communication Preferences Persistence

**Status:** Draft
**Epic:** Database Persistence & Audit Trail
**Story Points:** 8
**Priority:** High
**Dependencies:** Epic 3 Story 1 (Database Infrastructure), Epic 3 Story 2 (Audit Trail)

## User Story

**As a** customer and compliance officer
**I want** persistent communication preferences that are respected across all channels and tracked for compliance
**So that** customers receive communications in their preferred manner and we maintain regulatory compliance for consent management

## Problem Statement

IntelliFin currently lacks a comprehensive system for managing customer communication preferences, leading to:

- No persistent storage of customer channel preferences (SMS vs Email vs In-App)
- Inability to honor opt-out requests consistently across all communication types
- No audit trail of preference changes for compliance purposes
- Risk of non-compliance with BoZ regulations on customer consent
- Customer frustration from receiving unwanted communications
- No granular control over communication types (collections vs notifications vs marketing)

This story implements a robust preference management system that ensures customer choices are respected while maintaining full compliance audit trails.

## Acceptance Criteria

### Preference Management System
- [ ] **AC 4.1:** Store customer preferences persistently in UserCommunicationPreferences table
- [ ] **AC 4.2:** Support granular preferences by communication type: Collections, Notifications, Marketing, Regulatory
- [ ] **AC 4.3:** Channel preference configuration (SMS, Email, In-App, Push) with priority ordering
- [ ] **AC 4.4:** Frequency preferences: Immediate, Daily Summary, Weekly Summary, Opt-Out
- [ ] **AC 4.5:** Global opt-out capability that overrides all other preferences

### Preference Application and Enforcement
- [ ] **AC 4.6:** Automatic application of preferences to all outgoing communications
- [ ] **AC 4.7:** Preference validation before sending any communication
- [ ] **AC 4.8:** Fallback channel selection when primary channel is unavailable or opted out
- [ ] **AC 4.9:** Regulatory communications sent regardless of marketing opt-outs (with appropriate logging)
- [ ] **AC 4.10:** Real-time preference checking with caching for performance

### Customer Interface
- [ ] **AC 4.11:** Customer portal for managing communication preferences
- [ ] **AC 4.12:** Simple opt-out links in all communications with immediate effect
- [ ] **AC 4.13:** Confirmation messages when preferences are updated
- [ ] **AC 4.14:** Clear indication of which communications are mandatory vs optional
- [ ] **AC 4.15:** Mobile-responsive preference management interface

### Compliance and Audit
- [ ] **AC 4.16:** Complete audit trail of all preference changes with timestamps and reasons
- [ ] **AC 4.17:** Integration with existing IntelliFin audit system for cross-system tracking
- [ ] **AC 4.18:** Opt-out compliance reporting for BoZ regulatory requirements
- [ ] **AC 4.19:** Preference history maintained for regulatory inspection purposes
- [ ] **AC 4.20:** Automatic preference backup and recovery capabilities

## Technical Implementation

### Enhanced UserCommunicationPreferences Entity
```csharp
public class UserCommunicationPreferences
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty; // Customer, LoanOfficer, etc.

    // Preference Details
    public string PreferenceType { get; set; } = string.Empty; // Collections, Notifications, Marketing, Regulatory
    public bool Enabled { get; set; } = true;
    public string? Channels { get; set; } // JSON array: ["SMS", "Email", "InApp"] in priority order
    public string? Frequency { get; set; } // Immediate, Daily, Weekly, OptOut

    // Opt-out tracking
    public DateTimeOffset? OptOutDate { get; set; }
    public string? OptOutReason { get; set; }
    public string? OptOutMethod { get; set; } // Portal, Email Link, SMS Reply, Phone Call
    public string? OptOutSource { get; set; } // Communication ID that triggered opt-out

    // Contact Information
    public string? PreferredPhoneNumber { get; set; }
    public string? PreferredEmailAddress { get; set; }
    public string? TimeZone { get; set; } = "Africa/Lusaka";
    public string? PreferredLanguage { get; set; } = "en";

    // Quiet Hours
    public TimeSpan? QuietHoursStart { get; set; } // No communications before this time
    public TimeSpan? QuietHoursEnd { get; set; } // No communications after this time
    public string? QuietDays { get; set; } // JSON array of days ["Sunday"]

    // Metadata
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public int Version { get; set; } = 1; // For optimistic concurrency

    // Computed Properties
    public List<string> ChannelList => !string.IsNullOrEmpty(Channels)
        ? JsonSerializer.Deserialize<List<string>>(Channels) ?? new List<string>()
        : new List<string>();

    public bool IsOptedOut => OptOutDate.HasValue && Frequency == "OptOut";
    public bool IsInQuietHours => IsCurrentlyInQuietHours();

    private bool IsCurrentlyInQuietHours()
    {
        if (!QuietHoursStart.HasValue || !QuietHoursEnd.HasValue)
            return false;

        var now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, TimeZone ?? "Africa/Lusaka").TimeOfDay;
        return now >= QuietHoursStart && now <= QuietHoursEnd;
    }
}

public class PreferenceChangeLog
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PreferenceType { get; set; } = string.Empty;
    public string? OldValue { get; set; } // JSON of previous preference
    public string? NewValue { get; set; } // JSON of new preference
    public string ChangeReason { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? SourceCommunicationId { get; set; } // If changed via opt-out link
}
```

### Communication Preference Service
```csharp
public interface ICommunicationPreferenceService
{
    Task<UserCommunicationPreferences> GetPreferencesAsync(string userId, string preferenceType, CancellationToken cancellationToken = default);
    Task<Dictionary<string, UserCommunicationPreferences>> GetAllPreferencesAsync(string userId, CancellationToken cancellationToken = default);
    Task<UserCommunicationPreferences> UpdatePreferencesAsync(UpdatePreferencesRequest request, CancellationToken cancellationToken = default);
    Task<OptOutResult> ProcessOptOutAsync(OptOutRequest request, CancellationToken cancellationToken = default);
    Task<bool> IsOptedOutAsync(string userId, string preferenceType, string channel, CancellationToken cancellationToken = default);
    Task<List<string>> GetPreferredChannelsAsync(string userId, string preferenceType, CancellationToken cancellationToken = default);
    Task<bool> CanSendCommunicationAsync(CanSendRequest request, CancellationToken cancellationToken = default);
    Task<PreferenceValidationResult> ValidateCommunicationAsync(CommunicationValidationRequest request, CancellationToken cancellationToken = default);
}

public class CommunicationPreferenceService : ICommunicationPreferenceService
{
    private readonly IUserCommunicationPreferencesRepository _repository;
    private readonly IPreferenceChangeLogRepository _changeLogRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CommunicationPreferenceService> _logger;

    public async Task<UserCommunicationPreferences> GetPreferencesAsync(
        string userId, string preferenceType, CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = $"user_pref:{userId}:{preferenceType}";
        var cached = await _cacheService.GetAsync<UserCommunicationPreferences>(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        var preferences = await _repository.GetByUserAndTypeAsync(userId, preferenceType, cancellationToken);

        // Create default preferences if none exist
        if (preferences == null)
        {
            preferences = await CreateDefaultPreferencesAsync(userId, preferenceType, cancellationToken);
        }

        // Cache for 15 minutes
        await _cacheService.SetAsync(cacheKey, preferences, TimeSpan.FromMinutes(15));

        return preferences;
    }

    public async Task<UserCommunicationPreferences> UpdatePreferencesAsync(
        UpdatePreferencesRequest request, CancellationToken cancellationToken = default)
    {
        var currentPreferences = await GetPreferencesAsync(request.UserId, request.PreferenceType, cancellationToken);

        // Log the change for audit compliance
        await LogPreferenceChangeAsync(currentPreferences, request);

        // Update preferences
        var updatedPreferences = new UserCommunicationPreferences
        {
            Id = currentPreferences.Id,
            UserId = request.UserId,
            UserType = currentPreferences.UserType,
            PreferenceType = request.PreferenceType,
            Enabled = request.Enabled,
            Channels = JsonSerializer.Serialize(request.Channels),
            Frequency = request.Frequency,
            PreferredPhoneNumber = request.PreferredPhoneNumber,
            PreferredEmailAddress = request.PreferredEmailAddress,
            TimeZone = request.TimeZone ?? currentPreferences.TimeZone,
            PreferredLanguage = request.PreferredLanguage ?? currentPreferences.PreferredLanguage,
            QuietHoursStart = request.QuietHoursStart,
            QuietHoursEnd = request.QuietHoursEnd,
            QuietDays = request.QuietDays != null ? JsonSerializer.Serialize(request.QuietDays) : null,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = request.UpdatedBy,
            Version = currentPreferences.Version + 1
        };

        var result = await _repository.UpdateAsync(updatedPreferences, cancellationToken);

        // Clear cache
        var cacheKey = $"user_pref:{request.UserId}:{request.PreferenceType}";
        await _cacheService.RemoveAsync(cacheKey);

        return result;
    }

    public async Task<bool> CanSendCommunicationAsync(
        CanSendRequest request, CancellationToken cancellationToken = default)
    {
        var preferences = await GetPreferencesAsync(request.UserId, request.PreferenceType, cancellationToken);

        // Always allow regulatory communications
        if (request.PreferenceType == "Regulatory")
        {
            return true;
        }

        // Check if globally opted out
        if (preferences.IsOptedOut)
        {
            return false;
        }

        // Check if specific preference type is disabled
        if (!preferences.Enabled)
        {
            return false;
        }

        // Check if channel is available and preferred
        if (!preferences.ChannelList.Contains(request.Channel))
        {
            return false;
        }

        // Check quiet hours
        if (preferences.IsInQuietHours && request.PreferenceType != "Collections")
        {
            return false;
        }

        // Check frequency preferences
        if (await IsFrequencyLimitReachedAsync(request, preferences, cancellationToken))
        {
            return false;
        }

        return true;
    }

    private async Task<bool> IsFrequencyLimitReachedAsync(
        CanSendRequest request, UserCommunicationPreferences preferences, CancellationToken cancellationToken)
    {
        if (preferences.Frequency == "Immediate")
        {
            return false;
        }

        var checkPeriod = preferences.Frequency switch
        {
            "Daily" => TimeSpan.FromDays(1),
            "Weekly" => TimeSpan.FromDays(7),
            _ => TimeSpan.Zero
        };

        if (checkPeriod == TimeSpan.Zero)
        {
            return false;
        }

        var cutoffDate = DateTimeOffset.UtcNow.Subtract(checkPeriod);
        var recentCount = await _repository.GetCommunicationCountAsync(
            request.UserId, request.PreferenceType, cutoffDate, cancellationToken);

        return recentCount > 0; // Already sent in this period
    }
}
```

### Preference Application in Notification Pipeline
```csharp
public class PreferenceEnforcementMiddleware
{
    private readonly ICommunicationPreferenceService _preferenceService;
    private readonly ILogger<PreferenceEnforcementMiddleware> _logger;

    public async Task<ProcessingResult> ProcessAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        // Validate communication against preferences
        var validationResult = await _preferenceService.ValidateCommunicationAsync(
            new CommunicationValidationRequest
            {
                UserId = request.RecipientId,
                PreferenceType = request.Category,
                Channel = request.Channel,
                IsRegulatory = request.IsRegulatory
            }, cancellationToken);

        if (!validationResult.CanSend)
        {
            return new ProcessingResult
            {
                Success = false,
                Reason = validationResult.Reason,
                AlternativeChannels = validationResult.AlternativeChannels
            };
        }

        // Apply channel preferences
        if (validationResult.ShouldUseAlternativeChannel)
        {
            request.Channel = validationResult.PreferredChannel;
            request.ContactInfo = validationResult.PreferredContactInfo;
        }

        // Apply timing preferences
        if (validationResult.ShouldDelay)
        {
            request.ScheduledFor = validationResult.SuggestedSendTime;
        }

        return new ProcessingResult { Success = true };
    }
}

public class PreferenceValidationResult
{
    public bool CanSend { get; set; }
    public string? Reason { get; set; }
    public bool ShouldUseAlternativeChannel { get; set; }
    public string? PreferredChannel { get; set; }
    public string? PreferredContactInfo { get; set; }
    public bool ShouldDelay { get; set; }
    public DateTime? SuggestedSendTime { get; set; }
    public List<string> AlternativeChannels { get; set; } = new();
}
```

### Customer Preference Management API
```csharp
[ApiController]
[Route("api/customer/preferences")]
[Authorize]
public class CustomerPreferenceController : ControllerBase
{
    private readonly ICommunicationPreferenceService _preferenceService;
    private readonly ICurrentUserService _currentUserService;

    [HttpGet]
    public async Task<ActionResult<Dictionary<string, UserCommunicationPreferences>>> GetMyPreferences(
        CancellationToken cancellationToken = default)
    {
        var customerId = _currentUserService.GetCustomerId();
        var preferences = await _preferenceService.GetAllPreferencesAsync(customerId, cancellationToken);
        return Ok(preferences);
    }

    [HttpPut("{preferenceType}")]
    public async Task<ActionResult<UserCommunicationPreferences>> UpdatePreferences(
        string preferenceType,
        [FromBody] UpdatePreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = _currentUserService.GetCustomerId();
        request.UserId = customerId;
        request.PreferenceType = preferenceType;
        request.UpdatedBy = customerId;

        var result = await _preferenceService.UpdatePreferencesAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("opt-out")]
    public async Task<ActionResult<OptOutResult>> OptOut(
        [FromBody] OptOutRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = _currentUserService.GetCustomerId();
        request.UserId = customerId;

        var result = await _preferenceService.ProcessOptOutAsync(request, cancellationToken);
        return Ok(result);
    }
}

public class UpdatePreferencesRequest
{
    public string UserId { get; set; } = string.Empty;
    public string PreferenceType { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<string> Channels { get; set; } = new();
    public string? Frequency { get; set; }
    public string? PreferredPhoneNumber { get; set; }
    public string? PreferredEmailAddress { get; set; }
    public string? TimeZone { get; set; }
    public string? PreferredLanguage { get; set; }
    public TimeSpan? QuietHoursStart { get; set; }
    public TimeSpan? QuietHoursEnd { get; set; }
    public List<string>? QuietDays { get; set; }
    public string? UpdatedBy { get; set; }
}

public class OptOutRequest
{
    public string UserId { get; set; } = string.Empty;
    public string? PreferenceType { get; set; } // null for global opt-out
    public string? Channel { get; set; } // null for all channels
    public string Reason { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty; // Portal, EmailLink, SMS, Phone
    public string? SourceCommunicationId { get; set; }
}
```

### Simplified Opt-Out Processing
```csharp
[ApiController]
[Route("api/public/opt-out")]
[AllowAnonymous]
public class PublicOptOutController : ControllerBase
{
    private readonly ICommunicationPreferenceService _preferenceService;

    [HttpGet("{token}")]
    public async Task<ActionResult> ProcessOptOutLink(
        string token,
        CancellationToken cancellationToken = default)
    {
        // Decode secure opt-out token
        var optOutData = await DecodeOptOutTokenAsync(token);

        var request = new OptOutRequest
        {
            UserId = optOutData.UserId,
            PreferenceType = optOutData.PreferenceType,
            Channel = optOutData.Channel,
            Reason = "Email link click",
            Method = "EmailLink",
            SourceCommunicationId = optOutData.CommunicationId
        };

        var result = await _preferenceService.ProcessOptOutAsync(request, cancellationToken);

        // Return user-friendly confirmation page
        return View("OptOutConfirmation", result);
    }

    [HttpPost("sms")]
    public async Task<ActionResult> ProcessSmsOptOut(
        [FromBody] SmsOptOutRequest request,
        CancellationToken cancellationToken = default)
    {
        // Process "STOP" SMS replies
        var optOutRequest = new OptOutRequest
        {
            UserId = request.UserId,
            Channel = "SMS",
            Reason = "SMS STOP reply",
            Method = "SMS"
        };

        await _preferenceService.ProcessOptOutAsync(optOutRequest, cancellationToken);
        return Ok();
    }
}
```

## Database Schema Enhancements

```sql
-- Enhanced UserCommunicationPreferences table
ALTER TABLE UserCommunicationPreferences
ADD PreferredPhoneNumber NVARCHAR(20) NULL,
    PreferredEmailAddress NVARCHAR(255) NULL,
    TimeZone NVARCHAR(50) NULL DEFAULT 'Africa/Lusaka',
    PreferredLanguage NVARCHAR(10) NULL DEFAULT 'en',
    QuietHoursStart TIME NULL,
    QuietHoursEnd TIME NULL,
    QuietDays NVARCHAR(100) NULL,
    OptOutReason NVARCHAR(500) NULL,
    OptOutMethod NVARCHAR(50) NULL,
    OptOutSource NVARCHAR(100) NULL,
    Version INT NOT NULL DEFAULT 1;

-- Preference change audit log
CREATE TABLE PreferenceChangeLogs (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(100) NOT NULL,
    PreferenceType NVARCHAR(50) NOT NULL,
    OldValue NVARCHAR(MAX) NULL,
    NewValue NVARCHAR(MAX) NULL,
    ChangeReason NVARCHAR(500) NOT NULL,
    ChangedBy NVARCHAR(100) NOT NULL,
    ChangedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    SourceCommunicationId NVARCHAR(100) NULL,

    INDEX IX_PreferenceChangeLogs_UserId_Date (UserId, ChangedAt DESC),
    INDEX IX_PreferenceChangeLogs_Type_Date (PreferenceType, ChangedAt DESC)
);

-- Performance indexes
CREATE INDEX IX_UserCommunicationPreferences_UserId_Enabled
ON UserCommunicationPreferences (UserId, Enabled)
WHERE Enabled = 1;

CREATE INDEX IX_UserCommunicationPreferences_OptOut
ON UserCommunicationPreferences (UserId, OptOutDate)
WHERE OptOutDate IS NOT NULL;
```

## Integration Testing

### Preference Management Tests
```csharp
[Test]
public async Task UpdatePreferences_ShouldLogChange()
{
    // Arrange
    var customerId = "CUST001";
    var request = new UpdatePreferencesRequest
    {
        UserId = customerId,
        PreferenceType = "Collections",
        Enabled = false,
        Channels = new List<string> { "Email" },
        UpdatedBy = customerId
    };

    // Act
    await _preferenceService.UpdatePreferencesAsync(request);

    // Assert
    var changeLogs = await _changeLogRepository.GetByUserIdAsync(customerId);
    Assert.That(changeLogs, Is.Not.Empty);
    Assert.That(changeLogs.First().PreferenceType, Is.EqualTo("Collections"));
}

[Test]
public async Task CanSendCommunication_ShouldRespectOptOut()
{
    // Arrange
    await SetupOptedOutCustomerAsync("CUST001", "Marketing");

    var request = new CanSendRequest
    {
        UserId = "CUST001",
        PreferenceType = "Marketing",
        Channel = "SMS"
    };

    // Act
    var canSend = await _preferenceService.CanSendCommunicationAsync(request);

    // Assert
    Assert.That(canSend, Is.False);
}

[Test]
public async Task CanSendCommunication_ShouldAllowRegulatory()
{
    // Arrange
    await SetupOptedOutCustomerAsync("CUST001", "Marketing");

    var request = new CanSendRequest
    {
        UserId = "CUST001",
        PreferenceType = "Regulatory",
        Channel = "SMS"
    };

    // Act
    var canSend = await _preferenceService.CanSendCommunicationAsync(request);

    // Assert
    Assert.That(canSend, Is.True);
}
```

## Success Metrics

### Compliance Metrics
- **Preference Adherence**: 100% of communications respect customer preferences
- **Opt-Out Processing**: <5 minutes from opt-out request to enforcement
- **Audit Completeness**: 100% of preference changes logged for compliance
- **Regulatory Compliance**: 100% of required communications delivered regardless of marketing opt-outs

### Customer Experience Metrics
- **Preference Update Time**: <2 seconds for preference changes to take effect
- **Opt-Out Effectiveness**: >99% success rate for immediate opt-out enforcement
- **Customer Satisfaction**: >30% reduction in communication-related complaints
- **Mobile Responsiveness**: <3 seconds preference page load time on mobile

## Risk Mitigation

### Compliance Protection
- **Immutable Audit Trail**: All preference changes permanently logged
- **Regulatory Override**: Mandatory communications sent regardless of preferences
- **Opt-Out Validation**: Multiple verification steps prevent accidental global opt-outs
- **Data Backup**: Preference history backed up for regulatory inspection

### Performance Protection
- **Caching Strategy**: Frequently accessed preferences cached in Redis
- **Database Optimization**: Efficient indexes for preference lookups
- **Fallback Mechanisms**: Default preferences when database unavailable
- **Rate Limiting**: Protection against preference update abuse

## Dependencies

### Technical Dependencies
- **Epic 3 Story 1**: Database infrastructure for UserCommunicationPreferences table
- **Epic 3 Story 2**: Audit trail integration for preference change logging
- **Cache Infrastructure**: Redis for preference caching and performance
- **Token Security**: Secure token generation for opt-out links

### Regulatory Dependencies
- **BoZ Compliance**: Adherence to customer consent and communication regulations
- **Data Protection**: Compliance with Zambian data protection laws
- **Industry Standards**: Following banking industry preference management practices

## Definition of Done

- [ ] Complete preference management system with granular controls
- [ ] Customer-facing preference portal with mobile responsiveness
- [ ] Automatic preference enforcement in communication pipeline
- [ ] Simplified opt-out processing via multiple channels
- [ ] Comprehensive audit trail for all preference changes
- [ ] Integration with existing customer authentication system
- [ ] Performance-optimized preference lookups with caching
- [ ] Regulatory communication override capabilities
- [ ] Integration tests covering all preference scenarios
- [ ] Documentation for customer service on preference management

## Notes

This preference management system is **critical for customer satisfaction and regulatory compliance**. The granular control over communication types and channels ensures customers receive only the communications they want while maintaining the ability to deliver mandatory regulatory notices.

The comprehensive audit trail ensures IntelliFin can demonstrate compliance with customer consent regulations during BoZ inspections, while the real-time enforcement prevents customer frustration from unwanted communications.