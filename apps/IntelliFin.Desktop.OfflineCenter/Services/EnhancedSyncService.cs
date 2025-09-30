using IntelliFin.Desktop.OfflineCenter.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace IntelliFin.Desktop.OfflineCenter.Services;

/// <summary>
/// Enhanced sync service with bidirectional synchronization and conflict resolution
/// </summary>
public class EnhancedSyncService : ISyncService
{
    private readonly IOfflineDataService _offlineDataService;
    private readonly IFinancialApiService _financialApiService;
    private readonly IConflictResolutionService _conflictResolutionService;
    private readonly ILogger<EnhancedSyncService> _logger;
    
    private bool _isSyncInProgress;
    private readonly ConcurrentDictionary<Type, ConflictResolutionStrategy> _conflictStrategies;
    private readonly SemaphoreSlim _syncSemaphore;

    // Events
    public event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;
    public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;
    public event EventHandler<SyncErrorEventArgs>? SyncError;
    public event EventHandler<ConflictDetectedEventArgs>? ConflictDetected;

    public EnhancedSyncService(
        IOfflineDataService offlineDataService, 
        IFinancialApiService financialApiService,
        IConflictResolutionService conflictResolutionService,
        ILogger<EnhancedSyncService> logger)
    {
        _offlineDataService = offlineDataService;
        _financialApiService = financialApiService;
        _conflictResolutionService = conflictResolutionService;
        _logger = logger;
        
        _conflictStrategies = new ConcurrentDictionary<Type, ConflictResolutionStrategy>();
        _syncSemaphore = new SemaphoreSlim(1, 1);
    }

    #region Sync Status Methods

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

    public async Task<SyncStatusResult> GetSyncStatusAsync()
    {
        var result = new SyncStatusResult
        {
            IsSyncInProgress = _isSyncInProgress,
            LastSyncTime = await GetLastSyncTimeAsync(),
            HasPendingOperations = await HasPendingSyncOperationsAsync(),
            IsOnline = await _financialApiService.CheckConnectivityAsync()
        };

        try
        {
            result.PendingUploadCount = await _offlineDataService.GetPendingUploadCountAsync();
            result.PendingConflictCount = await _offlineDataService.GetPendingConflictCountAsync();
            result.LastEntitySyncTimes = await _offlineDataService.GetLastEntitySyncTimesAsync();
            result.RecentErrors = await _offlineDataService.GetRecentSyncErrorsAsync();
            result.LastSuccessfulSyncTime = await _offlineDataService.GetLastSuccessfulSyncTimeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync status details");
        }

        return result;
    }

    #endregion

    #region Bidirectional Sync Operations

    public async Task<SyncResult> PerformFullSyncAsync()
    {
        return await PerformSyncAsync(SyncType.Full);
    }

    public async Task<SyncResult> PerformIncrementalSyncAsync()
    {
        return await PerformSyncAsync(SyncType.Incremental);
    }

    public async Task<SyncResult> PerformBidirectionalSyncAsync()
    {
        return await PerformSyncAsync(SyncType.Bidirectional);
    }

