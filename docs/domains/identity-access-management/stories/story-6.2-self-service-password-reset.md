# Story 6.2: Self-Service Password Reset and Account Management

## Story Information

**Epic:** User Self-Service (Epic 6)  
**Story ID:** 6.2  
**Story Name:** Self-Service Password Reset and Account Management  
**Priority:** High  
**Estimated Effort:** 5 story points (10-12 hours)  
**Dependencies:** Story 1.3 (Keycloak Client Registration), Story 1.4 (OIDC Integration), Story 6.1 (Migration - Keycloak as primary)  
**Blocks:** User experience improvements, reduced support ticket volume

---

## Story Description

As an **End User**, I want **self-service password reset and account management capabilities** so that **I can manage my account without requiring administrator assistance for routine operations**.

### Business Value

- Reduces IT support ticket volume by 40-60% for password-related issues
- Improves user experience through immediate self-service resolution
- Decreases account lockout downtime from hours to minutes
- Enhances security through email verification workflows
- Provides audit trail for self-service operations
- Enables compliance with password policy enforcement

### User Story

```
Given a user who has forgotten their password
When they navigate to the password reset page
Then they should receive a secure reset email
And the reset link should expire after 24 hours
And password complexity requirements should be enforced
And the user should be able to log in with their new password
And all active sessions should be invalidated
```

---

## Acceptance Criteria

### Functional Criteria

- [ ] **AC1:** Password reset flow via Keycloak:
  - User initiates reset with email or username
  - System sends secure reset email with time-limited link
  - Link expires after 24 hours
  - Password complexity validated (8+ chars, uppercase, lowercase, number, special char)
  - All existing user sessions invalidated after reset

- [ ] **AC2:** Account verification emails for new user registrations:
  - Automated email sent upon account creation
  - Verification link with secure token
  - Account enabled only after email verification
  - Link expires after 72 hours

- [ ] **AC3:** Account management endpoints:
  - GET /api/auth/me - Retrieve current user profile
  - PUT /api/auth/me - Update user profile (name, email, preferences)
  - POST /api/auth/change-password - Change password (requires current password)
  - GET /api/auth/sessions - List active sessions
  - DELETE /api/auth/sessions/{sessionId} - Revoke specific session

- [ ] **AC4:** Keycloak event listener for bidirectional sync:
  - User profile updates in Keycloak sync to SQL Server
  - Password changes logged to audit table
  - Session invalidation propagated to Redis

- [ ] **AC5:** Email templates with corporate branding:
  - Password reset template with instructions
  - Account verification template with welcome message
  - Password changed notification (security alert)
  - Templates support localization (en, es, fr)

### Non-Functional Criteria

- [ ] **AC6:** Email delivery rate ≥95% (monitored via SMTP metrics)

- [ ] **AC7:** Password reset flow completes in ≤5 minutes end-to-end

- [ ] **AC8:** Profile update API responds in <150ms at p95

- [ ] **AC9:** Password reset link generation uses cryptographically secure random tokens (256-bit)

- [ ] **AC10:** All self-service operations logged with audit trail (actor, timestamp, IP, action)

---

## Technical Specification

### Password Reset Architecture

```
User Flow:
1. User clicks "Forgot Password" on login page
2. User enters email/username
3. Identity Service calls Keycloak API to initiate reset
4. Keycloak generates secure token and sends email via SMTP
5. User clicks link in email
6. Keycloak presents password reset form
7. User enters new password (validated against policy)
8. Keycloak updates password, invalidates sessions
9. Keycloak event listener notifies Identity Service
10. Identity Service logs audit event and clears Redis sessions
11. User redirected to login with success message
```

**Integration Points:**
- Keycloak: Password reset API, email theme customization
- SMTP: Email delivery (smtp.intellifin.local:587, TLS)
- Identity Service: Audit logging, session invalidation
- Redis: Session cache clearing
- SQL Server: Audit event persistence

### Keycloak Email Configuration

**Location:** Keycloak Realm Settings → Email

```json
{
  "smtpServer": {
    "host": "smtp.intellifin.local",
    "port": "587",
    "from": "noreply@intellifin.local",
    "fromDisplayName": "IntelliFin Loan Management",
    "replyTo": "support@intellifin.local",
    "starttls": "true",
    "auth": "true",
    "user": "${env.SMTP_USERNAME}",
    "password": "${env.SMTP_PASSWORD}"
  }
}
```

