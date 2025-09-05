namespace IntelliFin.Desktop.OfflineCenter.Services;

public interface ISyncService
{
    // Sync Status
    Task<bool> IsSyncInProgressAsync();
    Task<DateTime?> GetLastSyncTimeAsync();
    Task<bool> HasPendingSyncOperationsAsync();
    
    // Full Sync
    Task<bool> PerformFullSyncAsync();
    Task<bool> PerformIncrementalSyncAsync();
    
    // Individual Entity Sync
    Task<bool> SyncLoansAsync();
    Task<bool> SyncClientsAsync();
    Task<bool> SyncPaymentsAsync();
    Task<bool> SyncFinancialSummariesAsync();
    Task<bool> SyncReportsAsync();
    
    // Upload Operations
    Task<bool> UploadPendingChangesAsync();
    Task<bool> UploadOfflinePaymentsAsync();
    Task<bool> UploadOfflineReportsAsync();
    
    // Sync Events
    event EventHandler<SyncProgressEventArgs> SyncProgressChanged;
    event EventHandler<SyncCompletedEventArgs> SyncCompleted;
    event EventHandler<SyncErrorEventArgs> SyncError;
    
    // Configuration
    Task SetAutoSyncEnabledAsync(bool enabled);
    Task<bool> GetAutoSyncEnabledAsync();
    Task SetSyncIntervalAsync(TimeSpan interval);
    Task<TimeSpan> GetSyncIntervalAsync();
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
