namespace IntelliFin.IdentityService.Configuration;

public class AccountLockoutConfiguration
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutDurationMinutes { get; set; } = 15;
    public int AttemptsWindowMinutes { get; set; } = 15;
    public bool EnableLockout { get; set; } = true;
    public bool LockoutAllowedForNewUsers { get; set; } = true;
    public int ProgressiveLockoutMinutes { get; set; } = 30;
    public bool NotifyOnLockout { get; set; } = true;
    public bool RequireAdminUnlock { get; set; } = false;
    public int[] ProgressiveLockoutDurations { get; set; } = { 15, 30, 60, 120, 240 };
}