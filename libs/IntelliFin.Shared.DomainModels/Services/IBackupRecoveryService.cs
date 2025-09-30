namespace IntelliFin.Shared.DomainModels.Services;

/// <summary>
/// Comprehensive backup and disaster recovery service for IntelliFin platform
/// </summary>
public interface IBackupRecoveryService
{
    // Backup Operations
    Task<BackupResult> CreateFullBackupAsync(BackupRequest request, CancellationToken cancellationToken = default);
    Task<BackupResult> CreateIncrementalBackupAsync(BackupRequest request, CancellationToken cancellationToken = default);
    Task<BackupResult> CreateDifferentialBackupAsync(BackupRequest request, CancellationToken cancellationToken = default);
    
    // Backup Management
    Task<List<BackupInfo>> GetBackupHistoryAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
    Task<BackupInfo?> GetBackupInfoAsync(string backupId, CancellationToken cancellationToken = default);
    Task<bool> DeleteBackupAsync(string backupId, CancellationToken cancellationToken = default);
    Task<BackupValidationResult> ValidateBackupAsync(string backupId, CancellationToken cancellationToken = default);
    
    // Recovery Operations
    Task<RecoveryResult> RestoreFromBackupAsync(RestoreRequest request, CancellationToken cancellationToken = default);
    Task<RecoveryResult> RestoreToPointInTimeAsync(PointInTimeRestoreRequest request, CancellationToken cancellationToken = default);
    Task<List<RecoveryPoint>> GetAvailableRecoveryPointsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    
    // Disaster Recovery
    Task<DisasterRecoveryResult> InitiateDisasterRecoveryAsync(DisasterRecoveryRequest request, CancellationToken cancellationToken = default);
    Task<DisasterRecoveryStatus> GetDisasterRecoveryStatusAsync(string recoveryId, CancellationToken cancellationToken = default);
    Task<bool> CancelDisasterRecoveryAsync(string recoveryId, CancellationToken cancellationToken = default);
    
