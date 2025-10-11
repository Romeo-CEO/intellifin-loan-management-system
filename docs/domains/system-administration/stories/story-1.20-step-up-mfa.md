# Story 1.20: Step-Up MFA Integration

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.20 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 4: Governance & Workflows |
| **Sprint** | Sprint 6 |
| **Story Points** | 13 |
| **Estimated Effort** | 7-10 days |
| **Priority** | P0 (Critical for security) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Story 1.1 (Keycloak), Camunda workflows, frontend integration |
| **Blocks** | Story 1.21, Story 1.24 |

---

## User Story

**As a** System Administrator,  
**I want** step-up multi-factor authentication for sensitive operations,  
**so that** high-risk actions (high-value loan approvals, config changes) require secondary authentication.

---

## Business Value

Step-up MFA adds critical security controls for high-risk operations:

- **Risk-Based Authentication**: Sensitive operations require additional authentication, not just initial login
- **Compliance**: Meets Bank of Zambia requirements for dual authorization on high-value transactions
- **Fraud Prevention**: Reduces risk of account takeover by requiring OTP for critical actions
- **Configurable Security**: System Administrators can define which operations require MFA
- **Audit Trail**: Complete visibility into MFA challenges, successes, and failures

This story establishes layered security controls that protect the most critical system operations.

---

## Acceptance Criteria

### AC1: Keycloak OTP Authenticator Configured
**Given** Keycloak IntelliFin realm is operational  
**When** configuring OTP authentication  
**Then**:
- OTP authenticator added to authentication flows
- Time-based One-Time Password (TOTP) enabled using RFC 6238
- QR code generation configured for enrollment (Google Authenticator, Authy, Microsoft Authenticator compatible)
- OTP algorithm: HMAC-SHA1 (default), HMAC-SHA256, or HMAC-SHA512
- OTP length: 6 digits
- Time step: 30 seconds
- Look-ahead window: 1 (allows 1 step forward/backward for clock drift)

### AC2: MFA Enrollment Flow Implemented
**Given** User first requires MFA  
**When** accessing MFA-protected operation  
**Then**:
- User redirected to MFA enrollment page if not enrolled
- Enrollment page displays QR code with `otpauth://` URI
- Manual entry option provided (secret key in Base32)
- User enters initial OTP code to verify enrollment
- Enrollment status stored in Keycloak user attributes: `mfa_enrolled=true`, `mfa_enrolled_at=timestamp`
- User redirected back to original operation after successful enrollment

### AC3: Camunda BPMN MFA Challenge Process
**Given** Camunda 8 is integrated  
**When** deploying MFA challenge workflow  
**Then**:
- BPMN process `step-up-mfa-challenge.bpmn` created and deployed
- Process includes: Start Event → Check MFA Enrollment → MFA Challenge → Validation → End Event
- Process variables: `userId`, `operation`, `challengeCode`, `mfaToken`, `validationResult`
- Timeout boundary event: Auto-fail after 5 minutes without OTP submission
- Process handles enrollment redirection for unenrolled users

### AC4: `[RequiresMfa]` Attribute Implementation
**Given** API endpoints require MFA protection  
**When** decorating controller actions  
**Then**:
- Custom authorization attribute `[RequiresMfa]` implemented
- Attribute checks JWT for `amr` claim containing `mfa` value
- If `amr` claim missing or doesn't include `mfa`, return HTTP 401 with error code `mfa_required`
- Response body includes: `{ "error": "mfa_required", "mfaChallengeUrl": "/api/admin/mfa/challenge", "operation": "<operation_name>" }`
- Attribute configurable with timeout: `[RequiresMfa(TimeoutMinutes = 15)]`
- Attribute applied to sensitive operations: loan approval >$50K, role assignment, config changes, data export

### AC5: MFA Challenge API Implemented
**Given** User needs to complete MFA challenge  
**When** calling MFA challenge endpoint  
**Then**:
- POST `/api/admin/mfa/challenge` endpoint accepts challenge request with `operation` parameter
- Camunda process instance created for MFA challenge
- Challenge code generated and stored temporarily (Redis cache, 5-minute TTL)
- Response includes `challengeId` and `expiresAt`
- POST `/api/admin/mfa/validate` endpoint accepts `challengeId` and `otpCode`
- Keycloak OTP validation called to verify user's OTP code
- Upon successful validation, Keycloak JWT refreshed with `amr` claim set to `["pwd", "mfa"]`
- MFA-validated token expires after 15 minutes (configurable)

### AC6: Frontend MFA Flow Integration
**Given** User triggers MFA-protected operation  
**When** API returns `mfa_required` error  
**Then**:
- Frontend detects error code `mfa_required`
- Modal dialog displayed with OTP input field
- User enters 6-digit OTP code
- Frontend calls `/api/admin/mfa/validate` with `challengeId` and `otpCode`
- On success, original API request retried automatically with MFA-validated token
- On failure, error message displayed: "Invalid OTP code. Please try again."
- After 3 failed attempts, lockout enforced (existing Keycloak lockout policy applies)

### AC7: MFA Configuration Management
**Given** System Administrator manages MFA requirements  
**When** configuring MFA policies  
**Then**:
- Admin UI includes "MFA Configuration" section
- Configurable MFA triggers:
  - Loan approval threshold (default: $50,000)
  - Role assignment: All role changes require MFA
  - User creation/deletion: Always require MFA
  - Configuration changes: Require MFA for sensitive config keys
  - Data export: Require MFA for customer PII export
- Configuration stored in database table `MfaConfiguration`
- Configuration changes require MFA (recursive MFA protection)
- Configuration audit logged

### AC8: MFA Audit Trail
**Given** MFA challenges occur  
**When** audit events are logged  
**Then**:
- Audit events logged for all MFA activities:
  - `MfaEnrolled`: User enrolled in MFA
  - `MfaChallengeInitiated`: MFA challenge started for operation
  - `MfaChallengeSucceeded`: User validated OTP successfully
  - `MfaChallengeFailed`: User entered invalid OTP
  - `MfaChallengeLockout`: User locked out after failed attempts
  - `MfaChallengeTimeout`: Challenge expired without validation
- All audit events include correlation ID, operation name, timestamp
- Failed MFA attempts trigger security alerts (>5 failures in 1 hour)

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Lines 1108-1131 (Story 1.20), Phase 4 Overview  
**Architecture Sections**: Section 4.2 (Keycloak Authentication), Section 5 (Camunda Workflows), Section 4.1 (Admin Service)  
**Requirements**: FR9 (Configurable MFA operations), NFR10 (Authentication security)

