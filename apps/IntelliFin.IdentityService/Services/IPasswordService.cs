using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

public interface IPasswordService
{
    Task<string> HashPasswordAsync(string password, CancellationToken cancellationToken = default);
    Task<bool> VerifyPasswordAsync(string password, string hashedPassword, CancellationToken cancellationToken = default);
    Task<PasswordValidationResult> ValidatePasswordAsync(string password, string? username = null, CancellationToken cancellationToken = default);
    Task<bool> IsPasswordCompromisedAsync(string password, CancellationToken cancellationToken = default);
    Task<PasswordPolicy> GetPasswordPolicyAsync(CancellationToken cancellationToken = default);
    Task<PasswordStrengthResult> CheckPasswordStrengthAsync(string password, CancellationToken cancellationToken = default);
}

public class PasswordValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public PasswordStrengthResult Strength { get; set; } = new();
}

public class PasswordStrengthResult
{
    public PasswordStrength Strength { get; set; }
    public int Score { get; set; } // 0-100
    public List<string> Suggestions { get; set; } = new();
    public Dictionary<string, bool> Requirements { get; set; } = new();
}

public enum PasswordStrength
{
    VeryWeak = 0,
    Weak = 1,
    Fair = 2,
    Good = 3,
    Strong = 4,
    VeryStrong = 5
}