    private async Task<SyncResult> PerformSyncAsync(SyncType syncType)
    {
        if (!await _syncSemaphore.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            return new SyncResult 
            { 
                Success = false, 
                Errors = { "Another sync operation is already in progress" } 
            };
        }

        try
        {
            _isSyncInProgress = true;
            var startTime = DateTime.UtcNow;
            
            var result = new SyncResult { SyncTime = startTime };
            
            _logger.LogInformation("Starting {SyncType} sync operation", syncType);
            OnSyncProgressChanged("Sync", 0, $"Starting {syncType} sync");

            // Check connectivity
            if (!await _financialApiService.CheckConnectivityAsync())
            {
                result.Success = false;
                result.Errors.Add("No internet connectivity available");
                OnSyncError("Sync", "No internet connectivity available");
                return result;
            }

            // Step 1: Upload pending changes first
            OnSyncProgressChanged("Upload", 10, "Uploading pending changes");
            var uploadResult = await UploadPendingChangesAsync();
            if (!uploadResult)
            {
                result.Errors.Add("Failed to upload pending changes");
            }

            // Step 2: Download updates from server
            OnSyncProgressChanged("Download", 30, "Downloading updates");
            await SyncEntitiesAsync(result, SyncDirection.Download);

            // Step 3: Detect and resolve conflicts for bidirectional sync
            if (syncType == SyncType.Bidirectional || syncType == SyncType.Full)
            {
                OnSyncProgressChanged("Conflict Detection", 60, "Detecting conflicts");
                var conflicts = await DetectConflictsAsync();
                result.ConflictsDetected = conflicts.Count;

                if (conflicts.Any())
                {
                    OnConflictDetected(conflicts, "Multiple entity types");
                    
                    OnSyncProgressChanged("Conflict Resolution", 70, "Resolving conflicts");
                    var resolutionResult = await ResolveConflictsAsync(conflicts, ConflictResolutionStrategy.LastWriteWins);
                    result.ConflictsResolved = resolutionResult.ConflictsResolved;
                    result.UnresolvedConflicts = resolutionResult.UnresolvedConflicts;
                    result.Errors.AddRange(resolutionResult.Errors);
                }
            }

            // Step 4: Validate data integrity
            OnSyncProgressChanged("Validation", 90, "Validating data integrity");
            var integrityResult = await ValidateDataIntegrityAsync();
            if (!integrityResult.IsValid)
            {
                result.Errors.Add($"Data integrity issues: {string.Join(", ", integrityResult.IntegrityViolations)}");
            }

            // Finalize
            result.Duration = DateTime.UtcNow - startTime;
            result.Success = result.Errors.Count == 0;

            await _offlineDataService.UpdateLastSyncTimeAsync(result.Success ? startTime : null);

            OnSyncProgressChanged("Complete", 100, "Sync completed");
            OnSyncCompleted(result.Success, result.Duration, result.TotalEntitiesProcessed, 
                result.Success ? "Sync completed successfully" : "Sync completed with errors");

            _logger.LogInformation("Sync completed. Success: {Success}, Duration: {Duration}, Entities: {Entities}", 
                result.Success, result.Duration, result.TotalEntitiesProcessed);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync operation failed");
            OnSyncError("Sync", ex.Message, ex);
            
            return new SyncResult
            {
                Success = false,
                Errors = { ex.Message },
                Duration = DateTime.UtcNow - DateTime.UtcNow
            };
        }
        finally
        {
            _isSyncInProgress = false;
            _syncSemaphore.Release();
        }
    }

    private async Task SyncEntitiesAsync(SyncResult result, SyncDirection direction)
    {
        var entitySyncs = new List<Task<EntitySyncResult>>
        {
            SyncLoansAsync(direction),
            SyncClientsAsync(direction),
            SyncPaymentsAsync(direction),
            SyncFinancialSummariesAsync(direction),
            SyncReportsAsync(direction)
        };

        var entityResults = await Task.WhenAll(entitySyncs);

        foreach (var entityResult in entityResults)
        {
            result.TotalEntitiesProcessed += entityResult.EntitiesProcessed;
            result.EntitiesDownloaded += entityResult.EntitiesDownloaded;
            result.EntitiesUploaded += entityResult.EntitiesUploaded;
            result.ConflictsDetected += entityResult.ConflictsDetected;
            result.ConflictsResolved += entityResult.ConflictsResolved;
            result.Errors.AddRange(entityResult.Errors);
        }
    }

    #endregion

    #region Individual Entity Sync

    public async Task<EntitySyncResult> SyncLoansAsync(SyncDirection direction = SyncDirection.Bidirectional)
    {
        return await SyncEntityAsync<OfflineLoan>("Loan", direction, 
            () => _offlineDataService.GetPendingLoanChangesAsync(),
            (loans) => _financialApiService.GetLoansAsync(),
            (loans) => _financialApiService.UploadLoansAsync(loans),
            (loans) => _offlineDataService.SaveLoansAsync(loans));
    }

    public async Task<EntitySyncResult> SyncClientsAsync(SyncDirection direction = SyncDirection.Bidirectional)
    {
        return await SyncEntityAsync<OfflineClient>("Client", direction,
            () => _offlineDataService.GetPendingClientChangesAsync(),
            (clients) => _financialApiService.GetClientsAsync(),
            (clients) => _financialApiService.UploadClientsAsync(clients),
            (clients) => _offlineDataService.SaveClientsAsync(clients));
    }