### Technology Stack

- **Identity Provider**: Keycloak 24+ with OTP authenticator
- **Workflow Engine**: Camunda 8 (Zeebe)
- **OTP Standard**: RFC 6238 (TOTP)
- **OTP Libraries**: Google Authenticator, Authy, Microsoft Authenticator
- **Cache**: Redis (challenge code storage)
- **Database**: SQL Server 2022 (Admin Service database)
- **Frontend**: React with OTP input component

### Database Schema

```sql
-- Admin Service Database

CREATE TABLE MfaConfiguration (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    OperationName NVARCHAR(200) NOT NULL UNIQUE,
    RequiresMfa BIT NOT NULL DEFAULT 1,
    TimeoutMinutes INT NOT NULL DEFAULT 15,
    Description NVARCHAR(500),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy NVARCHAR(100),
    
    INDEX IX_OperationName (OperationName)
);

-- Seed default MFA configurations
INSERT INTO MfaConfiguration (OperationName, RequiresMfa, TimeoutMinutes, Description)
VALUES 
    ('LoanApproval.HighValue', 1, 15, 'Loan approvals over $50,000'),
    ('RoleManagement.Assign', 1, 15, 'Role assignment to users'),
    ('RoleManagement.Remove', 1, 15, 'Role removal from users'),
    ('UserManagement.Create', 1, 15, 'User account creation'),
    ('UserManagement.Delete', 1, 15, 'User account deletion'),
    ('Configuration.Update', 1, 15, 'Sensitive configuration changes'),
    ('DataExport.CustomerPII', 1, 15, 'Customer PII data export'),
    ('AuditLog.Export', 1, 15, 'Audit log export');

CREATE TABLE MfaChallenges (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ChallengeId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    UserId NVARCHAR(100) NOT NULL,
    UserName NVARCHAR(200) NOT NULL,
    Operation NVARCHAR(200) NOT NULL,
    ChallengeCode NVARCHAR(100) NOT NULL,  -- For correlation with Camunda
    Status NVARCHAR(50) NOT NULL,  -- Initiated, Succeeded, Failed, Timeout, Locked
    
    CamundaProcessInstanceId NVARCHAR(100),
    
    InitiatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ValidatedAt DATETIME2 NULL,
    ExpiresAt DATETIME2 NOT NULL,
    FailedAttempts INT NOT NULL DEFAULT 0,
    
    IpAddress NVARCHAR(45),
    UserAgent NVARCHAR(500),
    CorrelationId NVARCHAR(100),
    
    INDEX IX_ChallengeId (ChallengeId),
    INDEX IX_UserId (UserId),
    INDEX IX_Status (Status),
    INDEX IX_InitiatedAt (InitiatedAt DESC),
    INDEX IX_ExpiresAt (ExpiresAt)
);

CREATE TABLE MfaEnrollments (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(100) NOT NULL UNIQUE,
    UserName NVARCHAR(200) NOT NULL,
    Enrolled BIT NOT NULL DEFAULT 0,
    EnrolledAt DATETIME2 NULL,
    SecretKey NVARCHAR(200),  -- Encrypted, for backup purposes
    LastUsedAt DATETIME2 NULL,
    
    INDEX IX_UserId (UserId)
);
```

### API Endpoints

#### MFA Challenge API

```csharp
// Controllers/MfaController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IntelliFin.Admin.Services;
using IntelliFin.Admin.Models;
using System.Security.Claims;

namespace IntelliFin.Admin.Controllers
{
    [ApiController]
    [Route("api/admin/mfa")]
    [Authorize]
    public class MfaController : ControllerBase
    {
        private readonly IMfaService _mfaService;
        private readonly ILogger<MfaController> _logger;

        public MfaController(
            IMfaService mfaService,
            ILogger<MfaController> logger)
        {
            _mfaService = mfaService;
            _logger = logger;
        }

        /// <summary>
        /// Initiate MFA challenge for sensitive operation
        /// </summary>
        [HttpPost("challenge")]
        [ProducesResponseType(typeof(MfaChallengeResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> InitiateChallenge(
            [FromBody] MfaChallengeRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.FindFirstValue(ClaimTypes.Name);
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            _logger.LogInformation(
                "MFA challenge initiated: User={UserId}, Operation={Operation}",
                userId, request.Operation);

            try
            {
                var response = await _mfaService.InitiateChallengeAsync(
                    userId,
                    userName,
                    request.Operation,
                    ipAddress,
                    userAgent,
                    cancellationToken);

                return Ok(response);
            }
            catch (UserNotEnrolledException)
            {
                return Ok(new MfaChallengeResponse
                {
                    RequiresEnrollment = true,
                    EnrollmentUrl = "/api/admin/mfa/enroll"
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Validate OTP code for MFA challenge
        /// </summary>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(MfaValidationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ValidateChallenge(
            [FromBody] MfaValidationRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation(
                "MFA validation attempt: User={UserId}, ChallengeId={ChallengeId}",
                userId, request.ChallengeId);

            try
            {
                var response = await _mfaService.ValidateChallengeAsync(
                    userId,
                    request.ChallengeId,
                    request.OtpCode,
                    cancellationToken);

                if (response.Success)
                {
                    _logger.LogInformation(
                        "MFA validation succeeded: User={UserId}, ChallengeId={ChallengeId}",
                        userId, request.ChallengeId);
                }
                else
                {
                    _logger.LogWarning(
                        "MFA validation failed: User={UserId}, ChallengeId={ChallengeId}, Attempts={Attempts}",
                        userId, request.ChallengeId, response.FailedAttempts);
                }

                return Ok(response);
            }
            catch (ChallengeNotFoundException)
            {
                return BadRequest(new { error = "Challenge not found or expired" });
            }
            catch (UserLockedException ex)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    error = "User locked out due to too many failed MFA attempts",
                    lockoutUntil = ex.LockoutUntil
                });
            }
        }

        /// <summary>
        /// Enroll user in MFA (generate QR code)
        /// </summary>
        [HttpPost("enroll")]
        [ProducesResponseType(typeof(MfaEnrollmentResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> EnrollUser(CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.FindFirstValue(ClaimTypes.Name);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            _logger.LogInformation("MFA enrollment initiated: User={UserId}", userId);

            var response = await _mfaService.GenerateEnrollmentAsync(
                userId,
                userName,
                userEmail,
                cancellationToken);

            return Ok(response);
        }

        /// <summary>
        /// Complete MFA enrollment with verification code
        /// </summary>
        [HttpPost("enroll/verify")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyEnrollment(
            [FromBody] MfaEnrollmentVerificationRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation("MFA enrollment verification: User={UserId}", userId);

            try
            {
                await _mfaService.VerifyEnrollmentAsync(
                    userId,
                    request.SecretKey,
                    request.OtpCode,
                    cancellationToken);

                _logger.LogInformation("MFA enrollment completed: User={UserId}", userId);

                return Ok(new { message = "MFA enrollment completed successfully" });
            }
            catch (InvalidOtpException)
            {
                return BadRequest(new { error = "Invalid OTP code. Please try again." });
            }
        }

        /// <summary>
        /// Get MFA enrollment status
        /// </summary>
        [HttpGet("enrollment/status")]
        [ProducesResponseType(typeof(MfaEnrollmentStatusResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEnrollmentStatus(CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var status = await _mfaService.GetEnrollmentStatusAsync(userId, cancellationToken);

            return Ok(status);
        }

        /// <summary>
        /// Get MFA configuration (System Administrator only)
        /// </summary>
        [HttpGet("config")]
        [Authorize(Roles = "System Administrator")]
        [ProducesResponseType(typeof(List<MfaConfigDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMfaConfiguration(CancellationToken cancellationToken)
        {
            var config = await _mfaService.GetMfaConfigurationAsync(cancellationToken);
            return Ok(config);
        }

        /// <summary>
        /// Update MFA configuration (System Administrator only, requires MFA)
        /// </summary>
        [HttpPut("config/{operationName}")]
        [Authorize(Roles = "System Administrator")]
        [RequiresMfa(TimeoutMinutes = 15)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateMfaConfiguration(
            string operationName,
            [FromBody] MfaConfigUpdateDto update,
            CancellationToken cancellationToken)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation(
                "MFA configuration update: Operation={Operation}, Admin={AdminId}",
                operationName, adminId);

            try
            {
                await _mfaService.UpdateMfaConfigurationAsync(
                    operationName,
                    update,
                    adminId,
                    cancellationToken);

                return Ok(new { message = "MFA configuration updated successfully" });
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }
    }
}
```

