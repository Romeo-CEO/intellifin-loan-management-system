using IntelliFin.Desktop.OfflineCenter.Models;

namespace IntelliFin.Desktop.OfflineCenter.Services;

public class SyncService : ISyncService
{
    private readonly IOfflineDataService _offlineDataService;
    private readonly IFinancialApiService _financialApiService;
    private bool _isSyncInProgress;

    public event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;
    public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;
    public event EventHandler<SyncErrorEventArgs>? SyncError;

    public SyncService(IOfflineDataService offlineDataService, IFinancialApiService financialApiService)
    {
        _offlineDataService = offlineDataService;
        _financialApiService = financialApiService;
    }

    public async Task<bool> IsSyncInProgressAsync()
    {
        return _isSyncInProgress;
    }

    public async Task<DateTime?> GetLastSyncTimeAsync()
    {
        return await _offlineDataService.GetLastSyncTimeAsync();
    }

    public async Task<bool> HasPendingSyncOperationsAsync()
    {
        return await _offlineDataService.HasPendingSyncOperationsAsync();
    }

    public async Task<bool> PerformFullSyncAsync()
    {
        if (_isSyncInProgress) return false;

        _isSyncInProgress = true;
        var startTime = DateTime.UtcNow;
        var totalItemsSynced = 0;

        try
        {
            // Check connectivity
            if (!await _financialApiService.CheckConnectivityAsync())
            {
                OnSyncError("Full Sync", "No internet connectivity available");
                return false;
            }

            OnSyncProgress("Full Sync", 0, "Starting full synchronization...");

            // Sync Loans
            OnSyncProgress("Loans", 20, "Synchronizing loans...");
            if (await SyncLoansAsync())
            {
                var loans = await _offlineDataService.GetLoansAsync();
                totalItemsSynced += loans.Count();
            }

            // Sync Clients
            OnSyncProgress("Clients", 40, "Synchronizing clients...");
            if (await SyncClientsAsync())
            {
                var clients = await _offlineDataService.GetClientsAsync();
                totalItemsSynced += clients.Count();
            }

            // Sync Payments
            OnSyncProgress("Payments", 60, "Synchronizing payments...");
            if (await SyncPaymentsAsync())
            {
                var payments = await _offlineDataService.GetPaymentsAsync();
                totalItemsSynced += payments.Count();
            }

            // Sync Financial Summaries
            OnSyncProgress("Financial Data", 80, "Synchronizing financial summaries...");
            await SyncFinancialSummariesAsync();

            // Upload pending changes
            OnSyncProgress("Upload", 90, "Uploading pending changes...");
            await UploadPendingChangesAsync();

            OnSyncProgress("Complete", 100, "Synchronization completed successfully");

            var duration = DateTime.UtcNow - startTime;
            OnSyncCompleted(true, duration, totalItemsSynced, "Full synchronization completed successfully");

            return true;
        }
        catch (Exception ex)
        {
            OnSyncError("Full Sync", ex.Message);
            var duration = DateTime.UtcNow - startTime;
            OnSyncCompleted(false, duration, totalItemsSynced, $"Synchronization failed: {ex.Message}");
            return false;
        }
        finally
        {
            _isSyncInProgress = false;
        }
    }

    public async Task<bool> PerformIncrementalSyncAsync()
    {
        if (_isSyncInProgress) return false;

        _isSyncInProgress = true;
        var startTime = DateTime.UtcNow;

        try
        {
            if (!await _financialApiService.CheckConnectivityAsync())
            {
                return false;
            }

            OnSyncProgress("Incremental Sync", 0, "Starting incremental synchronization...");

            // Only sync recent changes
            var lastSync = await GetLastSyncTimeAsync();
            if (lastSync.HasValue && lastSync.Value > DateTime.UtcNow.AddHours(-1))
            {
                // Recent sync, only upload pending changes
                OnSyncProgress("Upload", 50, "Uploading pending changes...");
                await UploadPendingChangesAsync();
            }
            else
            {
                // Perform limited sync
                await SyncFinancialSummariesAsync();
                await UploadPendingChangesAsync();
            }

            OnSyncProgress("Complete", 100, "Incremental synchronization completed");

            var duration = DateTime.UtcNow - startTime;
            OnSyncCompleted(true, duration, 0, "Incremental synchronization completed");

            return true;
        }
        catch (Exception ex)
        {
            OnSyncError("Incremental Sync", ex.Message);
            return false;
        }
        finally
        {
            _isSyncInProgress = false;
        }
    }

