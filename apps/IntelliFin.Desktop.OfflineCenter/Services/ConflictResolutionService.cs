using System.Text.Json;
using System.Reflection;
using Microsoft.Extensions.Logging;
using IntelliFin.Shared.DomainModels.Services;

namespace IntelliFin.Desktop.OfflineCenter.Services;

/// <summary>
/// Service for handling sync conflicts between offline and online data
/// </summary>
public class ConflictResolutionService : IConflictResolutionService
{
    private readonly ILogger<ConflictResolutionService> _logger;
    private readonly IAuditService? _auditService;
    
    private readonly Dictionary<Type, ConflictResolutionStrategy> _defaultStrategies;

    public ConflictResolutionService(ILogger<ConflictResolutionService> logger, IAuditService? auditService = null)
    {
        _logger = logger;
        _auditService = auditService;
        
        // Define default resolution strategies for different entity types
        _defaultStrategies = new Dictionary<Type, ConflictResolutionStrategy>
        {
            // Critical financial data - require manual resolution
            [typeof(decimal)] = ConflictResolutionStrategy.RequireManualResolution,
            
            // Client data - last write wins with audit
            [typeof(object)] = ConflictResolutionStrategy.LastWriteWins
        };
    }

    public async Task<List<SyncConflict>> DetectConflictsAsync<T>(
        List<T> localEntities, 
        List<T> remoteEntities, 
        Func<T, string> keySelector,
        CancellationToken cancellationToken = default) where T : class
    {
        _logger.LogInformation("Starting conflict detection for {EntityType}", typeof(T).Name);
        
        var conflicts = new List<SyncConflict>();
        var localDict = localEntities.ToDictionary(keySelector, entity => entity);
        var remoteDict = remoteEntities.ToDictionary(keySelector, entity => entity);
        
        // Find modify-modify conflicts
        foreach (var kvp in localDict)
        {
            var key = kvp.Key;
            var localEntity = kvp.Value;
            
            if (remoteDict.TryGetValue(key, out var remoteEntity))
            {
                var conflict = await DetectEntityConflictAsync(key, localEntity, remoteEntity, cancellationToken);
                if (conflict != null)
                {
                    conflicts.Add(conflict);
                }
            }
        }
        
        // Find modify-delete conflicts (local exists, remote doesn't)
        foreach (var kvp in localDict)
        {
            if (!remoteDict.ContainsKey(kvp.Key))
            {
                var conflict = CreateModifyDeleteConflict(kvp.Key, kvp.Value);
                conflicts.Add(conflict);
            }
        }
        
        // Find delete-modify conflicts (remote exists, local doesn't)  
        foreach (var kvp in remoteDict)
        {
            if (!localDict.ContainsKey(kvp.Key))
            {
                var conflict = CreateDeleteModifyConflict(kvp.Key, kvp.Value);
                conflicts.Add(conflict);
            }
        }
        
        _logger.LogInformation("Detected {ConflictCount} conflicts for {EntityType}", 
            conflicts.Count, typeof(T).Name);
        
        return conflicts;
    }

    public async Task<ConflictResolutionResult> ResolveConflictsAsync(
        List<SyncConflict> conflicts, 
        ConflictResolutionStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resolving {ConflictCount} conflicts using {Strategy}", 
            conflicts.Count, strategy);
        
        var startTime = DateTime.UtcNow;
        var result = new ConflictResolutionResult
        {
            Success = true,
            ConflictsResolved = 0,
            ConflictsRemaining = conflicts.Count
        };
        
        foreach (var conflict in conflicts)
        {
            try
            {
                var resolution = await ResolveIndividualConflictAsync(conflict, strategy, cancellationToken);
                
                if (resolution.Success)
                {
                    result.ConflictsResolved++;
                    result.ConflictsRemaining--;
                    result.ResolvedEntities.Add(resolution.ResolvedEntity);
                    
                    await LogConflictResolutionAsync(conflict, resolution.Action, "system", cancellationToken);
                }
                else
                {
                    result.UnresolvedConflicts.Add(conflict);
                    result.Errors.Add($"Failed to resolve conflict for {conflict.EntityId}: {resolution.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving conflict for {EntityId}", conflict.EntityId);
                result.UnresolvedConflicts.Add(conflict);
                result.Errors.Add($"Exception resolving {conflict.EntityId}: {ex.Message}");
            }
        }
        
        result.Success = result.ConflictsRemaining == 0;
        result.ResolutionTime = DateTime.UtcNow - startTime;
        
        _logger.LogInformation("Resolved {Resolved}/{Total} conflicts in {Duration}ms", 
            result.ConflictsResolved, conflicts.Count, result.ResolutionTime.TotalMilliseconds);
        
        return result;
    }