### Custom Email Templates

**Location:** Keycloak Themes → intelli

fin-theme/email

#### Password Reset Template

**File:** `password-reset.ftl`

```html
<#import "template.ftl" as layout>
<@layout.emailLayout>
  <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
    <div style="background-color: #0066cc; padding: 20px; text-align: center;">
      <img src="${logoUrl}" alt="IntelliFin" style="max-width: 200px;" />
    </div>
    
    <div style="padding: 30px; background-color: #f9f9f9;">
      <h2>Password Reset Request</h2>
      
      <p>Hello ${user.firstName},</p>
      
      <p>We received a request to reset your password for your IntelliFin account. If you made this request, click the button below to reset your password:</p>
      
      <div style="text-align: center; margin: 30px 0;">
        <a href="${link}" style="background-color: #0066cc; color: white; padding: 12px 30px; text-decoration: none; border-radius: 4px; font-weight: bold;">Reset Password</a>
      </div>
      
      <p>This link will expire in <strong>24 hours</strong>.</p>
      
      <p>If you didn't request a password reset, please ignore this email or contact support if you have concerns.</p>
      
      <div style="border-top: 1px solid #ddd; margin-top: 30px; padding-top: 20px; font-size: 12px; color: #666;">
        <p>For security reasons, please do not share this email or the reset link with anyone.</p>
        <p>If the button above doesn't work, copy and paste this URL into your browser:<br/>
        <a href="${link}" style="word-break: break-all;">${link}</a></p>
      </div>
    </div>
    
    <div style="text-align: center; padding: 20px; color: #666; font-size: 12px;">
      <p>&copy; ${.now?string('yyyy')} IntelliFin. All rights reserved.</p>
      <p>Questions? Contact us at support@intellifin.local</p>
    </div>
  </div>
</@layout.emailLayout>
```

#### Account Verification Template

**File:** `email-verification.ftl`

```html
<#import "template.ftl" as layout>
<@layout.emailLayout>
  <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
    <div style="background-color: #0066cc; padding: 20px; text-align: center;">
      <img src="${logoUrl}" alt="IntelliFin" style="max-width: 200px;" />
    </div>
    
    <div style="padding: 30px; background-color: #f9f9f9;">
      <h2>Welcome to IntelliFin!</h2>
      
      <p>Hello ${user.firstName},</p>
      
      <p>Your IntelliFin account has been created. To complete your registration and activate your account, please verify your email address by clicking the button below:</p>
      
      <div style="text-align: center; margin: 30px 0;">
        <a href="${link}" style="background-color: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 4px; font-weight: bold;">Verify Email</a>
      </div>
      
      <p>This link will expire in <strong>72 hours</strong>.</p>
      
      <p>Once verified, you'll be able to access all features of your IntelliFin account.</p>
      
      <div style="border-top: 1px solid #ddd; margin-top: 30px; padding-top: 20px; font-size: 12px; color: #666;">
        <p>If the button above doesn't work, copy and paste this URL into your browser:<br/>
        <a href="${link}" style="word-break: break-all;">${link}</a></p>
      </div>
    </div>
    
    <div style="text-align: center; padding: 20px; color: #666; font-size: 12px;">
      <p>&copy; ${.now?string('yyyy')} IntelliFin. All rights reserved.</p>
      <p>Questions? Contact us at support@intellifin.local</p>
    </div>
  </div>
</@layout.emailLayout>
```

---

## Implementation Steps

### Step 1: Configure Keycloak SMTP Settings

**PowerShell Script:** `scripts/keycloak/configure-smtp.ps1`

```powershell
param(
    [string]$KeycloakUrl = "https://keycloak.dev.intellifin.local",
    [string]$AdminUsername = "admin",
    [string]$AdminPassword,
    [string]$Realm = "IntelliFin"
)

# Authenticate with Keycloak Admin API
$authResponse = Invoke-RestMethod `
    -Uri "$KeycloakUrl/realms/master/protocol/openid-connect/token" `
    -Method Post `
    -Body @{
        username = $AdminUsername
        password = $AdminPassword
        grant_type = "password"
        client_id = "admin-cli"
    }