    public async Task<EntitySyncResult> SyncPaymentsAsync(SyncDirection direction = SyncDirection.Bidirectional)
    {
        return await SyncEntityAsync<OfflinePayment>("Payment", direction,
            () => _offlineDataService.GetPendingPaymentChangesAsync(),
            (payments) => _financialApiService.GetPaymentsAsync(),
            (payments) => _financialApiService.UploadPaymentsAsync(payments),
            (payments) => _offlineDataService.SavePaymentsAsync(payments));
    }

    public async Task<EntitySyncResult> SyncFinancialSummariesAsync(SyncDirection direction = SyncDirection.Bidirectional)
    {
        return await SyncEntityAsync<OfflineFinancialSummary>("FinancialSummary", direction,
            () => _offlineDataService.GetPendingFinancialSummaryChangesAsync(),
            (summaries) => _financialApiService.GetFinancialSummariesAsync(),
            (summaries) => _financialApiService.UploadFinancialSummariesAsync(summaries),
            (summaries) => _offlineDataService.SaveFinancialSummariesAsync(summaries));
    }

    public async Task<EntitySyncResult> SyncReportsAsync(SyncDirection direction = SyncDirection.Bidirectional)
    {
        return await SyncEntityAsync<OfflineReport>("Report", direction,
            () => _offlineDataService.GetPendingReportChangesAsync(),
            (reports) => _financialApiService.GetReportsAsync(),
            (reports) => _financialApiService.UploadReportsAsync(reports),
            (reports) => _offlineDataService.SaveReportsAsync(reports));
    }

