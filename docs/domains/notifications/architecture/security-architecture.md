# Security Architecture

## Overview
The Communications enhancement security architecture ensures data protection, secure integrations, audit compliance, and risk mitigation while maintaining the existing security model of the IntelliFin system.

## Authentication and Authorization

### Existing Security Model Integration
```csharp
// Leverage existing JWT authentication
[Authorize]
[ApiController]
[Route("api/communications")]
public class CommunicationsController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;
    private readonly IBranchContextService _branchContext;

    [HttpGet("templates")]
    [RequiredPermission("Communications.Templates.Read")]
    public async Task<ActionResult<List<NotificationTemplate>>> GetTemplatesAsync(
        [FromQuery] string? category = null,
        [FromQuery] string? channel = null)
    {
        // Existing branch context validation
        var branchId = _branchContext.GetCurrentBranchId();

        // Role-based filtering
        var templates = await GetTemplatesForBranchAsync(branchId, category, channel);

        return Ok(templates);
    }

    [HttpPost("templates")]
    [RequiredPermission("Communications.Templates.Write")]
    public async Task<ActionResult<NotificationTemplate>> CreateTemplateAsync(
        [FromBody] CreateTemplateRequest request)
    {
        // Audit logging for template creation
        await LogTemplateOperationAsync("CREATE", request);

        var template = await _templateService.CreateAsync(request);
        return CreatedAtAction(nameof(GetTemplateById), new { id = template.Id }, template);
    }
}
```

### Role-Based Access Control
```csharp
public static class CommunicationsPermissions
{
    // Template management permissions
    public const string TemplatesRead = "Communications.Templates.Read";
    public const string TemplatesWrite = "Communications.Templates.Write";
    public const string TemplatesDelete = "Communications.Templates.Delete";
    public const string TemplatesTest = "Communications.Templates.Test";

    // Notification permissions
    public const string NotificationsRead = "Communications.Notifications.Read";
    public const string NotificationsSend = "Communications.Notifications.Send";
    public const string NotificationsHistory = "Communications.Notifications.History";

    // Administrative permissions
    public const string ConfigurationManage = "Communications.Configuration.Manage";
    public const string AuditAccess = "Communications.Audit.Access";
    public const string SystemAlerts = "Communications.SystemAlerts.Access";
}

// Role-based template access
public class TemplateAccessService
{
    public async Task<bool> CanAccessTemplateAsync(string userId, int templateId)
    {
        var user = await _userService.GetUserAsync(userId);
        var template = await _templateRepository.GetByIdAsync(templateId);

        // Branch-level access control
        if (!user.HasAccessToBranch(template.BranchId))
        {
            return false;
        }

        // Role-based access
        return user.Role switch
        {
            "LoanOfficer" => template.Category == "LoanOrigination",
            "CollectionsOfficer" => template.Category == "Collections",
            "BranchManager" => true, // Access to all branch templates
            "SystemAdmin" => true,   // Access to all templates
            _ => false
        };
    }
}
```

## Data Protection and Encryption

### Sensitive Data Handling
```csharp
public class SecureNotificationLog : NotificationLog
{
    // Encrypted PII fields
    [EncryptedColumn]
    public override string RecipientId { get; set; } = string.Empty;

    [EncryptedColumn]
    public override string Content { get; set; } = string.Empty;

    [EncryptedColumn]
    public override string? PersonalizationData { get; set; }

    // Non-encrypted metadata for querying
    public string RecipientIdHash { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
}

public class DataProtectionService
{
    private readonly IDataProtector _protector;
    private readonly IHashingService _hashingService;

    public async Task<NotificationLog> SecureNotificationAsync(NotificationLog notification)
    {
        // Encrypt sensitive fields
        notification.RecipientId = _protector.Protect(notification.RecipientId);
        notification.Content = _protector.Protect(notification.Content);

        if (!string.IsNullOrEmpty(notification.PersonalizationData))
        {
            notification.PersonalizationData = _protector.Protect(notification.PersonalizationData);
        }

        // Create searchable hashes
        notification.RecipientIdHash = _hashingService.Hash(notification.RecipientId);
        notification.ContentHash = _hashingService.Hash(notification.Content);

        return notification;
    }

    public async Task<NotificationLog> UnsecureNotificationAsync(NotificationLog notification)
    {
        // Decrypt sensitive fields for authorized access
        notification.RecipientId = _protector.Unprotect(notification.RecipientId);
        notification.Content = _protector.Unprotect(notification.Content);

        if (!string.IsNullOrEmpty(notification.PersonalizationData))
        {
            notification.PersonalizationData = _protector.Unprotect(notification.PersonalizationData);
        }

        return notification;
    }
}
```