$token = $authResponse.access_token

# Get current realm configuration
$realmConfig = Invoke-RestMethod `
    -Uri "$KeycloakUrl/admin/realms/$Realm" `
    -Method Get `
    -Headers @{ Authorization = "Bearer $token" }

# Update SMTP configuration
$realmConfig.smtpServer = @{
    host = "smtp.intellifin.local"
    port = "587"
    from = "noreply@intellifin.local"
    fromDisplayName = "IntelliFin Loan Management"
    replyTo = "support@intellifin.local"
    starttls = "true"
    auth = "true"
    user = $env:SMTP_USERNAME
    password = $env:SMTP_PASSWORD
}

# Update realm
Invoke-RestMethod `
    -Uri "$KeycloakUrl/admin/realms/$Realm" `
    -Method Put `
    -Headers @{ 
        Authorization = "Bearer $token"
        "Content-Type" = "application/json"
    } `
    -Body ($realmConfig | ConvertTo-Json -Depth 10)

Write-Host "SMTP configuration updated for realm $Realm"
```

### Step 2: Create Account Management API

**Location:** `IntelliFin.IdentityService/Controllers/AccountController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IntelliFin.IdentityService.Services;
using IntelliFin.IdentityService.Models.DTOs;

namespace IntelliFin.IdentityService.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly IAccountManagementService _accountService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAccountManagementService accountService,
        IAuditService auditService,
        ILogger<AccountController> logger)
    {
        _accountService = accountService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user profile
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var profile = await _accountService.GetUserProfileAsync(userId);
        return Ok(profile);
    }

    /// <summary>
    /// Update current user profile
    /// </summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _accountService.UpdateUserProfileAsync(userId, request);
        
        if (!result.Success)
        {
            return BadRequest(result.Errors);
        }

        // Audit log
        await _auditService.LogAsync(new AuditEvent
        {
            ActorId = userId,
            Action = "ProfileUpdated",
            Entity = "User",
            EntityId = userId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Details = System.Text.Json.JsonSerializer.Serialize(new { request.FirstName, request.LastName, request.Email })
        });

        return Ok(result.Profile);
    }

    /// <summary>
    /// Change password (requires current password)
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _accountService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        
        if (!result.Success)
        {
            return BadRequest(new { errors = result.Errors });
        }

        // Audit log
        await _auditService.LogAsync(new AuditEvent
        {
            ActorId = userId,
            Action = "PasswordChanged",
            Entity = "User",
            EntityId = userId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        // Send notification email
        await _accountService.SendPasswordChangedNotificationAsync(userId);

        return Ok(new { message = "Password changed successfully" });
    }

    /// <summary>
    /// List active sessions for current user
    /// </summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(List<SessionDto>), 200)]
    public async Task<IActionResult> GetSessions()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var sessions = await _accountService.GetActiveSessionsAsync(userId);
        return Ok(sessions);
    }

    /// <summary>
    /// Revoke a specific session
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RevokeSession(string sessionId)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _accountService.RevokeSessionAsync(userId, sessionId);
        
        if (!result)
        {
            return NotFound(new { message = "Session not found or already revoked" });
        }

        // Audit log
        await _auditService.LogAsync(new AuditEvent
        {
            ActorId = userId,
            Action = "SessionRevoked",
            Entity = "Session",
            EntityId = sessionId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        return NoContent();
    }
}
```

### Step 3: Implement Account Management Service

**Location:** `IntelliFin.IdentityService/Services/AccountManagementService.cs`