    private async Task<EntitySyncResult> SyncEntityAsync<T>(
        string entityType,
        SyncDirection direction,
        Func<Task<List<T>>> getPendingLocal,
        Func<Task<List<T>>> getRemote,
        Func<List<T>, Task<bool>> uploadToRemote,
        Func<List<T>, Task<bool>> saveLocal) where T : class
    {
        var result = new EntitySyncResult { EntityType = entityType };
        var startTime = DateTime.UtcNow;

        try
        {
            if (direction == SyncDirection.Upload || direction == SyncDirection.Bidirectional)
            {
                // Upload local changes
                var pendingLocal = await getPendingLocal();
                if (pendingLocal.Any())
                {
                    var uploadSuccess = await uploadToRemote(pendingLocal);
                    if (uploadSuccess)
                    {
                        result.EntitiesUploaded = pendingLocal.Count;
                    }
                    else
                    {
                        result.Errors.Add($"Failed to upload {entityType} entities");
                    }
                }
            }

            if (direction == SyncDirection.Download || direction == SyncDirection.Bidirectional)
            {
                // Download remote changes
                var remoteEntities = await getRemote();
                if (remoteEntities.Any())
                {
                    var saveSuccess = await saveLocal(remoteEntities);
                    if (saveSuccess)
                    {
                        result.EntitiesDownloaded = remoteEntities.Count;
                    }
                    else
                    {
                        result.Errors.Add($"Failed to save {entityType} entities");
                    }
                }
            }

            result.EntitiesProcessed = result.EntitiesDownloaded + result.EntitiesUploaded;
            result.Success = result.Errors.Count == 0;
            result.Duration = DateTime.UtcNow - startTime;

            await _offlineDataService.UpdateEntitySyncTimeAsync(entityType, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing {EntityType}", entityType);
            result.Errors.Add(ex.Message);
            result.Success = false;
        }

        return result;
    }

    #endregion

    #region Upload Operations

    public async Task<bool> UploadPendingChangesAsync()
    {
        try
        {
            var tasks = new List<Task<bool>>
            {
                UploadOfflinePaymentsAsync(),
                UploadOfflineReportsAsync(),
                UploadPendingEntityChangesAsync()
            };

            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading pending changes");
            return false;
        }
    }

    public async Task<bool> UploadOfflinePaymentsAsync()
    {
        try
        {
            var pendingPayments = await _offlineDataService.GetPendingPaymentChangesAsync();
            if (!pendingPayments.Any()) return true;

            return await _financialApiService.UploadPaymentsAsync(pendingPayments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading offline payments");
            return false;
        }
    }

    public async Task<bool> UploadOfflineReportsAsync()
    {
        try
        {
            var pendingReports = await _offlineDataService.GetPendingReportChangesAsync();
            if (!pendingReports.Any()) return true;

            return await _financialApiService.UploadReportsAsync(pendingReports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading offline reports");
            return false;
        }
    }

    private async Task<bool> UploadPendingEntityChangesAsync()
    {
        try
        {
            var tasks = new[]
            {
                _offlineDataService.GetPendingLoanChangesAsync().ContinueWith(t => 
                    t.IsCompletedSuccessfully && t.Result.Any() ? _financialApiService.UploadLoansAsync(t.Result) : Task.FromResult(true)),
                _offlineDataService.GetPendingClientChangesAsync().ContinueWith(t =>
                    t.IsCompletedSuccessfully && t.Result.Any() ? _financialApiService.UploadClientsAsync(t.Result) : Task.FromResult(true))
            };

            var results = await Task.WhenAll(tasks.Select(t => t.Unwrap()));
            return results.All(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading pending entity changes");
            return false;
        }
    }

    #endregion

    #region Conflict Resolution

    public async Task<List<SyncConflict>> DetectConflictsAsync()
    {
        var allConflicts = new List<SyncConflict>();

        try
        {
            // Detect conflicts for each entity type
            var conflictTasks = new List<Task<List<SyncConflict>>>
            {
                DetectEntityConflictsAsync<OfflineLoan>(l => l.LoanId),
                DetectEntityConflictsAsync<OfflineClient>(c => c.ClientId),
                DetectEntityConflictsAsync<OfflinePayment>(p => p.PaymentId)
            };

            var conflictResults = await Task.WhenAll(conflictTasks);
            foreach (var conflicts in conflictResults)
            {
                allConflicts.AddRange(conflicts);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting conflicts");
        }

        return allConflicts;
    }

    private async Task<List<SyncConflict>> DetectEntityConflictsAsync<T>(Func<T, string> keySelector) where T : class
    {
        try
        {
            var localEntities = await GetLocalEntitiesAsync<T>();
            var remoteEntities = await GetRemoteEntitiesAsync<T>();

            return await _conflictResolutionService.DetectConflictsAsync(localEntities, remoteEntities, keySelector);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting conflicts for {EntityType}", typeof(T).Name);
            return new List<SyncConflict>();
        }
    }

    public async Task<ConflictResolutionResult> ResolveConflictsAsync(List<SyncConflict> conflicts, ConflictResolutionStrategy strategy)
    {
        return await _conflictResolutionService.ResolveConflictsAsync(conflicts, strategy);
    }

    public async Task<ConflictResolutionResult> ResolveConflictsAsync(ConflictResolutionStrategy strategy)
    {
        var conflicts = await DetectConflictsAsync();
        return await ResolveConflictsAsync(conflicts, strategy);
    }

    #endregion

    #region Data Integrity

    public async Task<DataIntegrityResult> ValidateDataIntegrityAsync()
    {
        var overallResult = new DataIntegrityResult { IsValid = true };

        try
        {
            // Validate each entity type
            var validationTasks = new[]
            {
                ValidateEntityIntegrityAsync<OfflineLoan>(),
                ValidateEntityIntegrityAsync<OfflineClient>(),
                ValidateEntityIntegrityAsync<OfflinePayment>()
            };

            var validationResults = await Task.WhenAll(validationTasks);

            foreach (var result in validationResults)
            {
                if (!result.IsValid)
                {
                    overallResult.IsValid = false;
                    overallResult.IntegrityViolations.AddRange(result.IntegrityViolations);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating data integrity");
            overallResult.IsValid = false;
            overallResult.IntegrityViolations.Add($"Validation error: {ex.Message}");
        }

        return overallResult;
    }

    private async Task<DataIntegrityResult> ValidateEntityIntegrityAsync<T>() where T : class
    {
        var entities = await GetLocalEntitiesAsync<T>();
        return await _conflictResolutionService.ValidateIntegrityAsync(entities);
    }

    public async Task<bool> RepairDataIntegrityAsync()
    {
        try
        {
            var integrityResult = await ValidateDataIntegrityAsync();
            if (integrityResult.IsValid)
            {
                return true;
            }

            // Attempt to repair common integrity issues
            var repaired = await AttemptDataRepairAsync(integrityResult.IntegrityViolations);
            
            _logger.LogInformation("Data integrity repair completed. Success: {Success}", repaired);
            return repaired;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error repairing data integrity");
            return false;
        }
    }

    private async Task<bool> AttemptDataRepairAsync(List<string> violations)
    {
        // Basic repair logic - could be expanded based on specific violation types
        try
        {
            foreach (var violation in violations)
            {
                if (violation.Contains("Duplicate"))
                {
                    await RemoveDuplicateRecordsAsync();
                }
                else if (violation.Contains("null"))
                {
                    await RepairNullFieldsAsync();
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data repair");
            return false;
        }
    }

    private async Task RemoveDuplicateRecordsAsync()
    {
        // Implementation would depend on specific duplicate detection logic
        await _offlineDataService.RemoveDuplicateRecordsAsync();
    }

    private async Task RepairNullFieldsAsync()
    {
        // Implementation would depend on specific null field repair logic
        await _offlineDataService.RepairNullFieldsAsync();
    }

    #endregion

    #region Configuration

    public async Task SetAutoSyncEnabledAsync(bool enabled)
    {
        await _offlineDataService.SetSettingAsync("AutoSyncEnabled", enabled.ToString());
    }

    public async Task<bool> GetAutoSyncEnabledAsync()
    {
        var setting = await _offlineDataService.GetSettingAsync("AutoSyncEnabled");
        return bool.TryParse(setting, out var enabled) ? enabled : true;
    }

    public async Task SetSyncIntervalAsync(TimeSpan interval)
    {
        await _offlineDataService.SetSettingAsync("SyncInterval", interval.ToString());
    }

    public async Task<TimeSpan> GetSyncIntervalAsync()
    {
        var setting = await _offlineDataService.GetSettingAsync("SyncInterval");
        return TimeSpan.TryParse(setting, out var interval) ? interval : TimeSpan.FromMinutes(30);
    }

    public async Task SetConflictResolutionStrategyAsync(Type entityType, ConflictResolutionStrategy strategy)
    {
        _conflictStrategies.AddOrUpdate(entityType, strategy, (key, oldValue) => strategy);
        await _offlineDataService.SetSettingAsync($"ConflictStrategy_{entityType.Name}", strategy.ToString());
    }

    public async Task<ConflictResolutionStrategy> GetConflictResolutionStrategyAsync(Type entityType)
    {
        if (_conflictStrategies.TryGetValue(entityType, out var strategy))
        {
            return strategy;
        }

        var setting = await _offlineDataService.GetSettingAsync($"ConflictStrategy_{entityType.Name}");
        if (Enum.TryParse<ConflictResolutionStrategy>(setting, out strategy))
        {
            _conflictStrategies.TryAdd(entityType, strategy);
            return strategy;
        }

        return _conflictResolutionService.GetDefaultStrategy(entityType);
    }

    #endregion

    #region Helper Methods

    private async Task<List<T>> GetLocalEntitiesAsync<T>() where T : class
    {
        return typeof(T).Name switch
        {
            nameof(OfflineLoan) => (await _offlineDataService.GetLoansAsync()).Cast<T>().ToList(),
            nameof(OfflineClient) => (await _offlineDataService.GetClientsAsync()).Cast<T>().ToList(),
            nameof(OfflinePayment) => (await _offlineDataService.GetPaymentsAsync()).Cast<T>().ToList(),
            _ => new List<T>()
        };
    }

    private async Task<List<T>> GetRemoteEntitiesAsync<T>() where T : class
    {
        return typeof(T).Name switch
        {
            nameof(OfflineLoan) => (await _financialApiService.GetLoansAsync()).Cast<T>().ToList(),
            nameof(OfflineClient) => (await _financialApiService.GetClientsAsync()).Cast<T>().ToList(),
            nameof(OfflinePayment) => (await _financialApiService.GetPaymentsAsync()).Cast<T>().ToList(),
            _ => new List<T>()
        };
    }

    #endregion

    #region Event Handlers

    private void OnSyncProgressChanged(string operation, int percentage, string message)
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

    private void OnSyncError(string operation, string errorMessage, Exception? exception = null)
    {
        SyncError?.Invoke(this, new SyncErrorEventArgs
        {
            Operation = operation,
            ErrorMessage = errorMessage,
            Exception = exception
        });
    }

    private void OnConflictDetected(List<SyncConflict> conflicts, string entityType)
    {
        ConflictDetected?.Invoke(this, new ConflictDetectedEventArgs
        {
            Conflicts = conflicts,
            EntityType = entityType,
            ConflictCount = conflicts.Count
        });
    }

    #endregion
}

/// <summary>
/// Types of sync operations
/// </summary>
internal enum SyncType
{
    Full,
    Incremental,
    Bidirectional
}