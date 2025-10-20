using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace IntelliFin.IdentityService.Services;

public class PasswordService : IPasswordService
{
    private readonly PasswordConfiguration _config;
    private readonly ILogger<PasswordService> _logger;

    // Common weak passwords - in production this would be loaded from a database or file
    private readonly HashSet<string> _commonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "123456", "password123", "admin", "qwerty", "letmein", "welcome",
        "monkey", "1234567890", "123456789", "12345678", "12345", "1234", "123",
        "password1", "admin123", "root", "toor", "pass", "test"
    };

    public PasswordService(IOptions<PasswordConfiguration> config, ILogger<PasswordService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public Task<string> HashPasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password, _config.SaltRounds);
            
            _logger.LogDebug("Password hashed successfully");
            return Task.FromResult(hashedPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hash password");
            throw;
        }
    }

    public Task<bool> VerifyPasswordAsync(string password, string hashedPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(password))
                return Task.FromResult(false);

            if (string.IsNullOrWhiteSpace(hashedPassword))
                return Task.FromResult(false);

            var isValid = BCrypt.Net.BCrypt.Verify(password, hashedPassword);
            
            _logger.LogDebug("Password verification completed: {IsValid}", isValid);
            return Task.FromResult(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify password");
            return Task.FromResult(false);
        }
    }

    public async Task<PasswordValidationResult> ValidatePasswordAsync(string password, string? username = null, CancellationToken cancellationToken = default)
    {
        var result = new PasswordValidationResult();

        if (string.IsNullOrWhiteSpace(password))
        {
            result.Errors.Add("Password is required");
            return result;
        }

        // Length validation
        if (password.Length < _config.MinLength)
            result.Errors.Add($"Password length must be at least {_config.MinLength} characters");

        if (password.Length > _config.MaxLength)
            result.Errors.Add($"Password length must not exceed {_config.MaxLength} characters");

        // Usability guidance: discourage extremely long passwords even if under max
        if (password.Length > 64 && password.Length <= _config.MaxLength)
            result.Errors.Add("Password length should not exceed 64 characters for usability");

        // Character requirements
        if (_config.RequireUppercase && !password.Any(char.IsUpper))
            result.Errors.Add("Password must contain at least one uppercase letter");

        if (_config.RequireLowercase && !password.Any(char.IsLower))
            result.Errors.Add("Password must contain at least one lowercase letter");

        if (_config.RequireDigit && !password.Any(char.IsDigit))
            result.Errors.Add("Password must contain at least one digit");

        if (_config.RequireSpecialChar && !password.Any(c => !char.IsLetterOrDigit(c)))
            result.Errors.Add("Password must contain at least one special character");

        // Unique characters requirement
        if (password.Distinct().Count() < _config.RequiredUniqueChars)
            result.Errors.Add($"Password must contain at least {_config.RequiredUniqueChars} unique characters");

        // Username similarity check
        if (!string.IsNullOrWhiteSpace(username) && password.Contains(username, StringComparison.OrdinalIgnoreCase))
            result.Errors.Add("Password must not contain the username");

        // Common password check
        if (await IsPasswordCompromisedAsync(password, cancellationToken))
            result.Errors.Add("Password is too common or has been compromised");

        // Note: Do not treat configured common passwords as substring patterns to avoid false positives.
        // Exact common password checks are handled by IsPasswordCompromisedAsync.

        result.IsValid = result.Errors.Count == 0;
        result.Strength = await CheckPasswordStrengthAsync(password, cancellationToken);

        return result;
    }

    public Task<bool> IsPasswordCompromisedAsync(string password, CancellationToken cancellationToken = default)
    {
        // Check against common passwords
        var isCompromised = _commonPasswords.Contains(password);

        // In production, this could also check against known breach databases
        // like HaveIBeenPwned API

        return Task.FromResult(isCompromised);
    }

    public Task<PasswordPolicy> GetPasswordPolicyAsync(CancellationToken cancellationToken = default)
    {
        var policy = new PasswordPolicy
        {
            MinLength = _config.MinLength,
            MaxLength = _config.MaxLength,
            RequireUppercase = _config.RequireUppercase,
            RequireLowercase = _config.RequireLowercase,
            RequireDigit = _config.RequireDigit,
            RequireSpecialChar = _config.RequireSpecialChar,
            RequiredUniqueChars = _config.RequiredUniqueChars,
            PasswordHistoryLimit = _config.PasswordHistoryLimit,
            MaxAge = _config.MaxAge,
            CommonPasswords = _config.CommonPasswords
        };

        return Task.FromResult(policy);
    }

    public Task<PasswordStrengthResult> CheckPasswordStrengthAsync(string password, CancellationToken cancellationToken = default)
    {
        var result = new PasswordStrengthResult();
        var score = 0;
        var suggestions = new List<string>();

        // Length scoring
        if (password.Length >= 12)
            score += 25;
        else if (password.Length >= 8)
            score += 15;
        else
            suggestions.Add("Use at least 12 characters for better security");

        // Character variety scoring
        var hasLower = password.Any(char.IsLower);
        var hasUpper = password.Any(char.IsUpper);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

        result.Requirements["HasLowercase"] = hasLower;
        result.Requirements["HasUppercase"] = hasUpper;
        result.Requirements["HasDigit"] = hasDigit;
        result.Requirements["HasSpecialChar"] = hasSpecial;

        var charTypeCount = new[] { hasLower, hasUpper, hasDigit, hasSpecial }.Count(x => x);
        score += charTypeCount * 10;

        if (!hasLower) suggestions.Add("Include lowercase letters");
        if (!hasUpper) suggestions.Add("Include uppercase letters");
        if (!hasDigit) suggestions.Add("Include numbers");
        if (!hasSpecial) suggestions.Add("Include special characters");

        // Unique character bonus
        var uniqueChars = password.Distinct().Count();
        score += Math.Min(uniqueChars * 2, 20);

        // Pattern penalties
        if (Regex.IsMatch(password, @"(.)\1{2,}"))
        {
            score -= 10;
            suggestions.Add("Avoid repeating characters");
        }

        if (Regex.IsMatch(password, @"(012|123|234|345|456|567|678|789|890|abc|bcd|cde|def)"))
        {
            score -= 15;
            suggestions.Add("Avoid sequential characters");
        }

        // Common password penalty
        if (_commonPasswords.Contains(password))
        {
            score -= 30;
            suggestions.Add("Avoid common passwords");
        }

        // Determine strength
        result.Score = Math.Max(0, Math.Min(100, score));
        result.Strength = result.Score switch
        {
            < 20 => PasswordStrength.VeryWeak,
            < 40 => PasswordStrength.Weak,
            < 60 => PasswordStrength.Fair,
            < 80 => PasswordStrength.Good,
            < 90 => PasswordStrength.Strong,
            _ => PasswordStrength.VeryStrong
        };

        result.Suggestions = suggestions;

        return Task.FromResult(result);
    }
}