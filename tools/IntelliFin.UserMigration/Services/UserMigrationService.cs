using IntelliFin.UserMigration.Data;
using IntelliFin.UserMigration.Models;
using IntelliFin.UserMigration.Models.Keycloak;
using IntelliFin.UserMigration.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.UserMigration.Services;

public sealed class UserMigrationService
{
    private static readonly string[] RequiredActions = ["UPDATE_PASSWORD"];

    private readonly IdentityDbContext _identityDbContext;
    private readonly AdminDbContext _adminDbContext;
    private readonly KeycloakAdminClient _keycloakAdminClient;
    private readonly MigrationOptions _options;
    private readonly ILogger<UserMigrationService> _logger;

    public UserMigrationService(
        IdentityDbContext identityDbContext,
        AdminDbContext adminDbContext,
        KeycloakAdminClient keycloakAdminClient,
        IOptions<MigrationOptions> options,
        ILogger<UserMigrationService> logger)
    {
        _identityDbContext = identityDbContext;
        _adminDbContext = adminDbContext;
        _keycloakAdminClient = keycloakAdminClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MigrationResult> MigrateUsersAsync(IReadOnlyDictionary<string, RoleIdMapping> roleMappings, CancellationToken cancellationToken)
    {
        var result = new MigrationResult();
        var batchSize = Math.Max(1, _options.UserBatchSize);
        var totalUsers = await _identityDbContext.Users.CountAsync(cancellationToken).ConfigureAwait(false);

        for (var offset = 0; offset < totalUsers; offset += batchSize)
        {
            var users = await _identityDbContext.Users
                .AsNoTracking()
                .OrderBy(u => u.Id)
                .Skip(offset)
                .Take(batchSize)
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var user in users)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var mapping = await _adminDbContext.UserIdMappings.FirstOrDefaultAsync(m => m.AspNetUserId == user.Id, cancellationToken).ConfigureAwait(false);
                    string keycloakUserId;
                    if (mapping is not null)
                    {
                        keycloakUserId = mapping.KeycloakUserId;
                        _logger.LogInformation("User {Email} already mapped to Keycloak id {KeycloakUserId}. Re-applying role assignments.", user.Email, keycloakUserId);
                        result.SkippedCount++;
                    }
                    else
                    {
                        var keycloakUser = MapToKeycloakUser(user);
                        keycloakUserId = await _keycloakAdminClient.CreateUserAsync(keycloakUser, cancellationToken).ConfigureAwait(false);
                        mapping = new UserIdMapping
                        {
                            AspNetUserId = user.Id,
                            KeycloakUserId = keycloakUserId,
                            MigrationDate = DateTime.UtcNow,
                            MigrationStatus = "Completed"
                        };
                        await _adminDbContext.UserIdMappings.AddAsync(mapping, cancellationToken).ConfigureAwait(false);
                        result.SuccessCount++;
                        _logger.LogInformation("Migrated user {Email} to Keycloak id {KeycloakUserId}.", user.Email, keycloakUserId);
                    }

                    await MigrateUserRolesAsync(user, keycloakUserId, roleMappings, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to migrate user {UserId} ({Email}).", user.Id, user.Email);
                    result.FailedUsers.Add(new FailedMigration
                    {
                        UserId = user.Id,
                        Email = user.Email,
                        Error = ex.Message
                    });
                }
            }

            await _adminDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private static KeycloakUserRepresentation MapToKeycloakUser(AspNetUser user)
    {
        var keycloakUser = new KeycloakUserRepresentation
        {
            Username = user.UserName,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            EmailVerified = user.EmailConfirmed,
            Enabled = !user.LockoutEnabled || user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow,
            RequiredActions = RequiredActions.ToList()
        };

        AddAttributeIfPresent(keycloakUser, "phoneNumber", user.PhoneNumber);
        AddAttributeIfPresent(keycloakUser, "branchId", user.BranchId?.ToString());
        AddAttributeIfPresent(keycloakUser, "tenantId", user.TenantId?.ToString());
        AddFlagAttribute(keycloakUser, "phoneNumberConfirmed", user.PhoneNumberConfirmed);
        AddFlagAttribute(keycloakUser, "twoFactorEnabled", user.TwoFactorEnabled);
        AddFlagAttribute(keycloakUser, "lockoutEnabled", user.LockoutEnabled);

        return keycloakUser;
    }

    private static void AddAttributeIfPresent(KeycloakUserRepresentation user, string attributeName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            user.Attributes[attributeName] = new List<string> { value };
        }
    }

    private static void AddFlagAttribute(KeycloakUserRepresentation user, string attributeName, bool value)
    {
        user.Attributes[attributeName] = new List<string> { value ? bool.TrueString.ToLowerInvariant() : bool.FalseString.ToLowerInvariant() };
    }

    private async Task MigrateUserRolesAsync(AspNetUser user, string keycloakUserId, IReadOnlyDictionary<string, RoleIdMapping> roleMappings, CancellationToken cancellationToken)
    {
        var keycloakRoles = new List<KeycloakRoleRepresentation>();
        foreach (var userRole in user.UserRoles)
        {
            if (!roleMappings.TryGetValue(userRole.RoleId, out var mapping))
            {
                _logger.LogWarning("Role mapping not found for role {RoleId} when migrating user {UserId}.", userRole.RoleId, user.Id);
                continue;
            }

            keycloakRoles.Add(new KeycloakRoleRepresentation
            {
                Id = mapping.KeycloakRoleId,
                Name = mapping.RoleName
            });
        }

        await _keycloakAdminClient.AssignRealmRolesToUserAsync(keycloakUserId, keycloakRoles, cancellationToken).ConfigureAwait(false);
    }
}