    public ConflictResolutionStrategy GetDefaultStrategy(Type entityType)
    {
        // Check for exact type match first
        if (_defaultStrategies.TryGetValue(entityType, out var strategy))
        {
            return strategy;
        }
        
        // Check for inheritance-based matches
        foreach (var kvp in _defaultStrategies)
        {
            if (kvp.Key.IsAssignableFrom(entityType))
            {
                return kvp.Value;
            }
        }
        
        // Default to last write wins for unknown types
        return ConflictResolutionStrategy.LastWriteWins;
    }

    public async Task<DataIntegrityResult> ValidateIntegrityAsync<T>(
        List<T> resolvedEntities,
        CancellationToken cancellationToken = default) where T : class
    {
        _logger.LogInformation("Validating data integrity for {Count} {EntityType} entities", 
            resolvedEntities.Count, typeof(T).Name);
        
        var result = new DataIntegrityResult
        {
            IsValid = true,
            ValidationTime = DateTime.UtcNow
        };
        
        // Basic validation checks
        var duplicateIds = FindDuplicateIds(resolvedEntities);
        if (duplicateIds.Any())
        {
            result.IsValid = false;
            result.IntegrityViolations.AddRange(
                duplicateIds.Select(id => $"Duplicate ID found: {id}")
            );
        }
        
        // Entity-specific validation
        foreach (var entity in resolvedEntities)
        {
            var entityValidation = await ValidateEntityAsync(entity, cancellationToken);
            if (!entityValidation.IsValid)
            {
                result.IsValid = false;
                result.IntegrityViolations.AddRange(entityValidation.Violations);
            }
        }
        
        // Add validation metrics
        result.ValidationMetrics["TotalEntities"] = resolvedEntities.Count;
        result.ValidationMetrics["DuplicateIds"] = duplicateIds.Count;
        result.ValidationMetrics["IntegrityViolations"] = result.IntegrityViolations.Count;
        
        _logger.LogInformation("Data integrity validation completed. Valid: {IsValid}, Violations: {ViolationCount}", 
            result.IsValid, result.IntegrityViolations.Count);
        
        return result;
    }