### Database Security Configuration
```csharp
public class SecureLmsDbContext : LmsDbContext
{
    private readonly IDataProtectionService _dataProtection;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure encryption for sensitive fields
        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.Property(e => e.RecipientId)
                  .HasConversion(
                      v => _dataProtection.Encrypt(v),
                      v => _dataProtection.Decrypt(v));

            entity.Property(e => e.Content)
                  .HasConversion(
                      v => _dataProtection.Encrypt(v),
                      v => _dataProtection.Decrypt(v));

            // Add indexes on hash fields for searching
            entity.HasIndex(e => e.RecipientIdHash);
            entity.HasIndex(e => e.ContentHash);
        });

        // Row-level security for branch isolation
        modelBuilder.Entity<NotificationLog>()
            .HasQueryFilter(e => e.BranchId == GetCurrentBranchId());
    }
}
```

## External Integration Security

### Africa's Talking API Security
```csharp
public class SecureAfricasTalkingSmsProvider : AfricasTalkingSmsProvider
{
    private readonly ISecretsManager _secretsManager;
    private readonly IHttpClientFactory _httpClientFactory;

    protected override async Task<HttpClient> CreateSecureClientAsync()
    {
        var client = _httpClientFactory.CreateClient("AfricasTalkingSecure");

        // Retrieve API key securely
        var apiKey = await _secretsManager.GetSecretAsync("AfricasTalking:ApiKey");
        client.DefaultRequestHeaders.Add("apikey", apiKey);

        // Add request signing
        client.DefaultRequestHeaders.Add("X-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

        return client;
    }

    public override async Task<SmsResult> SendAsync(SmsRequest request, CancellationToken cancellationToken = default)
    {
        // Validate phone number format to prevent injection
        if (!IsValidPhoneNumber(request.To))
        {
            return new SmsResult
            {
                Success = false,
                ErrorMessage = "Invalid phone number format"
            };
        }

        // Sanitize message content
        var sanitizedMessage = SanitizeMessageContent(request.Message);
        request = request with { Message = sanitizedMessage };

        return await base.SendAsync(request, cancellationToken);
    }

    private bool IsValidPhoneNumber(string phoneNumber)
    {
        // Strict validation for Zambian phone numbers
        var zambiaMobilePattern = @"^\+260[79][0-9]{8}$";
        return Regex.IsMatch(phoneNumber, zambiaMobilePattern);
    }

    private string SanitizeMessageContent(string content)
    {
        // Remove potentially dangerous characters
        var sanitized = Regex.Replace(content, @"[<>""']", string.Empty);

        // Limit message length
        if (sanitized.Length > 160)
        {
            sanitized = sanitized[..157] + "...";
        }

        return sanitized;
    }
}
```

### Webhook Security
```csharp
public class WebhookSecurityService : IWebhookSecurityService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhookSecurityService> _logger;
    private readonly IMemoryCache _nonceCache;

    public async Task<bool> VerifyWebhookAsync(HttpRequest request)
    {
        try
        {
            // Rate limiting for webhook endpoints
            if (!await CheckRateLimitAsync(GetClientIPAddress(request)))
            {
                _logger.LogWarning("Webhook rate limit exceeded for IP {IP}", GetClientIPAddress(request));
                return false;
            }

            // Verify signature
            if (!await VerifySignatureAsync(request))
            {
                _logger.LogWarning("Webhook signature verification failed");
                return false;
            }

            // Verify timestamp to prevent replay attacks
            if (!VerifyTimestamp(request))
            {
                _logger.LogWarning("Webhook timestamp verification failed");
                return false;
            }

            // Verify nonce to prevent duplicate processing
            if (!VerifyNonce(request))
            {
                _logger.LogWarning("Webhook nonce verification failed");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook security verification failed");
            return false;
        }
    }

    private async Task<bool> VerifySignatureAsync(HttpRequest request)
    {
        var signature = request.Headers["X-AfricasTalking-Signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        var body = await ReadRequestBodyAsync(request);
        var secret = await _secretsManager.GetSecretAsync("AfricasTalking:WebhookSecret");
        var expectedSignature = ComputeHMACSHA256(body, secret);

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(signature),
            Convert.FromBase64String(expectedSignature));
    }

    private bool VerifyTimestamp(HttpRequest request)
    {
        var timestampHeader = request.Headers["X-Timestamp"].FirstOrDefault();
        if (!long.TryParse(timestampHeader, out var timestamp))
        {
            return false;
        }

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        var now = DateTimeOffset.UtcNow;

        // Allow 5-minute window
        return Math.Abs((now - requestTime).TotalMinutes) <= 5;
    }

    private bool VerifyNonce(HttpRequest request)
    {
        var nonce = request.Headers["X-Nonce"].FirstOrDefault();
        if (string.IsNullOrEmpty(nonce))
        {
            return false;
        }

        // Check if nonce already used
        if (_nonceCache.TryGetValue($"nonce:{nonce}", out _))
        {
            return false;
        }

        // Store nonce for 10 minutes
        _nonceCache.Set($"nonce:{nonce}", true, TimeSpan.FromMinutes(10));
        return true;
    }
}
```