### Service Implementation

```csharp
// Services/MfaService.cs
using IntelliFin.Admin.Data;
using IntelliFin.Admin.Models;
using IntelliFin.Shared.Keycloak;
using IntelliFin.Shared.Camunda;
using IntelliFin.Shared.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text;
using OtpNet;
using QRCoder;

namespace IntelliFin.Admin.Services
{
    public interface IMfaService
    {
        Task<MfaChallengeResponse> InitiateChallengeAsync(
            string userId,
            string userName,
            string operation,
            string ipAddress,
            string userAgent,
            CancellationToken cancellationToken);

        Task<MfaValidationResponse> ValidateChallengeAsync(
            string userId,
            Guid challengeId,
            string otpCode,
            CancellationToken cancellationToken);

        Task<MfaEnrollmentResponse> GenerateEnrollmentAsync(
            string userId,
            string userName,
            string userEmail,
            CancellationToken cancellationToken);

        Task VerifyEnrollmentAsync(
            string userId,
            string secretKey,
            string otpCode,
            CancellationToken cancellationToken);

        Task<MfaEnrollmentStatusResponse> GetEnrollmentStatusAsync(
            string userId,
            CancellationToken cancellationToken);

        Task<List<MfaConfigDto>> GetMfaConfigurationAsync(CancellationToken cancellationToken);

        Task UpdateMfaConfigurationAsync(
            string operationName,
            MfaConfigUpdateDto update,
            string adminId,
            CancellationToken cancellationToken);
    }

    public class MfaService : IMfaService
    {
        private readonly AdminDbContext _dbContext;
        private readonly IKeycloakAdminClient _keycloakClient;
        private readonly ICamundaClient _camundaClient;
        private readonly IAuditService _auditService;
        private readonly IDistributedCache _cache;
        private readonly ILogger<MfaService> _logger;

        private const int OTP_LENGTH = 6;
        private const int TIME_STEP_SECONDS = 30;
        private const int CHALLENGE_TIMEOUT_MINUTES = 5;
        private const int MAX_FAILED_ATTEMPTS = 3;

        public MfaService(
            AdminDbContext dbContext,
            IKeycloakAdminClient keycloakClient,
            ICamundaClient camundaClient,
            IAuditService auditService,
            IDistributedCache cache,
            ILogger<MfaService> logger)
        {
            _dbContext = dbContext;
            _keycloakClient = keycloakClient;
            _camundaClient = camundaClient;
            _auditService = auditService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<MfaChallengeResponse> InitiateChallengeAsync(
            string userId,
            string userName,
            string operation,
            string ipAddress,
            string userAgent,
            CancellationToken cancellationToken)
        {
            // Check if user is enrolled
            var enrollment = await _dbContext.MfaEnrollments
                .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

            if (enrollment == null || !enrollment.Enrolled)
            {
                throw new UserNotEnrolledException($"User {userId} is not enrolled in MFA");
            }

            var correlationId = Guid.NewGuid().ToString("N");
            var challengeId = Guid.NewGuid();
            var challengeCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            var expiresAt = DateTime.UtcNow.AddMinutes(CHALLENGE_TIMEOUT_MINUTES);

            // Create challenge record
            var challenge = new MfaChallenge
            {
                ChallengeId = challengeId,
                UserId = userId,
                UserName = userName,
                Operation = operation,
                ChallengeCode = challengeCode,
                Status = "Initiated",
                InitiatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CorrelationId = correlationId
            };

            _dbContext.MfaChallenges.Add(challenge);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Start Camunda workflow
            var processInstanceId = await _camundaClient.StartProcessAsync(
                "step-up-mfa-challenge",
                new Dictionary<string, object>
                {
                    { "challengeId", challengeId.ToString() },
                    { "userId", userId },
                    { "userName", userName },
                    { "operation", operation },
                    { "challengeCode", challengeCode },
                    { "correlationId", correlationId }
                },
                cancellationToken);

            challenge.CamundaProcessInstanceId = processInstanceId;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Store challenge in cache for quick lookup (5 minutes TTL)
            await _cache.SetStringAsync(
                $"mfa:challenge:{challengeId}",
                System.Text.Json.JsonSerializer.Serialize(new { userId, operation, challengeCode }),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
                cancellationToken);

            // Audit log
            await _auditService.LogAsync(new AuditEvent
            {
                Actor = userId,
                Action = "MfaChallengeInitiated",
                EntityType = "MfaChallenge",
                EntityId = challengeId.ToString(),
                CorrelationId = correlationId,
                EventData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    operation = operation,
                    ipAddress = ipAddress
                })
            }, cancellationToken);

            _logger.LogInformation(
                "MFA challenge created: ChallengeId={ChallengeId}, User={UserId}, Operation={Operation}",
                challengeId, userId, operation);

            return new MfaChallengeResponse
            {
                ChallengeId = challengeId,
                ExpiresAt = expiresAt,
                RequiresEnrollment = false
            };
        }

        public async Task<MfaValidationResponse> ValidateChallengeAsync(
            string userId,
            Guid challengeId,
            string otpCode,
            CancellationToken cancellationToken)
        {
            var challenge = await _dbContext.MfaChallenges
                .FirstOrDefaultAsync(c => c.ChallengeId == challengeId, cancellationToken);

            if (challenge == null || challenge.ExpiresAt < DateTime.UtcNow)
            {
                throw new ChallengeNotFoundException("Challenge not found or expired");
            }

            if (challenge.UserId != userId)
            {
                throw new UnauthorizedException("Challenge does not belong to this user");
            }

            if (challenge.Status != "Initiated")
            {
                throw new InvalidOperationException($"Challenge is not active (status: {challenge.Status})");
            }

            // Check lockout
            if (challenge.FailedAttempts >= MAX_FAILED_ATTEMPTS)
            {
                challenge.Status = "Locked";
                await _dbContext.SaveChangesAsync(cancellationToken);
                throw new UserLockedException("User locked out", DateTime.UtcNow.AddMinutes(30));
            }

            // Validate OTP via Keycloak
            var isValid = await _keycloakClient.ValidateOtpAsync(userId, otpCode, cancellationToken);

            if (!isValid)
            {
                challenge.FailedAttempts++;
                await _dbContext.SaveChangesAsync(cancellationToken);

                // Audit log - Failed attempt
                await _auditService.LogAsync(new AuditEvent
                {
                    Actor = userId,
                    Action = "MfaChallengeFailed",
                    EntityType = "MfaChallenge",
                    EntityId = challengeId.ToString(),
                    CorrelationId = challenge.CorrelationId,
                    EventData = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        failedAttempts = challenge.FailedAttempts,
                        operation = challenge.Operation
                    })
                }, cancellationToken);

                _logger.LogWarning(
                    "MFA validation failed: ChallengeId={ChallengeId}, User={UserId}, Attempts={Attempts}",
                    challengeId, userId, challenge.FailedAttempts);

                return new MfaValidationResponse
                {
                    Success = false,
                    FailedAttempts = challenge.FailedAttempts,
                    RemainingAttempts = MAX_FAILED_ATTEMPTS - challenge.FailedAttempts,
                    Message = "Invalid OTP code. Please try again."
                };
            }

            // Validation succeeded
            challenge.Status = "Succeeded";
            challenge.ValidatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Update last used timestamp in enrollment
            var enrollment = await _dbContext.MfaEnrollments
                .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);
            if (enrollment != null)
            {
                enrollment.LastUsedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Complete Camunda task
            await _camundaClient.CompleteTaskAsync(
                challenge.CamundaProcessInstanceId!,
                "mfa-validation",
                new Dictionary<string, object>
                {
                    { "validated", true },
                    { "validatedAt", DateTime.UtcNow.ToString("o") }
                },
                cancellationToken);

            // Generate MFA-validated token (Keycloak token with amr claim)
            var mfaToken = await _keycloakClient.GenerateMfaValidatedTokenAsync(
                userId,
                TimeSpan.FromMinutes(15),
                cancellationToken);

            // Audit log - Success
            await _auditService.LogAsync(new AuditEvent
            {
                Actor = userId,
                Action = "MfaChallengeSucceeded",
                EntityType = "MfaChallenge",
                EntityId = challengeId.ToString(),
                CorrelationId = challenge.CorrelationId,
                EventData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    operation = challenge.Operation,
                    validatedAt = challenge.ValidatedAt
                })
            }, cancellationToken);

            _logger.LogInformation(
                "MFA validation succeeded: ChallengeId={ChallengeId}, User={UserId}",
                challengeId, userId);

            return new MfaValidationResponse
            {
                Success = true,
                MfaToken = mfaToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                Message = "MFA validation successful"
            };
        }

        public async Task<MfaEnrollmentResponse> GenerateEnrollmentAsync(
            string userId,
            string userName,
            string userEmail,
            CancellationToken cancellationToken)
        {
            // Check if already enrolled
            var existingEnrollment = await _dbContext.MfaEnrollments
                .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

            if (existingEnrollment?.Enrolled == true)
            {
                throw new InvalidOperationException("User is already enrolled in MFA");
            }

            // Generate secret key (Base32-encoded)
            var secretKeyBytes = KeyGeneration.GenerateRandomKey(20);
            var secretKey = Base32Encoding.ToString(secretKeyBytes);

            // Generate OTP Auth URI for QR code
            var issuer = "IntelliFin";
            var accountName = $"{userName} ({userEmail})";
            var otpAuthUri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}?secret={secretKey}&issuer={Uri.EscapeDataString(issuer)}&digits={OTP_LENGTH}&period={TIME_STEP_SECONDS}";

            // Generate QR code
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeImageBytes = qrCode.GetGraphic(10);
            var qrCodeBase64 = Convert.ToBase64String(qrCodeImageBytes);

            // Store enrollment (not yet verified)
            if (existingEnrollment == null)
            {
                _dbContext.MfaEnrollments.Add(new MfaEnrollment
                {
                    UserId = userId,
                    UserName = userName,
                    Enrolled = false,
                    SecretKey = EncryptSecretKey(secretKey)
                });
            }
            else
            {
                existingEnrollment.SecretKey = EncryptSecretKey(secretKey);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Store secret key in cache temporarily (15 minutes) for verification
            await _cache.SetStringAsync(
                $"mfa:enrollment:{userId}",
                secretKey,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) },
                cancellationToken);

            _logger.LogInformation("MFA enrollment generated: User={UserId}", userId);

            return new MfaEnrollmentResponse
            {
                QrCodeDataUri = $"data:image/png;base64,{qrCodeBase64}",
                SecretKey = secretKey,
                Issuer = issuer,
                AccountName = accountName
            };
        }

        public async Task VerifyEnrollmentAsync(
            string userId,
            string secretKey,
            string otpCode,
            CancellationToken cancellationToken)
        {
            // Retrieve secret key from cache
            var cachedSecretKey = await _cache.GetStringAsync($"mfa:enrollment:{userId}", cancellationToken);

            if (string.IsNullOrEmpty(cachedSecretKey) || cachedSecretKey != secretKey)
            {
                throw new InvalidOperationException("Enrollment session expired or invalid");
            }

            // Validate OTP
            var secretKeyBytes = Base32Encoding.ToBytes(secretKey);
            var totp = new Totp(secretKeyBytes, step: TIME_STEP_SECONDS, totpSize: OTP_LENGTH);
            var isValid = totp.VerifyTotp(otpCode, out _, new VerificationWindow(1, 1));

            if (!isValid)
            {
                throw new InvalidOtpException("Invalid OTP code");
            }

            // Mark enrollment as complete
            var enrollment = await _dbContext.MfaEnrollments
                .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

            if (enrollment == null)
            {
                throw new NotFoundException("Enrollment not found");
            }

            enrollment.Enrolled = true;
            enrollment.EnrolledAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Store in Keycloak user attributes
            await _keycloakClient.SetUserAttributeAsync(
                userId,
                "mfa_enrolled",
                "true",
                cancellationToken);

            await _keycloakClient.SetUserAttributeAsync(
                userId,
                "mfa_enrolled_at",
                DateTime.UtcNow.ToString("o"),
                cancellationToken);

            // Audit log
            await _auditService.LogAsync(new AuditEvent
            {
                Actor = userId,
                Action = "MfaEnrolled",
                EntityType = "MfaEnrollment",
                EntityId = userId,
                EventData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    enrolledAt = enrollment.EnrolledAt
                })
            }, cancellationToken);

            _logger.LogInformation("MFA enrollment completed: User={UserId}", userId);
        }

        public async Task<MfaEnrollmentStatusResponse> GetEnrollmentStatusAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            var enrollment = await _dbContext.MfaEnrollments
                .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

            return new MfaEnrollmentStatusResponse
            {
                Enrolled = enrollment?.Enrolled ?? false,
                EnrolledAt = enrollment?.EnrolledAt,
                LastUsedAt = enrollment?.LastUsedAt
            };
        }

        public async Task<List<MfaConfigDto>> GetMfaConfigurationAsync(CancellationToken cancellationToken)
        {
            var config = await _dbContext.MfaConfiguration
                .OrderBy(c => c.OperationName)
                .Select(c => new MfaConfigDto
                {
                    OperationName = c.OperationName,
                    RequiresMfa = c.RequiresMfa,
                    TimeoutMinutes = c.TimeoutMinutes,
                    Description = c.Description
                })
                .ToListAsync(cancellationToken);

            return config;
        }

        public async Task UpdateMfaConfigurationAsync(
            string operationName,
            MfaConfigUpdateDto update,
            string adminId,
            CancellationToken cancellationToken)
        {
            var config = await _dbContext.MfaConfiguration
                .FirstOrDefaultAsync(c => c.OperationName == operationName, cancellationToken);

            if (config == null)
            {
                throw new NotFoundException($"MFA configuration for operation '{operationName}' not found");
            }

            var oldValue = new { config.RequiresMfa, config.TimeoutMinutes };

            config.RequiresMfa = update.RequiresMfa;
            config.TimeoutMinutes = update.TimeoutMinutes;
            config.UpdatedBy = adminId;
            config.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Audit log
            await _auditService.LogAsync(new AuditEvent
            {
                Actor = adminId,
                Action = "MfaConfigurationUpdated",
                EntityType = "MfaConfiguration",
                EntityId = operationName,
                EventData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    oldValue = oldValue,
                    newValue = new { update.RequiresMfa, update.TimeoutMinutes }
                })
            }, cancellationToken);

            _logger.LogInformation(
                "MFA configuration updated: Operation={Operation}, Admin={AdminId}",
                operationName, adminId);
        }

        private string EncryptSecretKey(string secretKey)
        {
            // TODO: Implement encryption using Data Protection API or Vault
            // For now, store as-is (in production, this MUST be encrypted)
            return secretKey;
        }
    }
}
```

