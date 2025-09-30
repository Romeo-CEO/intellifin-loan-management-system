namespace IntelliFin.Desktop.OfflineCenter.Services;

/// <summary>
/// Service for handling sync conflicts between offline and online data
/// </summary>
public interface IConflictResolutionService
{
    /// <summary>
    /// Detect conflicts between local and remote entities
    /// </summary>
    Task<List<SyncConflict>> DetectConflictsAsync<T>(
        List<T> localEntities, 
        List<T> remoteEntities, 
        Func<T, string> keySelector,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Resolve conflicts using specified strategy
    /// </summary>
    Task<ConflictResolutionResult> ResolveConflictsAsync(
        List<SyncConflict> conflicts, 
        ConflictResolutionStrategy strategy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get conflict resolution strategy for entity type
    /// </summary>
    ConflictResolutionStrategy GetDefaultStrategy(Type entityType);

    /// <summary>
    /// Validate data integrity after conflict resolution
    /// </summary>
    Task<DataIntegrityResult> ValidateIntegrityAsync<T>(
        List<T> resolvedEntities,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Create audit trail for conflict resolution
    /// </summary>
    Task LogConflictResolutionAsync(
        SyncConflict conflict, 
        ConflictResolutionAction action,
        string userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a sync conflict between local and remote data
/// </summary>
public class SyncConflict
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public object LocalEntity { get; set; } = new();
    public object RemoteEntity { get; set; } = new();
    public DateTime LocalLastModified { get; set; }
    public DateTime RemoteLastModified { get; set; }
    public ConflictType ConflictType { get; set; }
    public List<string> ConflictFields { get; set; } = new();
    public string ConflictDescription { get; set; } = string.Empty;
    public ConflictSeverity Severity { get; set; }
}

/// <summary>
/// Result of conflict resolution operation
/// </summary>
public class ConflictResolutionResult
{
    public bool Success { get; set; }
    public int ConflictsResolved { get; set; }
    public int ConflictsRemaining { get; set; }
    public List<object> ResolvedEntities { get; set; } = new();
    public List<SyncConflict> UnresolvedConflicts { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan ResolutionTime { get; set; }
}

/// <summary>
/// Data integrity validation result
/// </summary>
public class DataIntegrityResult
{
    public bool IsValid { get; set; }
    public List<string> IntegrityViolations { get; set; } = new();
    public Dictionary<string, object> ValidationMetrics { get; set; } = new();
    public DateTime ValidationTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Types of sync conflicts
/// </summary>
public enum ConflictType
{
    /// <summary>
    /// Entity modified in both local and remote
    /// </summary>
    ModifyModify,
    
    /// <summary>
    /// Entity modified locally but deleted remotely
    /// </summary>
    ModifyDelete,
    
    /// <summary>
    /// Entity deleted locally but modified remotely
    /// </summary>
    DeleteModify,
    
    /// <summary>
    /// Entity created with same ID in both locations
    /// </summary>
    CreateCreate,
    
    /// <summary>
    /// Data type or schema mismatch
    /// </summary>
    SchemaMismatch,
    
    /// <summary>
    /// Business rule violation
    /// </summary>
    BusinessRuleViolation
}

/// <summary>
/// Conflict resolution strategies
/// </summary>
public enum ConflictResolutionStrategy
{
    /// <summary>
    /// Always prefer local changes
    /// </summary>
    LocalWins,
    
    /// <summary>
    /// Always prefer remote changes
    /// </summary>
    RemoteWins,
    
    /// <summary>
    /// Prefer most recently modified
    /// </summary>
    LastWriteWins,
    
    /// <summary>
    /// Prefer first created
    /// </summary>
    FirstCreateWins,
    
    /// <summary>
    /// Merge compatible changes
    /// </summary>
    MergeChanges,
    
    /// <summary>
    /// Require manual resolution
    /// </summary>
    RequireManualResolution,
    
    /// <summary>
    /// Create duplicate with conflict marker
    /// </summary>
    CreateDuplicate,
    
    /// <summary>
    /// Apply business-specific rules
    /// </summary>
    BusinessRulesBased
}

/// <summary>
/// Conflict resolution actions taken
/// </summary>
public enum ConflictResolutionAction
{
    LocalAccepted,
    RemoteAccepted,
    Merged,
    DuplicateCreated,
    ManualResolutionRequired,
    BusinessRuleApplied,
    ConflictIgnored
}

/// <summary>
/// Severity levels for conflicts
/// </summary>
public enum ConflictSeverity
{
    Low,
    Medium,
    High,
    Critical
}