## Input Validation and Sanitization

### Template Content Validation
```csharp
public class TemplateSecurityValidator
{
    private static readonly string[] ProhibitedTags = { "script", "iframe", "object", "embed", "form" };
    private static readonly string[] AllowedTokens =
    {
        "customer_name", "customer_first_name", "customer_phone", "customer_email",
        "loan_reference", "loan_amount", "loan_balance", "due_date",
        "company_name", "company_phone", "current_date"
    };

    public TemplateValidationResult ValidateTemplate(string content, string channel)
    {
        var result = new TemplateValidationResult { IsValid = true };

        // Check for prohibited content
        foreach (var tag in ProhibitedTags)
        {
            if (content.Contains($"<{tag}", StringComparison.OrdinalIgnoreCase))
            {
                result.IsValid = false;
                result.Errors.Add($"Prohibited tag '{tag}' found in template");
            }
        }

        // Validate personalization tokens
        var tokens = ExtractTokens(content);
        var unauthorizedTokens = tokens.Except(AllowedTokens, StringComparer.OrdinalIgnoreCase).ToList();
        if (unauthorizedTokens.Any())
        {
            result.IsValid = false;
            result.Errors.Add($"Unauthorized tokens found: {string.Join(", ", unauthorizedTokens)}");
        }

        // Channel-specific validation
        if (channel.Equals("SMS", StringComparison.OrdinalIgnoreCase))
        {
            if (content.Length > 160)
            {
                result.Warnings.Add("SMS content exceeds 160 characters");
            }
        }

        // Check for XSS patterns
        var xssPatterns = new[]
        {
            @"javascript:",
            @"vbscript:",
            @"onload=",
            @"onerror=",
            @"onclick="
        };

        foreach (var pattern in xssPatterns)
        {
            if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                result.IsValid = false;
                result.Errors.Add($"Potential XSS pattern detected: {pattern}");
            }
        }

        return result;
    }
}
```

### Request Validation
```csharp
public class CreateTemplateRequestValidator : AbstractValidator<CreateTemplateRequest>
{
    public CreateTemplateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .Length(1, 100)
            .Matches(@"^[a-zA-Z0-9\-_]+$")
            .WithMessage("Template name must contain only alphanumeric characters, hyphens, and underscores");

        RuleFor(x => x.Category)
            .NotEmpty()
            .Must(BeValidCategory)
            .WithMessage("Invalid template category");

        RuleFor(x => x.Channel)
            .NotEmpty()
            .Must(BeValidChannel)
            .WithMessage("Invalid notification channel");

        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(10000)
            .Must(BeValidContent)
            .WithMessage("Template content contains invalid or prohibited elements");

        RuleFor(x => x.Subject)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Subject));
    }

    private bool BeValidCategory(string category)
    {
        var validCategories = new[] { "LoanOrigination", "Collections", "SystemAlerts", "Marketing" };
        return validCategories.Contains(category, StringComparer.OrdinalIgnoreCase);
    }

    private bool BeValidChannel(string channel)
    {
        var validChannels = new[] { "SMS", "Email", "InApp", "Push" };
        return validChannels.Contains(channel, StringComparer.OrdinalIgnoreCase);
    }

    private bool BeValidContent(string content)
    {
        var validator = new TemplateSecurityValidator();
        var result = validator.ValidateTemplate(content, "Email"); // Use strictest validation
        return result.IsValid;
    }
}
```

## Audit and Compliance