### RequiresMfa Authorization Attribute

```csharp
// Attributes/RequiresMfaAttribute.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace IntelliFin.Admin.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class RequiresMfaAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public int TimeoutMinutes { get; set; } = 15;

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Check for amr (Authentication Methods Reference) claim
            var amrClaim = user.FindFirst("amr");
            var amrValues = amrClaim?.Value.Split(',').Select(v => v.Trim()).ToList() ?? new List<string>();

            // Check if MFA is present in amr claim
            if (!amrValues.Contains("mfa"))
            {
                // MFA not validated, return 401 with mfa_required error
                var operation = context.ActionDescriptor.DisplayName ?? "unknown";

                context.Result = new UnauthorizedObjectResult(new
                {
                    error = "mfa_required",
                    message = "This operation requires multi-factor authentication",
                    mfaChallengeUrl = "/api/admin/mfa/challenge",
                    operation = operation,
                    timeoutMinutes = TimeoutMinutes
                });

                return;
            }

            // Check MFA token expiration (iat claim)
            var iatClaim = user.FindFirst("iat");
            if (iatClaim != null && long.TryParse(iatClaim.Value, out var iat))
            {
                var issuedAt = DateTimeOffset.FromUnixTimeSeconds(iat);
                var expiresAt = issuedAt.AddMinutes(TimeoutMinutes);

                if (DateTimeOffset.UtcNow > expiresAt)
                {
                    context.Result = new UnauthorizedObjectResult(new
                    {
                        error = "mfa_expired",
                        message = "MFA validation has expired. Please re-authenticate.",
                        mfaChallengeUrl = "/api/admin/mfa/challenge"
                    });

                    return;
                }
            }

            // MFA validated and not expired - allow request
            await Task.CompletedTask;
        }
    }
}
```

