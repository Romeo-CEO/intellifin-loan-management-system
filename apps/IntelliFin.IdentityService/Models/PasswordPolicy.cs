namespace IntelliFin.IdentityService.Models;

public class PasswordPolicy
{
    public int MinLength { get; set; } = 8;
    public int MaxLength { get; set; } = 128;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireSpecialChar { get; set; } = true;
    public int RequiredUniqueChars { get; set; } = 2;
    public int PasswordHistoryLimit { get; set; } = 5;
    public int MaxAge { get; set; } = 90; // days
    public string[] ForbiddenPatterns { get; set; } = Array.Empty<string>();
    public string[] CommonPasswords { get; set; } = Array.Empty<string>();
    
    public string Description => GenerateDescription();

    private string GenerateDescription()
    {
        var requirements = new List<string>
        {
            $"Must be between {MinLength} and {MaxLength} characters"
        };

        if (RequireUppercase) requirements.Add("Must contain at least one uppercase letter");
        if (RequireLowercase) requirements.Add("Must contain at least one lowercase letter");
        if (RequireDigit) requirements.Add("Must contain at least one digit");
        if (RequireSpecialChar) requirements.Add("Must contain at least one special character");
        if (RequiredUniqueChars > 1) requirements.Add($"Must contain at least {RequiredUniqueChars} unique characters");

        return string.Join(". ", requirements) + ".";
    }
}