namespace IntelliFin.IdentityService.Configuration;

public class RedisConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = "intellifin:identity:";
    public int Database { get; set; } = 0;
    public int SessionTimeoutMinutes { get; set; } = 30;
    public int RefreshTokenTimeoutDays { get; set; } = 7;
    public int TokenFamilyRetentionDays { get; set; } = 7;
    public int TokenDenylistTimeoutMinutes { get; set; } = 60;
    public bool AbortOnConnectFail { get; set; } = false;
    public int ConnectRetry { get; set; } = 3;
    public int ConnectTimeout { get; set; } = 5000;
    public int SyncTimeout { get; set; } = 5000;
}