### Camunda BPMN Process

```xml
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:zeebe="http://camunda.org/schema/zeebe/1.0"
                  id="step-up-mfa-challenge"
                  targetNamespace="http://intellifin.local/bpmn">
  
  <bpmn:process id="step-up-mfa-challenge" name="Step-Up MFA Challenge" isExecutable="true">
    
    <!-- Start Event -->
    <bpmn:startEvent id="StartEvent_MfaChallenge" name="MFA Challenge Initiated">
      <bpmn:outgoing>Flow_ToCheckEnrollment</bpmn:outgoing>
    </bpmn:startEvent>
    
    <!-- Check MFA Enrollment Service Task -->
    <bpmn:serviceTask id="Task_CheckEnrollment" name="Check MFA Enrollment">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="check-mfa-enrollment" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToCheckEnrollment</bpmn:incoming>
      <bpmn:outgoing>Flow_ToEnrollmentGateway</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Exclusive Gateway - Enrolled? -->
    <bpmn:exclusiveGateway id="Gateway_EnrollmentCheck" name="Enrolled?">
      <bpmn:incoming>Flow_ToEnrollmentGateway</bpmn:incoming>
      <bpmn:outgoing>Flow_ToChallenge</bpmn:outgoing>
      <bpmn:outgoing>Flow_ToEnrollmentRequired</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    
    <!-- User Task - MFA Validation -->
    <bpmn:userTask id="Task_MfaValidation" name="MFA Validation">
      <bpmn:extensionElements>
        <zeebe:assignmentDefinition assignee="${userId}" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToChallenge</bpmn:incoming>
      <bpmn:outgoing>Flow_ToValidationGateway</bpmn:outgoing>
    </bpmn:userTask>
    
    <!-- Exclusive Gateway - Validated? -->
    <bpmn:exclusiveGateway id="Gateway_ValidationResult" name="Validated?">
      <bpmn:incoming>Flow_ToValidationGateway</bpmn:incoming>
      <bpmn:outgoing>Flow_ToSuccess</bpmn:outgoing>
      <bpmn:outgoing>Flow_ToFailure</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    
    <!-- Service Task - Notify Success -->
    <bpmn:serviceTask id="Task_NotifySuccess" name="Notify Success">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="notify-mfa-success" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToSuccess</bpmn:incoming>
      <bpmn:outgoing>Flow_ToEndSuccess</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- Service Task - Notify Failure -->
    <bpmn:serviceTask id="Task_NotifyFailure" name="Notify Failure">
      <bpmn:extensionElements>
        <zeebe:taskDefinition type="notify-mfa-failure" />
      </bpmn:extensionElements>
      <bpmn:incoming>Flow_ToFailure</bpmn:incoming>
      <bpmn:incoming>Flow_ToTimeoutFailed</bpmn:incoming>
      <bpmn:outgoing>Flow_ToEndFailure</bpmn:outgoing>
    </bpmn:serviceTask>
    
    <!-- End Event - Enrollment Required -->
    <bpmn:endEvent id="EndEvent_EnrollmentRequired" name="Enrollment Required">
      <bpmn:incoming>Flow_ToEnrollmentRequired</bpmn:incoming>
    </bpmn:endEvent>
    
    <!-- End Events -->
    <bpmn:endEvent id="EndEvent_Success" name="MFA Validated">
      <bpmn:incoming>Flow_ToEndSuccess</bpmn:incoming>
    </bpmn:endEvent>
    
    <bpmn:endEvent id="EndEvent_Failure" name="MFA Failed">
      <bpmn:incoming>Flow_ToEndFailure</bpmn:incoming>
    </bpmn:endEvent>
    
    <!-- Sequence Flows -->
    <bpmn:sequenceFlow id="Flow_ToCheckEnrollment" sourceRef="StartEvent_MfaChallenge" targetRef="Task_CheckEnrollment" />
    <bpmn:sequenceFlow id="Flow_ToEnrollmentGateway" sourceRef="Task_CheckEnrollment" targetRef="Gateway_EnrollmentCheck" />
    
    <bpmn:sequenceFlow id="Flow_ToChallenge" sourceRef="Gateway_EnrollmentCheck" targetRef="Task_MfaValidation">
      <bpmn:conditionExpression>${enrolled == true}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    
    <bpmn:sequenceFlow id="Flow_ToEnrollmentRequired" sourceRef="Gateway_EnrollmentCheck" targetRef="EndEvent_EnrollmentRequired">
      <bpmn:conditionExpression>${enrolled == false}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    
    <bpmn:sequenceFlow id="Flow_ToValidationGateway" sourceRef="Task_MfaValidation" targetRef="Gateway_ValidationResult" />
    
    <bpmn:sequenceFlow id="Flow_ToSuccess" sourceRef="Gateway_ValidationResult" targetRef="Task_NotifySuccess">
      <bpmn:conditionExpression>${validated == true}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    
    <bpmn:sequenceFlow id="Flow_ToFailure" sourceRef="Gateway_ValidationResult" targetRef="Task_NotifyFailure">
      <bpmn:conditionExpression>${validated == false}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    
    <bpmn:sequenceFlow id="Flow_ToEndSuccess" sourceRef="Task_NotifySuccess" targetRef="EndEvent_Success" />
    <bpmn:sequenceFlow id="Flow_ToEndFailure" sourceRef="Task_NotifyFailure" targetRef="EndEvent_Failure" />
    
    <!-- Boundary Event - Timeout (5 minutes) -->
    <bpmn:boundaryEvent id="BoundaryEvent_Timeout" name="5min Timeout" attachedToRef="Task_MfaValidation">
      <bpmn:outgoing>Flow_ToTimeoutFailed</bpmn:outgoing>
      <bpmn:timerEventDefinition>
        <bpmn:timeDuration>PT5M</bpmn:timeDuration>
      </bpmn:timerEventDefinition>
    </bpmn:boundaryEvent>
    
    <bpmn:sequenceFlow id="Flow_ToTimeoutFailed" sourceRef="BoundaryEvent_Timeout" targetRef="Task_NotifyFailure" />
    
  </bpmn:process>
</bpmn:definitions>
```

