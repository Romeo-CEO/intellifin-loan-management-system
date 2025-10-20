using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Models.Domain;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;

using AuditEvent = IntelliFin.IdentityService.Models.AuditEvent;
namespace IntelliFin.IdentityService.Services;

public class BaselineSeedService : IBaselineSeedService
{
    private readonly LmsDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<BaselineSeedService> _logger;

    public BaselineSeedService(
        LmsDbContext context,
        RoleManager<ApplicationRole> roleManager,
        IAuditService auditService,
        ILogger<BaselineSeedService> logger)
    {
        _context = context;
        _roleManager = roleManager;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<SeedResult> SeedBaselineDataAsync(CancellationToken cancellationToken = default)
    {
        var result = new SeedResult();
        var startedAt = DateTime.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        IDbContextTransaction? transaction = null;
        var useTransaction = _context.Database.IsRelational();
        if (useTransaction)
        {
            transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        }
        try
        {
            var seedConfig = LoadSeedConfiguration();

            foreach (var roleDefinition in seedConfig.Roles)
            {
                var (created, permissionCount) = await SeedRoleAsync(roleDefinition, cancellationToken);
                if (created)
                {
                    result.RolesCreated++;
                    result.PermissionsCreated += permissionCount;
                }
                else
                {
                    result.RolesSkipped++;
                }
            }

            foreach (var sodRule in seedConfig.SodRules)
            {
                var created = await SeedSoDRuleAsync(sodRule, cancellationToken);
                if (created) result.SoDRulesCreated++;
            }

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            sw.Stop();
            _logger.LogInformation("Baseline seed completed: {Roles} roles, {Perms} permissions, {Rules} SoD rules in {ElapsedMs} ms",
                result.RolesCreated, result.PermissionsCreated, result.SoDRulesCreated, sw.ElapsedMilliseconds);
            if (sw.Elapsed.TotalSeconds > 10)
            {
                _logger.LogWarning("Baseline seed exceeded expected duration (<10s). Took {Seconds:N2}s", sw.Elapsed.TotalSeconds);
            }
            return result;
        }
        catch (Exception ex)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Baseline seed operation failed");
            result.Errors.Add($"Seed failed: {ex.Message}");
            return result;
        }
    }

    public async Task<SeedValidationResult> ValidateSeedDataAsync(CancellationToken cancellationToken = default)
    {
        var result = new SeedValidationResult();
        var config = LoadSeedConfiguration();

        foreach (var role in config.Roles)
        {
            var exists = await _roleManager.RoleExistsAsync(role.RoleName);
            result.AddRoleCheck(role.RoleName, !exists);
        }

        foreach (var sod in config.SodRules)
        {
            var exists = await _context.Set<SoDRule>().AnyAsync(r => r.Name == sod.RuleName, cancellationToken);
            result.AddSoDCheck(sod.RuleName, !exists);
        }

        return result;
    }

    private async Task<(bool Created, int PermissionCount)> SeedRoleAsync(RoleDefinition roleDefinition, CancellationToken cancellationToken)
    {
        if (await _roleManager.RoleExistsAsync(roleDefinition.RoleName))
        {
            _logger.LogInformation("Role {RoleName} already exists, skipping", roleDefinition.RoleName);
            return (false, 0);
        }

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = roleDefinition.RoleName,
            NormalizedName = roleDefinition.RoleName.ToUpperInvariant(),
            Description = roleDefinition.Description,
            IsCustom = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        var createResult = await _roleManager.CreateAsync(role);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            throw new Exception($"Failed to create role {roleDefinition.RoleName}: {errors}");
        }

        int permissionCount = 0;
        foreach (var permission in roleDefinition.Permissions)
        {
            await _roleManager.AddClaimAsync(role, new Claim("permission", permission));
            permissionCount++;
        }

        await _auditService.LogAsync(new AuditEvent
        {
            ActorId = "SYSTEM",
            Action = "RoleCreated",
            Entity = "Role",
            EntityId = role.Id,
            Details = new Dictionary<string, object>
            {
                ["RoleName"] = roleDefinition.RoleName,
                ["PermissionCount"] = permissionCount
            },
            Success = true
        }, cancellationToken);

        return (true, permissionCount);
    }

    private async Task<bool> SeedSoDRuleAsync(SoDRuleDefinition sodDefinition, CancellationToken cancellationToken)
    {
        var exists = await _context.Set<SoDRule>().AnyAsync(r => r.Name == sodDefinition.RuleName, cancellationToken);
        if (exists)
        {
            _logger.LogInformation("SoD rule {RuleName} already exists, skipping", sodDefinition.RuleName);
            return false;
        }

        var enforcement = Enum.TryParse<SoDEnforcementLevel>(sodDefinition.Enforcement, true, out var parsed)
            ? parsed
            : SoDEnforcementLevel.Strict;

        var sodRule = new SoDRule
        {
            Id = Guid.NewGuid(),
            Name = sodDefinition.RuleName,
            Description = sodDefinition.Description,
            Enforcement = enforcement,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system",
        };
        sodRule.ConflictingPermissions = sodDefinition.ConflictingPermissions;

        _context.Set<SoDRule>().Add(sodRule);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEvent
        {
            ActorId = "SYSTEM",
            Action = "SoDRuleCreated",
            Entity = "SoDRule",
            EntityId = sodRule.Id.ToString(),
            Details = new Dictionary<string, object>
            {
                ["RuleName"] = sodDefinition.RuleName,
                ["Enforcement"] = sodDefinition.Enforcement
            },
            Success = true
        }, cancellationToken);

        return true;
    }

    private BaselineSeedConfiguration LoadSeedConfiguration()
    {
        var baseDir = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDir, "Data", "Seeds", "BaselineRolesSeed.json");
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Seed configuration not found at {configPath}");
        }

        var json = File.ReadAllText(configPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<BaselineSeedConfiguration>(json, options)
            ?? throw new Exception("Failed to deserialize seed configuration");
    }
}