    public async Task LogConflictResolutionAsync(
        SyncConflict conflict, 
        ConflictResolutionAction action,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (_auditService != null)
        {
            try
            {
                await _auditService.LogEventAsync(
                    userId, 
                    "CONFLICT_RESOLVED", 
                    conflict.EntityType, 
                    conflict.EntityId,
                    new
                    {
                        ConflictType = conflict.ConflictType.ToString(),
                        ResolutionAction = action.ToString(),
                        Severity = conflict.Severity.ToString(),
                        ConflictFields = conflict.ConflictFields,
                        Description = conflict.ConflictDescription
                    },
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log conflict resolution audit event");
            }
        }
        
        _logger.LogInformation("Conflict resolved: {EntityType}:{EntityId} using {Action}", 
            conflict.EntityType, conflict.EntityId, action);
    }

    private async Task<SyncConflict?> DetectEntityConflictAsync<T>(
        string key, 
        T localEntity, 
        T remoteEntity,
        CancellationToken cancellationToken) where T : class
    {
        // Compare entities for differences
        var localJson = JsonSerializer.Serialize(localEntity);
        var remoteJson = JsonSerializer.Serialize(remoteEntity);
        
        if (localJson == remoteJson)
        {
            return null; // No conflict
        }
        
        var conflictFields = FindDifferentFields(localEntity, remoteEntity);
        
        var conflict = new SyncConflict
        {
            EntityType = typeof(T).Name,
            EntityId = key,
            LocalEntity = localEntity,
            RemoteEntity = remoteEntity,
            ConflictType = ConflictType.ModifyModify,
            ConflictFields = conflictFields,
            ConflictDescription = $"Entity {key} modified in both local and remote systems",
            Severity = DetermineSeverity(conflictFields)
        };
        
        // Try to get modification times if entities support it
        conflict.LocalLastModified = GetLastModifiedTime(localEntity);
        conflict.RemoteLastModified = GetLastModifiedTime(remoteEntity);
        
        return conflict;
    }

    private SyncConflict CreateModifyDeleteConflict<T>(string key, T localEntity) where T : class
    {
        return new SyncConflict
        {
            EntityType = typeof(T).Name,
            EntityId = key,
            LocalEntity = localEntity,
            RemoteEntity = null!,
            ConflictType = ConflictType.ModifyDelete,
            ConflictDescription = $"Entity {key} exists locally but was deleted remotely",
            Severity = ConflictSeverity.High
        };
    }

    private SyncConflict CreateDeleteModifyConflict<T>(string key, T remoteEntity) where T : class
    {
        return new SyncConflict
        {
            EntityType = typeof(T).Name,
            EntityId = key,
            LocalEntity = null!,
            RemoteEntity = remoteEntity,
            ConflictType = ConflictType.DeleteModify,
            ConflictDescription = $"Entity {key} was deleted locally but modified remotely",
            Severity = ConflictSeverity.High
        };
    }

    private async Task<ConflictResolutionResult.IndividualResolution> ResolveIndividualConflictAsync(
        SyncConflict conflict, 
        ConflictResolutionStrategy strategy,
        CancellationToken cancellationToken)
    {
        switch (strategy)
        {
            case ConflictResolutionStrategy.LocalWins:
                return new ConflictResolutionResult.IndividualResolution
                {
                    Success = true,
                    ResolvedEntity = conflict.LocalEntity,
                    Action = ConflictResolutionAction.LocalAccepted
                };
                
            case ConflictResolutionStrategy.RemoteWins:
                return new ConflictResolutionResult.IndividualResolution
                {
                    Success = true,
                    ResolvedEntity = conflict.RemoteEntity,
                    Action = ConflictResolutionAction.RemoteAccepted
                };
                
            case ConflictResolutionStrategy.LastWriteWins:
                return ResolveLastWriteWins(conflict);
                
            case ConflictResolutionStrategy.MergeChanges:
                return await MergeChangesAsync(conflict, cancellationToken);
                
            case ConflictResolutionStrategy.RequireManualResolution:
                return new ConflictResolutionResult.IndividualResolution
                {
                    Success = false,
                    ErrorMessage = "Manual resolution required",
                    Action = ConflictResolutionAction.ManualResolutionRequired
                };
                
            default:
                return new ConflictResolutionResult.IndividualResolution
                {
                    Success = false,
                    ErrorMessage = $"Unknown resolution strategy: {strategy}"
                };
        }
    }

    private ConflictResolutionResult.IndividualResolution ResolveLastWriteWins(SyncConflict conflict)
    {
        var useLocal = conflict.LocalLastModified >= conflict.RemoteLastModified;
        
        return new ConflictResolutionResult.IndividualResolution
        {
            Success = true,
            ResolvedEntity = useLocal ? conflict.LocalEntity : conflict.RemoteEntity,
            Action = useLocal ? ConflictResolutionAction.LocalAccepted : ConflictResolutionAction.RemoteAccepted
        };
    }

    private async Task<ConflictResolutionResult.IndividualResolution> MergeChangesAsync(
        SyncConflict conflict, 
        CancellationToken cancellationToken)
    {
        // Basic field-level merge - in practice, this would need entity-specific logic
        try
        {
            var mergedEntity = PerformFieldLevelMerge(conflict.LocalEntity, conflict.RemoteEntity);
            
            return new ConflictResolutionResult.IndividualResolution
            {
                Success = true,
                ResolvedEntity = mergedEntity,
                Action = ConflictResolutionAction.Merged
            };
        }
        catch (Exception ex)
        {
            return new ConflictResolutionResult.IndividualResolution
            {
                Success = false,
                ErrorMessage = $"Failed to merge changes: {ex.Message}"
            };
        }
    }

    private object PerformFieldLevelMerge(object localEntity, object remoteEntity)
    {
        // This is a simplified merge - real implementation would need
        // entity-specific business logic
        
        var entityType = localEntity.GetType();
        var merged = Activator.CreateInstance(entityType)!;
        
        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite) continue;
            
            var localValue = prop.GetValue(localEntity);
            var remoteValue = prop.GetValue(remoteEntity);
            
            // Simple merge logic - prefer non-null, non-default values
            if (localValue != null && !IsDefaultValue(localValue))
            {
                prop.SetValue(merged, localValue);
            }
            else if (remoteValue != null && !IsDefaultValue(remoteValue))
            {
                prop.SetValue(merged, remoteValue);
            }
        }
        