### Frontend Integration (React)

```typescript
// components/MfaChallengeModal.tsx
import React, { useState } from 'react';
import { Modal, Input, Button, Alert } from 'antd';
import { SafetyOutlined } from '@ant-design/icons';

interface MfaChallengeModalProps {
  visible: boolean;
  challengeId: string;
  operation: string;
  onSuccess: (mfaToken: string) => void;
  onCancel: () => void;
}

export const MfaChallengeModal: React.FC<MfaChallengeModalProps> = ({
  visible,
  challengeId,
  operation,
  onSuccess,
  onCancel
}) => {
  const [otpCode, setOtpCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [remainingAttempts, setRemainingAttempts] = useState(3);

  const handleValidate = async () => {
    if (otpCode.length !== 6) {
      setError('Please enter a 6-digit code');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await fetch('/api/admin/mfa/validate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ challengeId, otpCode })
      });

      const data = await response.json();

      if (response.ok && data.success) {
        onSuccess(data.mfaToken);
      } else {
        setError(data.message || 'Invalid OTP code');
        setRemainingAttempts(data.remainingAttempts || 0);
        setOtpCode('');
      }
    } catch (err) {
      setError('Network error. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={
        <span>
          <SafetyOutlined /> Multi-Factor Authentication Required
        </span>
      }
      visible={visible}
      onCancel={onCancel}
      footer={[
        <Button key="cancel" onClick={onCancel}>
          Cancel
        </Button>,
        <Button
          key="validate"
          type="primary"
          loading={loading}
          onClick={handleValidate}
          disabled={otpCode.length !== 6}
        >
          Validate
        </Button>
      ]}
    >
      <p>Operation: <strong>{operation}</strong></p>
      <p>Enter the 6-digit code from your authenticator app:</p>

      <Input
        placeholder="000000"
        maxLength={6}
        value={otpCode}
        onChange={(e) => setOtpCode(e.target.value.replace(/\D/g, ''))}
        onPressEnter={handleValidate}
        size="large"
        style={{ fontSize: '24px', textAlign: 'center', letterSpacing: '8px' }}
        autoFocus
      />

      {error && (
        <Alert
          message={error}
          type="error"
          showIcon
          style={{ marginTop: 16 }}
          description={
            remainingAttempts > 0
              ? `${remainingAttempts} attempt(s) remaining`
              : 'Account locked. Please contact administrator.'
          }
        />
      )}
    </Modal>
  );
};
```