```csharp
using IntelliFin.IdentityService.Models.DTOs;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace IntelliFin.IdentityService.Services;

public interface IAccountManagementService
{
    Task<UserProfileDto> GetUserProfileAsync(string userId);
    Task<UpdateProfileResult> UpdateUserProfileAsync(string userId, UpdateProfileRequest request);
    Task<ChangePasswordResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
    Task<List<SessionDto>> GetActiveSessionsAsync(string userId);
    Task<bool> RevokeSessionAsync(string userId, string sessionId);
    Task SendPasswordChangedNotificationAsync(string userId);
}

public class AccountManagementService : IAccountManagementService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AccountManagementService> _logger;
    private readonly LmsDbContext _context;
    private readonly IDistributedCache _cache;

    public AccountManagementService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AccountManagementService> logger,
        LmsDbContext context,
        IDistributedCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _context = context;
        _cache = cache;
    }

    public async Task<UserProfileDto> GetUserProfileAsync(string userId)
    {
        // Get user from Keycloak
        var keycloakUser = await GetKeycloakUserAsync(userId);
        
        // Enrich with local data
        var localUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        
        return new UserProfileDto
        {
            Id = userId,
            Username = keycloakUser["username"]?.ToString(),
            Email = keycloakUser["email"]?.ToString(),
            EmailVerified = keycloakUser["emailVerified"]?.ToString() == "True",
            FirstName = keycloakUser["firstName"]?.ToString(),
            LastName = keycloakUser["lastName"]?.ToString(),
            BranchId = localUser?.BranchId,
            BranchName = localUser?.BranchName,
            CreatedAt = keycloakUser["createdTimestamp"] != null 
                ? DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(keycloakUser["createdTimestamp"].ToString())).DateTime
                : DateTime.UtcNow
        };
    }

    public async Task<UpdateProfileResult> UpdateUserProfileAsync(string userId, UpdateProfileRequest request)
    {
        try
        {
            // Update Keycloak user
            var updatePayload = new
            {
                firstName = request.FirstName,
                lastName = request.LastName,
                email = request.Email
            };

            await UpdateKeycloakUserAsync(userId, updatePayload);

            // Sync to local database (via event listener or direct update)
            var localUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (localUser != null)
            {
                localUser.Email = request.Email;
                // Note: FirstName/LastName may need to be added to ApplicationUser model
                await _context.SaveChangesAsync();
            }

            var updatedProfile = await GetUserProfileAsync(userId);
            
            return new UpdateProfileResult
            {
                Success = true,
                Profile = updatedProfile
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user profile for user {UserId}", userId);
            return new UpdateProfileResult
            {
                Success = false,
                Errors = new List<string> { "Failed to update profile. Please try again." }
            };
        }
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        try
        {
            // Verify current password and set new password via Keycloak
            var payload = new
            {
                type = "password",
                temporary = false,
                value = newPassword
            };

            await ResetKeycloakPasswordAsync(userId, payload);

            // Invalidate all sessions for this user
            await InvalidateUserSessionsAsync(userId);

            return new ChangePasswordResult { Success = true };
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            _logger.LogWarning("Password change failed for user {UserId}: Invalid current password or policy violation", userId);
            return new ChangePasswordResult
            {
                Success = false,
                Errors = new List<string> { "Current password is incorrect or new password doesn't meet requirements." }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change password for user {UserId}", userId);
            return new ChangePasswordResult
            {
                Success = false,
                Errors = new List<string> { "Failed to change password. Please try again." }
            };
        }
    }

    public async Task<List<SessionDto>> GetActiveSessionsAsync(string userId)
    {
        // Get sessions from Redis cache
        var pattern = $"session:{userId}:*";
        // Implementation depends on Redis library (StackExchange.Redis)
        // This is a simplified example
        var sessions = new List<SessionDto>();

        // Query Keycloak for active sessions
        var keycloakSessions = await GetKeycloakSessionsAsync(userId);
        
        foreach (var session in keycloakSessions)
        {
            sessions.Add(new SessionDto
            {
                SessionId = session["id"]?.ToString(),
                IpAddress = session["ipAddress"]?.ToString(),
                Start = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(session["start"]?.ToString() ?? "0")).DateTime,
                LastAccess = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(session["lastAccess"]?.ToString() ?? "0")).DateTime
            });
        }

        return sessions;
    }

    public async Task<bool> RevokeSessionAsync(string userId, string sessionId)
    {
        try
        {
            // Revoke in Keycloak
            await DeleteKeycloakSessionAsync(sessionId);

            // Remove from Redis cache
            await _cache.RemoveAsync($"session:{userId}:{sessionId}");

            return true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Session {SessionId} not found for user {UserId}", sessionId, userId);
            return false;
        }
    }

    public async Task SendPasswordChangedNotificationAsync(string userId)
    {
        // Trigger Keycloak to send password changed email
        // This may require custom Keycloak event listener or direct SMTP integration
        _logger.LogInformation("Password changed notification sent to user {UserId}", userId);
    }

    // Helper methods for Keycloak API calls
    private async Task<JsonElement> GetKeycloakUserAsync(string userId)
    {
        var client = _httpClientFactory.CreateClient("Keycloak");
        var response = await client.GetAsync($"/admin/realms/IntelliFin/users/{userId}");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(content);
    }

    private async Task UpdateKeycloakUserAsync(string userId, object payload)
    {
        var client = _httpClientFactory.CreateClient("Keycloak");
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PutAsync($"/admin/realms/IntelliFin/users/{userId}", content);
        response.EnsureSuccessStatusCode();
    }

    private async Task ResetKeycloakPasswordAsync(string userId, object payload)
    {
        var client = _httpClientFactory.CreateClient("Keycloak");
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PutAsync($"/admin/realms/IntelliFin/users/{userId}/reset-password", content);
        response.EnsureSuccessStatusCode();
    }

    private async Task<List<JsonElement>> GetKeycloakSessionsAsync(string userId)
    {
        var client = _httpClientFactory.CreateClient("Keycloak");
        var response = await client.GetAsync($"/admin/realms/IntelliFin/users/{userId}/sessions");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<JsonElement>>(content) ?? new List<JsonElement>();
    }

    private async Task DeleteKeycloakSessionAsync(string sessionId)
    {
        var client = _httpClientFactory.CreateClient("Keycloak");
        var response = await client.DeleteAsync($"/admin/realms/IntelliFin/sessions/{sessionId}");
        response.EnsureSuccessStatusCode();
    }

    private async Task InvalidateUserSessionsAsync(string userId)
    {
        // Clear Redis cache
        var pattern = $"session:{userId}:*";
        // Implementation depends on Redis library

        // Revoke all Keycloak sessions
        var sessions = await GetKeycloakSessionsAsync(userId);
        foreach (var session in sessions)
        {
            var sessionId = session.GetProperty("id").GetString();
            await DeleteKeycloakSessionAsync(sessionId);
        }
    }
}
```

