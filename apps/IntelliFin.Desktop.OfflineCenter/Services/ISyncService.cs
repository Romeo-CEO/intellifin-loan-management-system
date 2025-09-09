namespace IntelliFin.Desktop.OfflineCenter.Services;

public interface ISyncService
{
    // Sync Status
    Task<bool> IsSyncInProgressAsync();
    Task<DateTime?> GetLastSyncTimeAsync();
    Task<bool> HasPendingSyncOperationsAsync();
    Task<SyncStatusResult> GetSyncStatusAsync();
    
    // Bidirectional Sync Operations
    Task<SyncResult> PerformFullSyncAsync();
    Task<SyncResult> PerformIncrementalSyncAsync();
    Task<SyncResult> PerformBidirectionalSyncAsync();
    
    // Individual Entity Sync (Enhanced)
    Task<EntitySyncResult> SyncLoansAsync(SyncDirection direction = SyncDirection.Bidirectional);
    Task<EntitySyncResult> SyncClientsAsync(SyncDirection direction = SyncDirection.Bidirectional);
    Task<EntitySyncResult> SyncPaymentsAsync(SyncDirection direction = SyncDirection.Bidirectional);
    Task<EntitySyncResult> SyncFinancialSummariesAsync(SyncDirection direction = SyncDirection.Bidirectional);
    Task<EntitySyncResult> SyncReportsAsync(SyncDirection direction = SyncDirection.Bidirectional);
    
    // Upload Operations
    Task<bool> UploadPendingChangesAsync();
    Task<bool> UploadOfflinePaymentsAsync();
    Task<bool> UploadOfflineReportsAsync();
    
    // Conflict Resolution
    Task<List<SyncConflict>> DetectConflictsAsync();
    Task<ConflictResolutionResult> ResolveConflictsAsync(List<SyncConflict> conflicts, ConflictResolutionStrategy strategy);
    Task<ConflictResolutionResult> ResolveConflictsAsync(ConflictResolutionStrategy strategy);
    
    // Data Integrity
    Task<DataIntegrityResult> ValidateDataIntegrityAsync();
    Task<bool> RepairDataIntegrityAsync();
    
    // Sync Events
    event EventHandler<SyncProgressEventArgs> SyncProgressChanged;
    event EventHandler<SyncCompletedEventArgs> SyncCompleted;
    event EventHandler<SyncErrorEventArgs> SyncError;
    event EventHandler<ConflictDetectedEventArgs> ConflictDetected;
    
    // Configuration
    Task SetAutoSyncEnabledAsync(bool enabled);
    Task<bool> GetAutoSyncEnabledAsync();
    Task SetSyncIntervalAsync(TimeSpan interval);
    Task<TimeSpan> GetSyncIntervalAsync();
    Task SetConflictResolutionStrategyAsync(Type entityType, ConflictResolutionStrategy strategy);
    Task<ConflictResolutionStrategy> GetConflictResolutionStrategyAsync(Type entityType);
}

public class SyncProgressEventArgs : EventArgs
{
    public string Operation { get; set; } = string.Empty;
    public int ProgressPercentage { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SyncCompletedEventArgs : EventArgs
{
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public int ItemsSynced { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SyncErrorEventArgs : EventArgs
{
    public string Operation { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}

public class ConflictDetectedEventArgs : EventArgs
{
    public List<SyncConflict> Conflicts { get; set; } = new();
    public string EntityType { get; set; } = string.Empty;
    public int ConflictCount { get; set; }
}

/// <summary>
/// Sync direction options
/// </summary>
public enum SyncDirection
{
    Download,    // Server to client only
    Upload,      // Client to server only
    Bidirectional // Both directions with conflict resolution
}

/// <summary>
/// Comprehensive sync result
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public int TotalEntitiesProcessed { get; set; }
    public int EntitiesDownloaded { get; set; }
    public int EntitiesUploaded { get; set; }
    public int ConflictsDetected { get; set; }
    public int ConflictsResolved { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<SyncConflict> UnresolvedConflicts { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();
    public DateTime SyncTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Entity-specific sync result
/// </summary>
public class EntitySyncResult
{
    public string EntityType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int EntitiesProcessed { get; set; }
    public int EntitiesDownloaded { get; set; }
    public int EntitiesUploaded { get; set; }
    public int ConflictsDetected { get; set; }
    public int ConflictsResolved { get; set; }
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Overall sync status
/// </summary>
public class SyncStatusResult
{
    public bool IsSyncInProgress { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public DateTime? LastSuccessfulSyncTime { get; set; }
    public bool HasPendingOperations { get; set; }
    public int PendingUploadCount { get; set; }
    public int PendingConflictCount { get; set; }
    public bool IsOnline { get; set; }
    public Dictionary<string, DateTime> LastEntitySyncTimes { get; set; } = new();
    public List<string> RecentErrors { get; set; } = new();
}