### Configuration

```json
// appsettings.json - Admin Service
{
  "MfaSettings": {
    "OtpLength": 6,
    "TimeStepSeconds": 30,
    "ChallengeTimeoutMinutes": 5,
    "MfaTokenTimeoutMinutes": 15,
    "MaxFailedAttempts": 3,
    "LockoutDurationMinutes": 30,
    "Issuer": "IntelliFin"
  },
  "RedisCache": {
    "ConnectionString": "${VAULT_REDIS_CONNECTION_STRING}",
    "InstanceName": "intellifin:"
  }
}
```

---

## Integration Verification

### IV1: Non-Sensitive Operations Continue Without MFA
**Verification Steps**:
1. Login as standard user (no MFA challenge yet)
2. Perform non-sensitive operations: view dashboard, list loans, search clients
3. Verify no MFA challenge triggered
4. Check audit log confirms no MFA events for these operations

**Success Criteria**:
- Normal workflows unaffected
- MFA only triggered for configured sensitive operations
- User experience smooth for day-to-day tasks

### IV2: MFA Enrollment Flow Tested
**Verification Steps**:
1. New user attempts sensitive operation (not yet enrolled)
2. System redirects to MFA enrollment page
3. QR code displayed, user scans with Google Authenticator
4. User enters initial OTP code
5. Enrollment confirmed, user redirected back to original operation
6. Original operation now requires MFA challenge (user is enrolled)

**Success Criteria**:
- QR code scans successfully in multiple authenticator apps
- Manual secret key entry works as alternative
- Enrollment status persisted in Keycloak and database
- Original operation resumes after enrollment

### IV3: MFA Failure Handling - User Lockout After 3 Failed Attempts
**Verification Steps**:
1. User triggers MFA challenge
2. Enter incorrect OTP 3 times
3. Verify lockout enforced (HTTP 429)
4. Attempt to retry MFA challenge - should be blocked
5. Wait 30 minutes or admin unlocks
6. Verify user can retry MFA challenge

**Success Criteria**:
- Lockout after exactly 3 failed attempts
- Clear error message with lockout duration
- Audit trail shows all failed attempts
- Admin can manually unlock user (via Keycloak)

---

## Testing Strategy

### Unit Tests

#### Test: RequiresMfaAttribute_MissingAmrClaim_ReturnsUnauthorized
```csharp
[Fact]
public async Task RequiresMfaAttribute_MissingAmrClaim_ReturnsUnauthorized()
{
    // Arrange
    var attribute = new RequiresMfaAttribute();
    var context = CreateAuthorizationContext(withAmrClaim: false);

    // Act
    await attribute.OnAuthorizationAsync(context);

    // Assert
    var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
    var value = result.Value as dynamic;
    Assert.Equal("mfa_required", value.error);
}
```

#### Test: MfaService_ValidateChallenge_InvalidOtp_IncrementsFailedAttempts
```csharp
[Fact]
public async Task MfaService_ValidateChallenge_InvalidOtp_IncrementsFailedAttempts()
{
    // Arrange
    var dbContext = CreateInMemoryDbContext();
    var keycloakMock = new Mock<IKeycloakAdminClient>();
    keycloakMock.Setup(k => k.ValidateOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    var service = new MfaService(dbContext, keycloakMock.Object, /* ... */);

    var challenge = new MfaChallenge
    {
        ChallengeId = Guid.NewGuid(),
        UserId = "user123",
        Status = "Initiated",
        ExpiresAt = DateTime.UtcNow.AddMinutes(5),
        FailedAttempts = 0
    };
    dbContext.MfaChallenges.Add(challenge);
    await dbContext.SaveChangesAsync();

    // Act
    var result = await service.ValidateChallengeAsync("user123", challenge.ChallengeId, "123456", CancellationToken.None);

    // Assert
    Assert.False(result.Success);
    var updatedChallenge = await dbContext.MfaChallenges.FindAsync(challenge.Id);
    Assert.Equal(1, updatedChallenge.FailedAttempts);
    Assert.Equal(2, result.RemainingAttempts);
}
```

### Integration Tests