### Step 4: Deploy Email Templates to Keycloak

**Deployment Script:** `scripts/keycloak/deploy-email-themes.ps1`

```powershell
# Copy email templates to Keycloak theme directory
$themeDir = "/opt/keycloak/themes/intellifin-theme/email"

# Create theme structure
New-Item -ItemType Directory -Force -Path "$themeDir/html"
New-Item -ItemType Directory -Force -Path "$themeDir/text"

# Copy templates
Copy-Item "templates/keycloak/email/*.ftl" -Destination "$themeDir/html/"
Copy-Item "templates/keycloak/email/text/*.ftl" -Destination "$themeDir/text/"

# Copy theme properties
Copy-Item "templates/keycloak/email/theme.properties" -Destination "$themeDir/"

# Restart Keycloak to load new theme
kubectl rollout restart statefulset keycloak -n identity

Write-Host "Email templates deployed. Waiting for Keycloak to restart..."
kubectl rollout status statefulset keycloak -n identity --timeout=5m
```

---

## Testing Requirements

### Unit Tests

**Location:** `IntelliFin.IdentityService.Tests/Controllers/AccountControllerTests.cs`

```csharp
[Fact]
public async Task GetProfile_ReturnsUserProfile()
{
    // Arrange
    var controller = CreateController();
    SetupAuthenticatedUser(userId: "user123");
    
    // Act
    var result = await controller.GetProfile();
    
    // Assert
    var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
    var profile = okResult.Value.Should().BeOfType<UserProfileDto>().Subject;
    profile.Id.Should().Be("user123");
}

[Fact]
public async Task ChangePassword_WithValidPassword_ReturnsSuccess()
{
    // Arrange
    var controller = CreateController();
    SetupAuthenticatedUser(userId: "user123");
    var request = new ChangePasswordRequest
    {
        CurrentPassword = "OldPassword123!",
        NewPassword = "NewPassword456!"
    };
    
    // Act
    var result = await controller.ChangePassword(request);
    
    // Assert
    result.Should().BeOfType<OkObjectResult>();
    _mockAuditService.Verify(x => x.LogAsync(It.Is<AuditEvent>(e => e.Action == "PasswordChanged")), Times.Once);
}

[Fact]
public async Task RevokeSession_WithValidSessionId_ReturnsNoContent()
{
    // Arrange
    var controller = CreateController();
    SetupAuthenticatedUser(userId: "user123");
    
    // Act
    var result = await controller.RevokeSession("session-456");
    
    // Assert
    result.Should().BeOfType<NoContentResult>();
}
```