### Comprehensive Audit Logging
```csharp
public class CommunicationsAuditService
{
    private readonly IAuditRepository _auditRepository;
    private readonly ICurrentUserService _currentUser;

    public async Task LogTemplateOperationAsync(string operation, object data, int? templateId = null)
    {
        var auditEvent = new AuditEvent
        {
            ActorId = _currentUser.GetUserId(),
            Action = $"Template.{operation}",
            Entity = "NotificationTemplate",
            EntityId = templateId?.ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            IP = _currentUser.GetClientIPAddress(),
            DetailsJson = JsonSerializer.Serialize(data),
            BranchId = _currentUser.GetBranchId()
        };

        await _auditRepository.CreateAsync(auditEvent);
    }

    public async Task LogNotificationSentAsync(NotificationLog notification)
    {
        var auditEvent = new AuditEvent
        {
            ActorId = notification.CreatedBy,
            Action = "Notification.Sent",
            Entity = "NotificationLog",
            EntityId = notification.Id.ToString(),
            Timestamp = notification.CreatedAt,
            DetailsJson = JsonSerializer.Serialize(new
            {
                Channel = notification.Channel,
                RecipientType = notification.RecipientType,
                TemplateId = notification.TemplateId,
                Status = notification.Status
            }),
            BranchId = notification.BranchId
        };

        await _auditRepository.CreateAsync(auditEvent);
    }

    public async Task LogDataAccessAsync(string operation, string entityType, string entityId)
    {
        var auditEvent = new AuditEvent
        {
            ActorId = _currentUser.GetUserId(),
            Action = $"Data.{operation}",
            Entity = entityType,
            EntityId = entityId,
            Timestamp = DateTimeOffset.UtcNow,
            IP = _currentUser.GetClientIPAddress(),
            BranchId = _currentUser.GetBranchId()
        };

        await _auditRepository.CreateAsync(auditEvent);
    }
}
```

### GDPR Compliance
```csharp
public class GDPRComplianceService
{
    public async Task HandleDataSubjectRequestAsync(string customerId, GDPRRequestType requestType)
    {
        switch (requestType)
        {
            case GDPRRequestType.DataExport:
                await ExportCustomerCommunicationDataAsync(customerId);
                break;

            case GDPRRequestType.DataDeletion:
                await DeleteCustomerCommunicationDataAsync(customerId);
                break;

            case GDPRRequestType.DataCorrection:
                await CorrectCustomerCommunicationDataAsync(customerId);
                break;
        }
    }

    private async Task DeleteCustomerCommunicationDataAsync(string customerId)
    {
        // Mark notifications for deletion (maintaining audit trail)
        var notifications = await _notificationRepository.GetByRecipientAsync(customerId);

        foreach (var notification in notifications)
        {
            // Pseudonymize rather than delete for audit compliance
            notification.RecipientId = $"DELETED_{Guid.NewGuid()}";
            notification.Content = "[DELETED]";
            notification.PersonalizationData = null;

            await _notificationRepository.UpdateAsync(notification);
        }

        // Log GDPR deletion action
        await _auditService.LogGDPRActionAsync(customerId, "DataDeletion");
    }
}
```

## Security Monitoring and Alerting

### Security Event Detection
```csharp
public class SecurityMonitoringService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await MonitorSecurityEventsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task MonitorSecurityEventsAsync(CancellationToken cancellationToken)
    {
        // Monitor failed authentication attempts
        await CheckFailedAuthenticationAttemptsAsync();

        // Monitor suspicious template creation patterns
        await CheckSuspiciousTemplateActivityAsync();

        // Monitor unusual notification volume
        await CheckNotificationVolumeAnomaliesAsync();

        // Monitor webhook security violations
        await CheckWebhookSecurityViolationsAsync();
    }

    private async Task CheckFailedAuthenticationAttemptsAsync()
    {
        var recentFailures = await _auditRepository.GetRecentFailedAuthAttemptsAsync(TimeSpan.FromMinutes(15));

        // Group by IP address
        var failuresByIP = recentFailures.GroupBy(f => f.IP).Where(g => g.Count() >= 5);

        foreach (var ipGroup in failuresByIP)
        {
            await _alertService.SendSecurityAlertAsync(new SecurityAlert
            {
                Type = "SuspiciousAuthActivity",
                Severity = AlertSeverity.High,
                Message = $"Multiple failed authentication attempts from IP {ipGroup.Key}",
                Data = new { IP = ipGroup.Key, AttemptCount = ipGroup.Count() }
            });
        }
    }

    private async Task CheckSuspiciousTemplateActivityAsync()
    {
        // Monitor for rapid template creation (potential abuse)
        var recentTemplateCreations = await _auditRepository.GetRecentTemplateCreationsAsync(TimeSpan.FromHours(1));
        var creationsByUser = recentTemplateCreations.GroupBy(t => t.ActorId).Where(g => g.Count() >= 10);

        foreach (var userGroup in creationsByUser)
        {
            await _alertService.SendSecurityAlertAsync(new SecurityAlert
            {
                Type = "SuspiciousTemplateActivity",
                Severity = AlertSeverity.Medium,
                Message = $"User {userGroup.Key} created {userGroup.Count()} templates in the last hour",
                Data = new { UserId = userGroup.Key, TemplateCount = userGroup.Count() }
            });
        }
    }
}
```

This security architecture ensures comprehensive protection of the Communications enhancement while integrating seamlessly with existing IntelliFin security infrastructure.