#### Test: End-to-End MFA Flow
```csharp
[Fact]
public async Task MfaFlow_EnrollChallengeValidate_Success()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>();
    var client = factory.CreateClient();

    // Act 1: Generate enrollment
    var enrollResponse = await client.PostAsync("/api/admin/mfa/enroll", null);
    enrollResponse.EnsureSuccessStatusCode();
    var enrollment = await enrollResponse.Content.ReadFromJsonAsync<MfaEnrollmentResponse>();

    // Generate valid OTP from secret key
    var secretKeyBytes = Base32Encoding.ToBytes(enrollment.SecretKey);
    var totp = new Totp(secretKeyBytes);
    var otpCode = totp.ComputeTotp();

    // Act 2: Verify enrollment
    var verifyPayload = new { secretKey = enrollment.SecretKey, otpCode = otpCode };
    var verifyResponse = await client.PostAsJsonAsync("/api/admin/mfa/enroll/verify", verifyPayload);
    verifyResponse.EnsureSuccessStatusCode();

    // Act 3: Initiate MFA challenge
    var challengePayload = new { operation = "TestOperation" };
    var challengeResponse = await client.PostAsJsonAsync("/api/admin/mfa/challenge", challengePayload);
    var challenge = await challengeResponse.Content.ReadFromJsonAsync<MfaChallengeResponse>();

    // Act 4: Validate challenge with new OTP
    var newOtpCode = totp.ComputeTotp();
    var validatePayload = new { challengeId = challenge.ChallengeId, otpCode = newOtpCode };
    var validateResponse = await client.PostAsJsonAsync("/api/admin/mfa/validate", validatePayload);
    var validation = await validateResponse.Content.ReadFromJsonAsync<MfaValidationResponse>();

    // Assert
    Assert.True(validation.Success);
    Assert.NotNull(validation.MfaToken);
}
```

### Performance Tests

#### Test: MFA Challenge Latency
```csharp
[Fact]
public async Task MfaChallenge_InitiateAndValidate_CompletesQuickly()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>();
    var client = factory.CreateClient();
    var stopwatch = Stopwatch.StartNew();

    // Act
    var challengePayload = new { operation = "TestOperation" };
    var challengeResponse = await client.PostAsJsonAsync("/api/admin/mfa/challenge", challengePayload);
    var challenge = await challengeResponse.Content.ReadFromJsonAsync<MfaChallengeResponse>();

    var otpCode = "123456";  // Assume valid OTP
    var validatePayload = new { challengeId = challenge.ChallengeId, otpCode = otpCode };
    var validateResponse = await client.PostAsJsonAsync("/api/admin/mfa/validate", validatePayload);

    stopwatch.Stop();

    // Assert
    Assert.True(stopwatch.ElapsedMilliseconds < 3000, "MFA challenge should complete in <3 seconds");
}
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| Clock drift between server and authenticator | OTP validation fails | Medium | Use look-ahead window of ±1 step (±30 seconds). Document time sync requirements. |
| User loses authenticator device | Cannot perform sensitive operations | Medium | Implement backup codes or admin-assisted MFA reset procedure. Store recovery codes securely. |
| MFA fatigue (too many challenges) | User frustration, reduced productivity | High | Carefully configure which operations require MFA. Use 15-minute MFA token timeout to reduce re-challenges. |
| Keycloak OTP validation latency | Slow MFA challenges | Low | Cache OTP validation results (1-minute TTL). Implement retry logic with exponential backoff. |
| Bypass via JWT manipulation | Attacker forges amr claim | Low | Validate JWT signature. Use short-lived MFA tokens. Monitor for suspicious amr claim patterns. |

---

## Definition of Done

- [ ] Database schema created with indexes
- [ ] Keycloak OTP authenticator configured
- [ ] API endpoints implemented with full validation
- [ ] `[RequiresMfa]` attribute implemented and tested
- [ ] Camunda BPMN process deployed
- [ ] Frontend MFA modal component implemented
- [ ] QR code generation working (tested with Google Authenticator, Authy, Microsoft Authenticator)
- [ ] MFA enrollment flow tested end-to-end
- [ ] MFA challenge flow tested with valid/invalid OTP
- [ ] Lockout mechanism tested (3 failed attempts)
- [ ] Unit tests: >85% code coverage
- [ ] Integration tests: All workflows pass
- [ ] Performance test: MFA challenge <3 seconds
- [ ] Security review: JWT validation, OTP security
- [ ] Audit events logged for all MFA activities
- [ ] Admin UI configuration interface complete
- [ ] Documentation: MFA enrollment guide, troubleshooting runbook
- [ ] User training materials created (how to set up authenticator app)

---

## Related Documentation

### PRD References
- **Lines 1108-1131**: Story 1.20 detailed requirements
- **Lines 1079-1243**: Phase 4 (Governance & Workflows) overview
- **FR9**: Configurable MFA operations
- **NFR10**: Authentication security standards

### Architecture References
- **Section 4.2**: Keycloak Authentication & Authorization
- **Section 5**: Camunda Workflow Integration
- **Section 4.1**: Admin Service architecture

### External Documentation
- [RFC 6238 - TOTP Algorithm](https://datatracker.ietf.org/doc/html/rfc6238)
- [Keycloak OTP Policy](https://www.keycloak.org/docs/latest/server_admin/#otp-policies)
- [Google Authenticator QR Format](https://github.com/google/google-authenticator/wiki/Key-Uri-Format)
- [OtpNet Library](https://github.com/kspearrin/Otp.NET)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Verify Redis cache connectivity for challenge storage
- [ ] Test QR code generation library (QRCoder) in target environment
- [ ] Review Keycloak OTP authenticator configuration options
- [ ] Coordinate with Frontend team on modal UX design
- [ ] Determine MFA-required operations list with Product Owner
- [ ] Plan MFA enrollment communication strategy (email templates, user guides)
- [ ] Test authenticator app compatibility (Google Authenticator, Authy, Microsoft Authenticator, 1Password)

### Post-Implementation Handoff
- [ ] Demo MFA enrollment and challenge flows to stakeholders
- [ ] Train Support team on MFA troubleshooting (lost device, lockout recovery)
- [ ] Document MFA bypass procedure for emergencies (CEO approval required)
- [ ] Set up monitoring for MFA failure rates (alert if >10% failure rate)
- [ ] Create user FAQ document (common issues, how to reset MFA)
- [ ] Review MFA configuration with Compliance Officer
- [ ] Add MFA metrics to security dashboard (enrollment rate, challenge success rate)

### Technical Debt / Future Enhancements
- [ ] Implement backup codes for MFA recovery (generate 10 single-use codes)
- [ ] Add SMS-based MFA as alternative to OTP (using Twilio)
- [ ] Support WebAuthn/FIDO2 for hardware security keys
- [ ] Implement adaptive MFA (risk-based: location, device, time of day)
- [ ] Add MFA enrollment reminders (email users after 30 days if not enrolled)
- [ ] Create MFA analytics dashboard (enrollment trends, failure hotspots)

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: [Story 1.21: Expanded Operational Roles and SoD Enforcement](./story-1.21-expanded-roles-sod.md)
