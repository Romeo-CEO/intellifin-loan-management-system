using IntelliFin.Communications.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace IntelliFin.Communications.Services;

public class EmailSuppressionService : IEmailSuppressionService
{
    private readonly ILogger<EmailSuppressionService> _logger;
    private readonly ConcurrentDictionary<string, EmailSuppression> _suppressions;
    private readonly Timer _cleanupTimer;

    public EmailSuppressionService(ILogger<EmailSuppressionService> logger)
    {
        _logger = logger;
        _suppressions = new ConcurrentDictionary<string, EmailSuppression>();
        
        // Set up cleanup timer to run every hour
        _cleanupTimer = new Timer(CleanupExpiredSuppressions, null, 
            TimeSpan.FromHours(1), TimeSpan.FromHours(1));
            
        InitializeDefaultSuppressions();
    }

    public async Task<bool> IsSupressedAsync(string emailAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(emailAddress))
            {
                return false;
            }

            var normalizedEmail = NormalizeEmailAddress(emailAddress);
            
            await Task.CompletedTask; // Simulate async operation
            
            if (_suppressions.TryGetValue(normalizedEmail, out var suppression))
            {
                // Check if suppression has expired
                if (suppression.ExpiresAt.HasValue && suppression.ExpiresAt.Value <= DateTime.UtcNow)
                {
                    _logger.LogDebug("Suppression expired for {Email}, removing from list", normalizedEmail);
                    _suppressions.TryRemove(normalizedEmail, out _);
                    return false;
                }

                _logger.LogDebug("Email {Email} is suppressed due to {Reason}", normalizedEmail, suppression.Reason);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking suppression status for {Email}", emailAddress);
            // In case of error, err on the side of caution and don't suppress
            return false;
        }
    }

    public async Task SuppressAsync(string emailAddress, SuppressionReason reason, DateTime? expiresAt = null, string? notes = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(emailAddress))
            {
                throw new ArgumentException("Email address is required", nameof(emailAddress));
            }

            var normalizedEmail = NormalizeEmailAddress(emailAddress);
            
            var suppression = new EmailSuppression
            {
                Id = Guid.NewGuid().ToString(),
                EmailAddress = normalizedEmail,
                Reason = reason,
                SuppressedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                Notes = notes
            };

            _suppressions.AddOrUpdate(normalizedEmail, suppression, (key, existing) => 
            {
                // Update existing suppression
                existing.Reason = reason;
                existing.SuppressedAt = DateTime.UtcNow;
                existing.ExpiresAt = expiresAt;
                existing.Notes = notes;
                return existing;
            });

            await Task.CompletedTask; // Simulate async operation
            
            _logger.LogInformation("Email suppressed: {Email} for reason {Reason}, expires: {ExpiresAt}", 
                normalizedEmail, reason, expiresAt?.ToString() ?? "Never");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suppressing email address {Email}", emailAddress);
            throw;
        }
    }

    public async Task UnsuppressAsync(string emailAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(emailAddress))
            {
                throw new ArgumentException("Email address is required", nameof(emailAddress));
            }

            var normalizedEmail = NormalizeEmailAddress(emailAddress);
            
            if (_suppressions.TryRemove(normalizedEmail, out var removed))
            {
                _logger.LogInformation("Email unsuppressed: {Email} (was suppressed for {Reason})", 
                    normalizedEmail, removed.Reason);
            }
            else
            {
                _logger.LogWarning("Attempted to unsuppress email {Email} that was not suppressed", normalizedEmail);
            }

            await Task.CompletedTask; // Simulate async operation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsuppressing email address {Email}", emailAddress);
            throw;
        }
    }

    public async Task<IEnumerable<EmailSuppression>> GetSuppressionsAsync(int pageSize = 50, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            if (pageSize <= 0) pageSize = 50;
            if (pageNumber <= 0) pageNumber = 1;

            await Task.CompletedTask; // Simulate async operation
            
            var skip = (pageNumber - 1) * pageSize;
            
            return _suppressions.Values
                .OrderBy(s => s.SuppressedAt)
                .Skip(skip)
                .Take(pageSize)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving suppressions (page {PageNumber}, size {PageSize})", pageNumber, pageSize);
            throw;
        }
    }

    public async Task ProcessBounceAsync(EmailBounce bounce, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing bounce for {Email}: {BounceType} - {Reason}", 
                bounce.EmailAddress, bounce.BounceType, bounce.Reason);

            var reason = bounce.BounceType switch
            {
                BounceType.Hard => SuppressionReason.Bounce,
                BounceType.Complaint => SuppressionReason.Complaint,
                BounceType.Suppression => SuppressionReason.GlobalSuppression,
                _ => SuppressionReason.Bounce
            };

            DateTime? expiresAt = null;
            
            // Set expiration based on bounce type
            if (bounce.BounceType == BounceType.Soft)
            {
                // Soft bounces expire after 7 days
                expiresAt = DateTime.UtcNow.AddDays(7);
            }
            else if (bounce.BounceType == BounceType.Hard)
            {
                // Hard bounces expire after 30 days (to allow for corrections)
                expiresAt = DateTime.UtcNow.AddDays(30);
            }
            // Complaints and global suppressions don't expire automatically

            await SuppressAsync(
                bounce.EmailAddress, 
                reason, 
                expiresAt, 
                $"Auto-suppressed due to {bounce.BounceType} bounce: {bounce.Reason}", 
                cancellationToken);

            // Mark as suppression listed if it's a hard bounce or complaint
            if (bounce.BounceType == BounceType.Hard || bounce.BounceType == BounceType.Complaint)
            {
                bounce.IsSuppressionListed = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing bounce for {Email}", bounce.EmailAddress);
            throw;
        }
    }

    public async Task CleanupExpiredSuppressionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Starting expired suppressions cleanup");
            
            var expiredKeys = new List<string>();
            var now = DateTime.UtcNow;
            
            foreach (var kvp in _suppressions)
            {
                if (kvp.Value.ExpiresAt.HasValue && kvp.Value.ExpiresAt.Value <= now)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            var removedCount = 0;
            foreach (var key in expiredKeys)
            {
                if (_suppressions.TryRemove(key, out var removed))
                {
                    removedCount++;
                    _logger.LogDebug("Removed expired suppression for {Email} (reason: {Reason})", 
                        removed.EmailAddress, removed.Reason);
                }
            }

            await Task.CompletedTask; // Simulate async operation
            
            if (removedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired suppressions", removedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during suppressions cleanup");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }

    private string NormalizeEmailAddress(string emailAddress)
    {
        return emailAddress.Trim().ToLowerInvariant();
    }

    private void CleanupExpiredSuppressions(object? state)
    {
        try
        {
            _ = CleanupExpiredSuppressionsAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in periodic suppressions cleanup");
        }
    }

    private void InitializeDefaultSuppressions()
    {
        // Add some common test/invalid email patterns to suppress
        var defaultSuppressions = new[]
        {
            "test@test.com",
            "noreply@test.com",
            "invalid@invalid.com",
            "admin@localhost",
            "test@localhost",
            "test@example.com",
            "user@example.com",
            "noreply@example.com"
        };

        foreach (var email in defaultSuppressions)
        {
            var suppression = new EmailSuppression
            {
                Id = Guid.NewGuid().ToString(),
                EmailAddress = email,
                Reason = SuppressionReason.GlobalSuppression,
                SuppressedAt = DateTime.UtcNow,
                Notes = "Default test/invalid email suppression"
            };
            
            _suppressions.TryAdd(email, suppression);
        }

        _logger.LogInformation("Initialized {Count} default email suppressions", defaultSuppressions.Length);
    }

    // Additional helper methods for advanced suppression logic
    public async Task<bool> IsTemporarilySuppressedAsync(string emailAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedEmail = NormalizeEmailAddress(emailAddress);
            
            if (_suppressions.TryGetValue(normalizedEmail, out var suppression))
            {
                return suppression.ExpiresAt.HasValue && suppression.ExpiresAt.Value > DateTime.UtcNow;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking temporary suppression for {Email}", emailAddress);
            return false;
        }
    }

    public async Task<bool> IsPermanentlySuppressedAsync(string emailAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedEmail = NormalizeEmailAddress(emailAddress);
            
            if (_suppressions.TryGetValue(normalizedEmail, out var suppression))
            {
                return !suppression.ExpiresAt.HasValue || 
                       suppression.Reason == SuppressionReason.GlobalSuppression ||
                       suppression.Reason == SuppressionReason.Complaint;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permanent suppression for {Email}", emailAddress);
            return false;
        }
    }

    public async Task<EmailSuppression?> GetSuppressionAsync(string emailAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedEmail = NormalizeEmailAddress(emailAddress);
            await Task.CompletedTask; // Simulate async operation
            
            _suppressions.TryGetValue(normalizedEmail, out var suppression);
            return suppression;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting suppression details for {Email}", emailAddress);
            throw;
        }
    }

    public async Task<int> GetSuppressionCountAsync(SuppressionReason? reason = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.CompletedTask; // Simulate async operation
            
            if (reason.HasValue)
            {
                return _suppressions.Values.Count(s => s.Reason == reason.Value);
            }
            
            return _suppressions.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting suppression count");
            throw;
        }
    }
}