    public async Task<bool> SyncLoansAsync()
    {
        try
        {
            var loans = await _financialApiService.FetchLoansAsync();
            await _offlineDataService.SaveLoansAsync(loans);

            await _offlineDataService.LogSyncOperationAsync(new OfflineSyncLog
            {
                EntityType = "Loans",
                Operation = "Sync",
                Status = "Success",
                Timestamp = DateTime.UtcNow,
                SyncDirection = "Download"
            });

            return true;
        }
        catch (Exception ex)
        {
            await _offlineDataService.LogSyncOperationAsync(new OfflineSyncLog
            {
                EntityType = "Loans",
                Operation = "Sync",
                Status = "Failed",
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow,
                SyncDirection = "Download"
            });

            return false;
        }
    }

    public async Task<bool> SyncClientsAsync()
    {
        try
        {
            var clients = await _financialApiService.FetchClientsAsync();
            await _offlineDataService.SaveClientsAsync(clients);

            await _offlineDataService.LogSyncOperationAsync(new OfflineSyncLog
            {
                EntityType = "Clients",
                Operation = "Sync",
                Status = "Success",
                Timestamp = DateTime.UtcNow,
                SyncDirection = "Download"
            });

            return true;
        }
        catch (Exception ex)
        {
            await _offlineDataService.LogSyncOperationAsync(new OfflineSyncLog
            {
                EntityType = "Clients",
                Operation = "Sync",
                Status = "Failed",
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow,
                SyncDirection = "Download"
            });

            return false;
        }
    }

    public async Task<bool> SyncPaymentsAsync()
    {
        try
        {
            var payments = await _financialApiService.FetchPaymentsAsync();
            await _offlineDataService.SavePaymentsAsync(payments);

            await _offlineDataService.LogSyncOperationAsync(new OfflineSyncLog
            {
                EntityType = "Payments",
                Operation = "Sync",
                Status = "Success",
                Timestamp = DateTime.UtcNow,
                SyncDirection = "Download"
            });

            return true;
        }
        catch (Exception ex)
        {
            await _offlineDataService.LogSyncOperationAsync(new OfflineSyncLog
            {
                EntityType = "Payments",
                Operation = "Sync",
                Status = "Failed",
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow,
                SyncDirection = "Download"
            });

            return false;
        }
    }

    public async Task<bool> SyncFinancialSummariesAsync()
    {
        try
        {
            var summary = await _financialApiService.FetchFinancialSummaryAsync();
            summary.LastSyncDate = DateTime.UtcNow;
            summary.IsSynced = true;
            await _offlineDataService.SaveFinancialSummaryAsync(summary);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SyncReportsAsync()
    {
        // Implementation for syncing reports
        return true;
    }

    public async Task<bool> UploadPendingChangesAsync()
    {
        // Implementation for uploading offline changes
        return true;
    }

    public async Task<bool> UploadOfflinePaymentsAsync()
    {
        // Implementation for uploading offline payments
        return true;
    }

    public async Task<bool> UploadOfflineReportsAsync()
    {
        // Implementation for uploading offline reports
        return true;
    }

    public async Task SetAutoSyncEnabledAsync(bool enabled)
    {
        Preferences.Set("AutoSyncEnabled", enabled);
    }

    public async Task<bool> GetAutoSyncEnabledAsync()
    {
        return Preferences.Get("AutoSyncEnabled", true);
    }

    public async Task SetSyncIntervalAsync(TimeSpan interval)
    {
        Preferences.Set("SyncIntervalMinutes", (int)interval.TotalMinutes);
    }

    public async Task<TimeSpan> GetSyncIntervalAsync()
    {
        var minutes = Preferences.Get("SyncIntervalMinutes", 15);
        return TimeSpan.FromMinutes(minutes);
    }

    private void OnSyncProgress(string operation, int percentage, string message)
    {
        SyncProgressChanged?.Invoke(this, new SyncProgressEventArgs
        {
            Operation = operation,
            ProgressPercentage = percentage,
            Message = message
        });
    }

    private void OnSyncCompleted(bool success, TimeSpan duration, int itemsSynced, string message)
    {
        SyncCompleted?.Invoke(this, new SyncCompletedEventArgs
        {
            Success = success,
            Duration = duration,
            ItemsSynced = itemsSynced,
            Message = message
        });
    }

    private void OnSyncError(string operation, string errorMessage)
    {
        SyncError?.Invoke(this, new SyncErrorEventArgs
        {
            Operation = operation,
            ErrorMessage = errorMessage
        });
    }
}