        return merged;
    }

    private List<string> FindDifferentFields<T>(T local, T remote) where T : class
    {
        var differences = new List<string>();
        var type = typeof(T);
        
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var localValue = prop.GetValue(local);
            var remoteValue = prop.GetValue(remote);
            
            if (!Equals(localValue, remoteValue))
            {
                differences.Add(prop.Name);
            }
        }
        
        return differences;
    }

    private ConflictSeverity DetermineSeverity(List<string> conflictFields)
    {
        // Financial fields are critical
        var criticalFields = new[] { "Amount", "Balance", "Payment", "Interest", "Principal" };
        if (conflictFields.Any(f => criticalFields.Any(cf => f.Contains(cf, StringComparison.OrdinalIgnoreCase))))
        {
            return ConflictSeverity.Critical;
        }
        
        // Client identification fields are high priority
        var highPriorityFields = new[] { "Id", "ClientId", "LoanId", "Status" };
        if (conflictFields.Any(f => highPriorityFields.Any(hf => f.Contains(hf, StringComparison.OrdinalIgnoreCase))))
        {
            return ConflictSeverity.High;
        }
        
        return ConflictSeverity.Medium;
    }

    private DateTime GetLastModifiedTime(object entity)
    {
        // Try to find a LastModified or UpdatedAt property
        var type = entity.GetType();
        var modifiedProps = type.GetProperties()
            .Where(p => p.Name.Contains("Modified") || p.Name.Contains("Updated"))
            .Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?))
            .ToList();
        
        if (modifiedProps.Any())
        {
            var value = modifiedProps.First().GetValue(entity);
            if (value is DateTime dateTime)
                return dateTime;
            if (value is DateTime? nullableDateTime && nullableDateTime.HasValue)
                return nullableDateTime.Value;
        }
        
        return DateTime.MinValue;
    }

    private List<string> FindDuplicateIds<T>(List<T> entities) where T : class
    {
        var ids = new List<string>();
        var type = typeof(T);
        
        // Try to find an Id property
        var idProperty = type.GetProperty("Id") ?? 
                        type.GetProperty("ClientId") ?? 
                        type.GetProperty("LoanId");
        
        if (idProperty != null)
        {
            foreach (var entity in entities)
            {
                var id = idProperty.GetValue(entity)?.ToString();
                if (!string.IsNullOrEmpty(id))
                {
                    ids.Add(id);
                }
            }
        }
        
        return ids.GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
    }

    private async Task<EntityValidationResult> ValidateEntityAsync<T>(T entity, CancellationToken cancellationToken) where T : class
    {
        var result = new EntityValidationResult { IsValid = true };
        
        // Basic null checks
        var type = typeof(T);
        foreach (var prop in type.GetProperties())
        {
            var value = prop.GetValue(entity);
            
            // Check for required string properties that are null or empty
            if (prop.PropertyType == typeof(string) && prop.Name.Contains("Id"))
            {
                if (string.IsNullOrEmpty(value as string))
                {
                    result.IsValid = false;
                    result.Violations.Add($"{prop.Name} cannot be null or empty");
                }
            }
        }
        
        return result;
    }

    private bool IsDefaultValue(object value)
    {
        if (value == null) return true;
        
        var type = value.GetType();
        if (type.IsValueType)
        {
            var defaultValue = Activator.CreateInstance(type);
            return value.Equals(defaultValue);
        }
        
        return false;
    }

    private class EntityValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Violations { get; set; } = new();
    }
}

// Extension to ConflictResolutionResult for individual resolution tracking
public static class ConflictResolutionResultExtensions
{
    public class IndividualResolution
    {
        public bool Success { get; set; }
        public object ResolvedEntity { get; set; } = new();
        public ConflictResolutionAction Action { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}