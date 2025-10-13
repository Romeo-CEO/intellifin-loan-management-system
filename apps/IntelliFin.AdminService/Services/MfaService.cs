using System.Security.Cryptography;
using System.Text.Json;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OtpNet;
using QRCoder;

namespace IntelliFin.AdminService.Services;

public sealed class MfaService : IMfaService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private const int OtpLength = 6;
    private const int TimeStepSeconds = 30;
    private const int ChallengeTimeoutMinutes = 5;
    private const int EnrollmentCacheTimeoutMinutes = 15;
    private const int MfaTokenTimeoutMinutes = 15;
    private const int MaxFailedAttempts = 3;

    private readonly AdminDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<MfaService> _logger;

    public MfaService(
        AdminDbContext dbContext,
        IAuditService auditService,
        IDistributedCache cache,
        ILogger<MfaService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<MfaChallengeResponse> InitiateChallengeAsync(
        string userId,
        string userName,
        string operation,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var enrollment = await _dbContext.MfaEnrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

        if (enrollment is null || !enrollment.Enrolled)
        {
            _logger.LogWarning("MFA challenge requested but user {UserId} is not enrolled", userId);
            throw new UserNotEnrolledException($"User '{userId}' is not enrolled in MFA.");
        }

        var correlationId = Guid.NewGuid().ToString("N");
        var challengeId = Guid.NewGuid();
        var challengeCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
        var expiresAt = DateTime.UtcNow.AddMinutes(ChallengeTimeoutMinutes);

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

        await _cache.SetStringAsync(
            $"mfa:challenge:{challengeId}",
            JsonSerializer.Serialize(new { challenge.UserId, challenge.Operation }, SerializerOptions),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ChallengeTimeoutMinutes)
            },
            cancellationToken);

        await LogAuditEventAsync(new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            Actor = userId,
            Action = "MfaChallengeInitiated",
            EntityType = nameof(MfaChallenge),
            EntityId = challengeId.ToString(),
            CorrelationId = correlationId,
            EventData = JsonSerializer.Serialize(new
            {
                operation,
                ipAddress,
                userAgent,
                expiresAt
            }, SerializerOptions)
        }, cancellationToken);

        _logger.LogInformation(
            "MFA challenge created for user {UserId} and operation {Operation} (ChallengeId: {ChallengeId})",
            userId,
            operation,
            challengeId);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(otpCode);

        var challenge = await _dbContext.MfaChallenges
            .FirstOrDefaultAsync(c => c.ChallengeId == challengeId, cancellationToken);

        if (challenge is null)
        {
            throw new ChallengeNotFoundException("Challenge not found or expired.");
        }

        if (!string.Equals(challenge.UserId, userId, StringComparison.Ordinal))
        {
            throw new UnauthorizedMfaException("Challenge does not belong to this user.");
        }

        if (!string.Equals(challenge.Status, "Initiated", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Challenge is not active (status: {challenge.Status}).");
        }

        if (challenge.ExpiresAt <= DateTime.UtcNow)
        {
            challenge.Status = "Timeout";
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new ChallengeNotFoundException("Challenge not found or expired.");
        }

        if (challenge.FailedAttempts >= MaxFailedAttempts)
        {
            challenge.Status = "Locked";
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new UserLockedException("User locked out due to failed MFA attempts.", DateTime.UtcNow.AddMinutes(30));
        }

        var enrollment = await _dbContext.MfaEnrollments
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

        if (enrollment is null || string.IsNullOrWhiteSpace(enrollment.SecretKey))
        {
            throw new InvalidOperationException("User enrollment not found.");
        }

        var secretKey = enrollment.SecretKey;
        var secretKeyBytes = Base32Encoding.ToBytes(secretKey);
        var totp = new Totp(secretKeyBytes, step: TimeStepSeconds, totpSize: OtpLength);
        var isValid = totp.VerifyTotp(otpCode, out _, new VerificationWindow(previous: 1, future: 1));

        if (!isValid)
        {
            challenge.FailedAttempts += 1;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await LogAuditEventAsync(new AuditEvent
            {
                Timestamp = DateTime.UtcNow,
                Actor = userId,
                Action = "MfaChallengeFailed",
                EntityType = nameof(MfaChallenge),
                EntityId = challengeId.ToString(),
                CorrelationId = challenge.CorrelationId,
                EventData = JsonSerializer.Serialize(new
                {
                    challenge.Operation,
                    challenge.FailedAttempts
                }, SerializerOptions)
            }, cancellationToken);

            _logger.LogWarning(
                "Invalid MFA code for challenge {ChallengeId}. Attempts: {Attempts}",
                challengeId,
                challenge.FailedAttempts);

            var remaining = Math.Max(0, MaxFailedAttempts - challenge.FailedAttempts);
            return new MfaValidationResponse
            {
                Success = false,
                FailedAttempts = challenge.FailedAttempts,
                RemainingAttempts = remaining,
                Message = "Invalid OTP code. Please try again."
            };
        }

        challenge.Status = "Succeeded";
        challenge.ValidatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (enrollment is not null)
        {
            enrollment.LastUsedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await _cache.RemoveAsync($"mfa:challenge:{challengeId}", cancellationToken);

        var mfaToken = Guid.NewGuid().ToString("N");
        var tokenExpiresAt = DateTime.UtcNow.AddMinutes(MfaTokenTimeoutMinutes);

        await _cache.SetStringAsync(
            $"mfa:token:{mfaToken}",
            JsonSerializer.Serialize(new
            {
                userId,
                challenge.Operation,
                expiresAt = tokenExpiresAt
            }, SerializerOptions),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(MfaTokenTimeoutMinutes)
            },
            cancellationToken);

        await LogAuditEventAsync(new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            Actor = userId,
            Action = "MfaChallengeSucceeded",
            EntityType = nameof(MfaChallenge),
            EntityId = challengeId.ToString(),
            CorrelationId = challenge.CorrelationId,
            EventData = JsonSerializer.Serialize(new
            {
                challenge.Operation,
                challenge.ValidatedAt
            }, SerializerOptions)
        }, cancellationToken);

        _logger.LogInformation(
            "MFA validation succeeded for challenge {ChallengeId}",
            challengeId);

        return new MfaValidationResponse
        {
            Success = true,
            MfaToken = mfaToken,
            ExpiresAt = tokenExpiresAt,
            FailedAttempts = challenge.FailedAttempts,
            RemainingAttempts = MaxFailedAttempts - challenge.FailedAttempts,
            Message = "MFA validation successful"
        };
    }

    public async Task<MfaEnrollmentResponse> GenerateEnrollmentAsync(
        string userId,
        string userName,
        string? userEmail,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        var existing = await _dbContext.MfaEnrollments
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

        if (existing is { Enrolled: true })
        {
            throw new InvalidOperationException("User is already enrolled in MFA.");
        }

        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secretKey = Base32Encoding.ToString(secretBytes);

        var issuer = "IntelliFin";
        var accountName = string.IsNullOrWhiteSpace(userEmail)
            ? userName
            : $"{userName} ({userEmail})";

        var otpAuthUri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}?secret={secretKey}&issuer={Uri.EscapeDataString(issuer)}&digits={OtpLength}&period={TimeStepSeconds}";

        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrBytes = qrCode.GetGraphic(10);
        var qrBase64 = Convert.ToBase64String(qrBytes);

        if (existing is null)
        {
            existing = new MfaEnrollment
            {
                UserId = userId,
                UserName = userName,
                Enrolled = false,
                SecretKey = secretKey
            };
            _dbContext.MfaEnrollments.Add(existing);
        }
        else
        {
            existing.SecretKey = secretKey;
            existing.Enrolled = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _cache.SetStringAsync(
            $"mfa:enrollment:{userId}",
            secretKey,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(EnrollmentCacheTimeoutMinutes)
            },
            cancellationToken);

        await LogAuditEventAsync(new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            Actor = userId,
            Action = "MfaEnrollmentInitiated",
            EntityType = nameof(MfaEnrollment),
            EntityId = userId,
            EventData = JsonSerializer.Serialize(new { issuer, accountName }, SerializerOptions)
        }, cancellationToken);

        _logger.LogInformation("Generated MFA enrollment for user {UserId}", userId);

        return new MfaEnrollmentResponse
        {
            QrCodeDataUri = $"data:image/png;base64,{qrBase64}",
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
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(otpCode);

        var cachedSecret = await _cache.GetStringAsync($"mfa:enrollment:{userId}", cancellationToken);
        if (string.IsNullOrWhiteSpace(cachedSecret) || !string.Equals(cachedSecret, secretKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Enrollment session expired or invalid.");
        }

        var secretKeyBytes = Base32Encoding.ToBytes(secretKey);
        var totp = new Totp(secretKeyBytes, step: TimeStepSeconds, totpSize: OtpLength);
        var isValid = totp.VerifyTotp(otpCode, out _, new VerificationWindow(previous: 1, future: 1));

        if (!isValid)
        {
            throw new InvalidOtpException("Invalid OTP code.");
        }

        var enrollment = await _dbContext.MfaEnrollments
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

        if (enrollment is null)
        {
            throw new InvalidOperationException("Enrollment not found.");
        }

        enrollment.Enrolled = true;
        enrollment.EnrolledAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync($"mfa:enrollment:{userId}", cancellationToken);

        await LogAuditEventAsync(new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            Actor = userId,
            Action = "MfaEnrolled",
            EntityType = nameof(MfaEnrollment),
            EntityId = userId,
            EventData = JsonSerializer.Serialize(new
            {
                enrollment.EnrolledAt
            }, SerializerOptions)
        }, cancellationToken);

        _logger.LogInformation("MFA enrollment completed for user {UserId}", userId);
    }

    public async Task<MfaEnrollmentStatusResponse> GetEnrollmentStatusAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var enrollment = await _dbContext.MfaEnrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

        return new MfaEnrollmentStatusResponse
        {
            Enrolled = enrollment?.Enrolled ?? false,
            EnrolledAt = enrollment?.EnrolledAt,
            LastUsedAt = enrollment?.LastUsedAt
        };
    }

    public async Task<IReadOnlyCollection<MfaConfigDto>> GetMfaConfigurationAsync(CancellationToken cancellationToken)
    {
        var configs = await _dbContext.MfaConfiguration
            .AsNoTracking()
            .OrderBy(c => c.OperationName)
            .Select(c => new MfaConfigDto
            {
                OperationName = c.OperationName,
                RequiresMfa = c.RequiresMfa,
                TimeoutMinutes = c.TimeoutMinutes,
                Description = c.Description
            })
            .ToListAsync(cancellationToken);

        return configs;
    }

    public async Task UpdateMfaConfigurationAsync(
        string operationName,
        MfaConfigUpdateDto update,
        string adminId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(update);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminId);

        var config = await _dbContext.MfaConfiguration
            .FirstOrDefaultAsync(c => c.OperationName == operationName, cancellationToken);

        if (config is null)
        {
            throw new NotFoundException($"MFA configuration for operation '{operationName}' not found.");
        }

        var previous = new { config.RequiresMfa, config.TimeoutMinutes };
        config.RequiresMfa = update.RequiresMfa;
        config.TimeoutMinutes = update.TimeoutMinutes;
        config.UpdatedBy = adminId;
        config.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditEventAsync(new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            Actor = adminId,
            Action = "MfaConfigurationUpdated",
            EntityType = nameof(MfaConfiguration),
            EntityId = operationName,
            EventData = JsonSerializer.Serialize(new
            {
                previous,
                updated = new { update.RequiresMfa, update.TimeoutMinutes }
            }, SerializerOptions)
        }, cancellationToken);

        _logger.LogInformation(
            "MFA configuration updated for operation {OperationName}",
            operationName);
    }

    private async Task LogAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        await _auditService.LogEventAsync(auditEvent, cancellationToken);
    }
}

public sealed class UserNotEnrolledException(string message) : Exception(message);

public sealed class ChallengeNotFoundException(string message) : Exception(message);

public sealed class InvalidOtpException(string message) : Exception(message);

public sealed class UnauthorizedMfaException(string message) : Exception(message);

public sealed class UserLockedException : Exception
{
    public UserLockedException(string message, DateTime? lockoutUntil)
        : base(message)
    {
        LockoutUntil = lockoutUntil;
    }

    public DateTime? LockoutUntil { get; }
}

public sealed class NotFoundException(string message) : Exception(message);