    // Backup Scheduling
    Task<BackupSchedule> CreateBackupScheduleAsync(BackupScheduleRequest request, CancellationToken cancellationToken = default);
    Task<List<BackupSchedule>> GetBackupSchedulesAsync(CancellationToken cancellationToken = default);
    Task<bool> UpdateBackupScheduleAsync(string scheduleId, BackupScheduleRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteBackupScheduleAsync(string scheduleId, CancellationToken cancellationToken = default);
    Task<bool> EnableBackupScheduleAsync(string scheduleId, CancellationToken cancellationToken = default);
    Task<bool> DisableBackupScheduleAsync(string scheduleId, CancellationToken cancellationToken = default);
    
    // Cross-Region Replication
    Task<ReplicationResult> ReplicateBackupAsync(string backupId, string targetRegion, CancellationToken cancellationToken = default);
    Task<List<BackupReplica>> GetBackupReplicasAsync(string backupId, CancellationToken cancellationToken = default);
    Task<bool> DeleteBackupReplicaAsync(string replicaId, CancellationToken cancellationToken = default);
    
    // Backup Testing
    Task<BackupTestResult> TestBackupIntegrityAsync(string backupId, CancellationToken cancellationToken = default);
    Task<RecoveryTestResult> TestRecoveryProcedureAsync(RecoveryTestRequest request, CancellationToken cancellationToken = default);
    Task<List<BackupTestResult>> GetBackupTestHistoryAsync(CancellationToken cancellationToken = default);
    
    // Compliance and Reporting
    Task<BackupComplianceReport> GetBackupComplianceReportAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<RetentionPolicyReport> GetRetentionPolicyReportAsync(CancellationToken cancellationToken = default);
    Task<BackupMetrics> GetBackupMetricsAsync(TimeSpan timeWindow, CancellationToken cancellationToken = default);
    
    // Configuration
    Task<BackupConfiguration> GetBackupConfigurationAsync(CancellationToken cancellationToken = default);
    Task<bool> UpdateBackupConfigurationAsync(BackupConfiguration configuration, CancellationToken cancellationToken = default);
}

/// <summary>
/// Backup request information
/// </summary>
public class BackupRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BackupType Type { get; set; }
    public List<string> DatabaseNames { get; set; } = new();
    public List<string> FileSystemPaths { get; set; } = new();
    public BackupCompression CompressionLevel { get; set; } = BackupCompression.Standard;
    public bool EncryptBackup { get; set; } = true;
    public string? EncryptionKey { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
    public BackupPriority Priority { get; set; } = BackupPriority.Normal;
    public bool PerformConsistencyCheck { get; set; } = true;
}

/// <summary>
/// Backup operation result
/// </summary>
public class BackupResult
{
    public string BackupId { get; set; } = Guid.NewGuid().ToString();
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
    public long BackupSizeBytes { get; set; }
    public long CompressedSizeBytes { get; set; }
    public double CompressionRatio => BackupSizeBytes > 0 ? (double)CompressedSizeBytes / BackupSizeBytes : 0;
    public string BackupLocation { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public BackupStatus Status { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Backup information and metadata
/// </summary>
public class BackupInfo
{
    public string BackupId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BackupType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public long SizeBytes { get; set; }
    public BackupStatus Status { get; set; }
    public string Location { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; }
    public List<string> DatabaseNames { get; set; } = new();
    public List<string> FileSystemPaths { get; set; } = new();
    public string ChecksumMD5 { get; set; } = string.Empty;
    public string ChecksumSHA256 { get; set; } = string.Empty;
    public Dictionary<string, string> Tags { get; set; } = new();
    public List<BackupReplica> Replicas { get; set; } = new();
}

/// <summary>
/// Restore request information
/// </summary>
public class RestoreRequest
{
    public string BackupId { get; set; } = string.Empty;
    public string TargetLocation { get; set; } = string.Empty;
    public bool OverwriteExisting { get; set; } = false;
    public List<string> DatabaseNames { get; set; } = new();
    public List<string> FileSystemPaths { get; set; } = new();
    public RestoreMode Mode { get; set; } = RestoreMode.Replace;
    public bool VerifyIntegrity { get; set; } = true;
    public Dictionary<string, string> Options { get; set; } = new();
}

/// <summary>
/// Point-in-time restore request
/// </summary>
public class PointInTimeRestoreRequest
{
    public DateTime TargetDateTime { get; set; }
    public string TargetLocation { get; set; } = string.Empty;
    public List<string> DatabaseNames { get; set; } = new();
    public bool VerifyIntegrity { get; set; } = true;
    public RestoreMode Mode { get; set; } = RestoreMode.Replace;
}

/// <summary>
/// Recovery operation result
/// </summary>
public class RecoveryResult
{
    public string RecoveryId { get; set; } = Guid.NewGuid().ToString();
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
    public long DataRestoredBytes { get; set; }
    public List<string> RestoredDatabases { get; set; } = new();
    public List<string> RestoredFiles { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public RecoveryStatus Status { get; set; }
}

/// <summary>
/// Recovery point information
/// </summary>
public class RecoveryPoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime PointInTime { get; set; }
    public RecoveryPointType Type { get; set; }
    public string BackupId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public List<string> AvailableDatabases { get; set; } = new();
}

/// <summary>
/// Disaster recovery request
/// </summary>
public class DisasterRecoveryRequest
{
    public string Name { get; set; } = string.Empty;
    public DisasterRecoveryType Type { get; set; }
    public string SourceRegion { get; set; } = string.Empty;
    public string TargetRegion { get; set; } = string.Empty;
    public DateTime? PointInTime { get; set; }
    public List<string> CriticalSystems { get; set; } = new();
    public bool AutoFailover { get; set; } = false;
    public Dictionary<string, string> Options { get; set; } = new();
}

/// <summary>
/// Disaster recovery result
/// </summary>
public class DisasterRecoveryResult
{
    public string RecoveryId { get; set; } = Guid.NewGuid().ToString();
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
    public DisasterRecoveryStatusEnum Status { get; set; }
    public List<string> RecoveredSystems { get; set; } = new();
    public List<string> FailedSystems { get; set; } = new();
    public TimeSpan RecoveryTimeObjective { get; set; }
    public TimeSpan ActualRecoveryTime { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();
}

/// <summary>
/// Disaster recovery status
/// </summary>
public class DisasterRecoveryStatus
{
    public string RecoveryId { get; set; } = string.Empty;
    public DisasterRecoveryPhase CurrentPhase { get; set; }
    public int OverallProgressPercentage { get; set; }
    public Dictionary<string, int> SystemProgress { get; set; } = new();
    public List<string> CompletedSteps { get; set; } = new();
    public List<string> PendingSteps { get; set; } = new();
    public List<string> FailedSteps { get; set; } = new();
    public DateTime EstimatedCompletionTime { get; set; }
}

/// <summary>
/// Backup schedule configuration
/// </summary>
public class BackupSchedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BackupType Type { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public BackupRequest BackupTemplate { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public int RetentionDays { get; set; } = 30;
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Backup schedule request
/// </summary>
public class BackupScheduleRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BackupType Type { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public BackupRequest BackupTemplate { get; set; } = new();
    public int RetentionDays { get; set; } = 30;
}

/// <summary>
/// Cross-region replication result
/// </summary>
public class ReplicationResult
{
    public string ReplicationId { get; set; } = Guid.NewGuid().ToString();
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string SourceRegion { get; set; } = string.Empty;
    public string TargetRegion { get; set; } = string.Empty;
    public long DataTransferredBytes { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Backup replica information
/// </summary>
public class BackupReplica
{
    public string ReplicaId { get; set; } = Guid.NewGuid().ToString();
    public string BackupId { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
    public ReplicationStatus Status { get; set; }
    public string Location { get; set; } = string.Empty;
}

/// <summary>
/// Backup validation result
/// </summary>
public class BackupValidationResult
{
    public string BackupId { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public DateTime ValidatedAt { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
    public Dictionary<string, bool> ComponentValidation { get; set; } = new();
    public string ChecksumValidation { get; set; } = string.Empty;
}

/// <summary>
/// Backup test result
/// </summary>
public class BackupTestResult
{
    public string TestId { get; set; } = Guid.NewGuid().ToString();
    public string BackupId { get; set; } = string.Empty;
    public BackupTestType TestType { get; set; }
    public bool Success { get; set; }
    public DateTime TestedAt { get; set; }
    public TimeSpan TestDuration { get; set; }
    public List<string> TestResults { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public double IntegrityScore { get; set; }
}

/// <summary>
/// Recovery test request
/// </summary>
public class RecoveryTestRequest
{
    public string BackupId { get; set; } = string.Empty;
    public RecoveryTestType TestType { get; set; }
    public string TestEnvironment { get; set; } = string.Empty;
    public List<string> TestScenarios { get; set; } = new();
    public bool VerifyDataIntegrity { get; set; } = true;
    public bool PerformanceTest { get; set; } = false;
}

/// <summary>
/// Recovery test result
/// </summary>
public class RecoveryTestResult
{
    public string TestId { get; set; } = Guid.NewGuid().ToString();
    public string BackupId { get; set; } = string.Empty;
    public RecoveryTestType TestType { get; set; }
    public bool Success { get; set; }
    public DateTime TestedAt { get; set; }
    public TimeSpan RecoveryTime { get; set; }
    public TimeSpan TargetRto { get; set; }
    public bool RtoMet => RecoveryTime <= TargetRto;
    public List<RecoveryTestScenarioResult> ScenarioResults { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, double> PerformanceMetrics { get; set; } = new();
}

public class RecoveryTestScenarioResult
{
    public string Scenario { get; set; } = string.Empty;
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Issues { get; set; } = new();
}

/// <summary>
/// Backup compliance report
/// </summary>
public class BackupComplianceReport
{
    public DateTime ReportDate { get; set; } = DateTime.UtcNow;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int TotalBackupsScheduled { get; set; }
    public int TotalBackupsCompleted { get; set; }
    public int TotalBackupsFailed { get; set; }
    public double BackupSuccessRate => TotalBackupsScheduled > 0 ? 
        (double)TotalBackupsCompleted / TotalBackupsScheduled * 100 : 0;
    public List<ComplianceViolation> Violations { get; set; } = new();
    public Dictionary<string, object> ComplianceMetrics { get; set; } = new();
}

public class ComplianceViolation
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public ComplianceViolationSeverity Severity { get; set; }
    public string AffectedSystem { get; set; } = string.Empty;
}

/// <summary>
/// Retention policy report
/// </summary>
public class RetentionPolicyReport
{
    public DateTime ReportDate { get; set; } = DateTime.UtcNow;
    public int TotalBackups { get; set; }
    public int BackupsExpiringSoon { get; set; }
    public int ExpiredBackups { get; set; }
    public long TotalStorageUsed { get; set; }
    public long EstimatedStorageAfterCleanup { get; set; }
    public List<RetentionPolicyRule> ActivePolicies { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
}

public class RetentionPolicyRule
{
    public string Name { get; set; } = string.Empty;
    public BackupType AppliesTo { get; set; }
    public int RetentionDays { get; set; }
    public int BackupsAffected { get; set; }
}

/// <summary>
/// Backup metrics
/// </summary>
public class BackupMetrics
{
    public TimeSpan TimeWindow { get; set; }
    public int TotalBackupsAttempted { get; set; }
    public int SuccessfulBackups { get; set; }
    public int FailedBackups { get; set; }
    public double SuccessRate => TotalBackupsAttempted > 0 ? 
        (double)SuccessfulBackups / TotalBackupsAttempted * 100 : 0;
    public TimeSpan AverageBackupTime { get; set; }
    public long TotalDataBacked { get; set; }
    public double AverageCompressionRatio { get; set; }
    public Dictionary<BackupType, int> BackupsByType { get; set; } = new();
    public List<string> TopErrors { get; set; } = new();
}

/// <summary>
/// Backup configuration
/// </summary>
public class BackupConfiguration
{
    public int DefaultRetentionDays { get; set; } = 30;
    public BackupCompression DefaultCompression { get; set; } = BackupCompression.Standard;
    public bool DefaultEncryption { get; set; } = true;
    public int MaxConcurrentBackups { get; set; } = 3;
    public int BackupTimeoutMinutes { get; set; } = 240;
    public List<string> DefaultBackupLocations { get; set; } = new();
    public List<string> ReplicationRegions { get; set; } = new();
    public Dictionary<string, string> EncryptionSettings { get; set; } = new();
    public Dictionary<string, int> RetentionPolicies { get; set; } = new();
}

// Enums
public enum BackupType
{
    Full,
    Incremental,
    Differential,
    TransactionLog,
    FileSystem,
    Application
}

public enum BackupStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled,
    Expired,
    Archived
}

public enum BackupCompression
{
    None,
    Standard,
    High,
    Maximum
}

public enum BackupPriority
{
    Low,
    Normal,
    High,
    Critical
}

public enum RestoreMode
{
    Replace,
    Merge,
    SkipExisting
}

public enum RecoveryStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public enum RecoveryPointType
{
    FullBackup,
    IncrementalBackup,
    TransactionLogBackup,
    Checkpoint
}

public enum DisasterRecoveryType
{
    Failover,
    Failback,
    RegionalFailover,
    CompleteRecovery
}

public enum DisasterRecoveryPhase
{
    Planning,
    Preparation,
    Execution,
    Verification,
    Completion,
    Rollback
}

public enum DisasterRecoveryStatusEnum
{
    Unknown,
    Completed,
    PartiallyCompleted,
    Failed
}

public enum ReplicationStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Synchronized
}

public enum BackupTestType
{
    IntegrityCheck,
    RestoreTest,
    PerformanceTest,
    ComplianceTest
}

public enum RecoveryTestType
{
    FullRecovery,
    PartialRecovery,
    PointInTimeRecovery,
    DisasterRecovery
}

public enum ComplianceViolationSeverity
{
    Low,
    Medium,
    High,
    Critical
}