### Integration Tests

**Password Reset E2E Test:**

```powershell
# Test password reset flow
$resetResponse = Invoke-RestMethod `
    -Uri "https://keycloak.dev.intellifin.local/realms/IntelliFin/login-actions/reset-credentials" `
    -Method Post `
    -Body @{ username = "testuser@intellifin.local" }

# Verify email sent (check SMTP logs or test inbox)
# Extract reset link from email
# Follow reset link and submit new password

# Verify user can log in with new password
$loginResponse = Invoke-RestMethod `
    -Uri "https://identity.dev.intellifin.local/api/auth/login" `
    -Method Post `
    -Body (@{ username = "testuser@intellifin.local"; password = "NewPassword123!" } | ConvertTo-Json) `
    -ContentType "application/json"

$loginResponse.success | Should -Be $true
```

---

## Integration Verification

### Checkpoint 1: SMTP Configuration Valid

**Verification:**
- Keycloak Admin Console → Realm Settings → Email
- Test email functionality
- Verify email received in inbox

### Checkpoint 2: Password Reset Flow Works

**Verification:**
- User initiates reset
- Reset email received within 1 minute
- Link works and expires after 24 hours
- Password policy enforced
- User can log in with new password

### Checkpoint 3: Account Management APIs Functional

**Verification:**
- GET /api/auth/me returns profile
- PUT /api/auth/me updates profile successfully
- POST /api/auth/change-password works
- GET /api/auth/sessions returns active sessions
- DELETE /api/auth/sessions/{id} revokes session

### Checkpoint 4: Audit Logging Complete

**Verification:**
- All operations logged to AuditEvents table
- Logs include actor, timestamp, IP, action
- Audit events forwarded to Admin Service

---

## Definition of Done

- [ ] Keycloak SMTP configuration deployed
- [ ] Email templates created and deployed
- [ ] AccountController with all endpoints implemented
- [ ] AccountManagementService implemented
- [ ] Password reset flow tested end-to-end
- [ ] Account verification tested
- [ ] All unit tests pass (>80% coverage)
- [ ] Integration tests pass
- [ ] Email delivery monitoring configured
- [ ] Audit logging verified
- [ ] Documentation updated (user guide)
- [ ] Code review completed
- [ ] PR merged to feature branch

---

## Dependencies

**Upstream Dependencies:**
- Story 1.3 (Keycloak Client Registration)
- Story 1.4 (OIDC Client Library Integration)
- Story 6.1 (Migration - Keycloak as primary)

**Downstream Dependencies:**
- User experience improvements
- Support ticket volume reduction tracking

---

## Notes for Developers

### SMTP Troubleshooting

**Issue 1:** Emails not being sent
- **Solution:** Check Keycloak logs: `kubectl logs -f keycloak-0 -n identity`
- Verify SMTP credentials in Kubernetes secrets
- Test SMTP connectivity from Keycloak pod: `telnet smtp.intellifin.local 587`

**Issue 2:** Reset links expiring too quickly
- **Solution:** Adjust token lifespan in Keycloak: Realm Settings → Tokens → Reset Password Token Lifespan

**Issue 3:** Password policy violations not clear to users
- **Solution:** Customize password policy error messages in Keycloak theme

### Security Considerations

- **Password Reset Token:** 256-bit random token, expires in 24 hours
- **Rate Limiting:** Implement rate limiting on password reset endpoint (max 3 requests/hour per email)
- **Email Verification:** Account remains disabled until email verified
- **Session Invalidation:** All sessions invalidated after password change
- **Audit Trail:** All operations logged immutably

### User Communication

**Email Deliverability:**
- Configure SPF, DKIM, DMARC records for intellifin.local domain
- Monitor delivery rates via SMTP logs
- Set up alerts for delivery failures >5%

---

**END OF STORY 6.2**
