namespace IntelliFin.IdentityService.Configuration;

public class PasswordConfiguration
{
    public int MinLength { get; set; } = 8;
    public int MaxLength { get; set; } = 128;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireSpecialChar { get; set; } = true;
    public int RequiredUniqueChars { get; set; } = 2;
    public int SaltRounds { get; set; } = 12;
    public int PasswordHistoryLimit { get; set; } = 5;
    public int MaxAge { get; set; } = 90; // days
    public string[] CommonPasswords { get; set; } = Array.Empty<string>();
}