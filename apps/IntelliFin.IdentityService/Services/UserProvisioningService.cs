using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Repositories;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service for provisioning users to Keycloak
/// </summary>
public interface IKeycloakUserProvisioningService
{
    Task<ProvisioningResult> ProvisionUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<ProvisioningResult> SyncUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<BulkProvisioningResult> ProvisionAllUsersAsync(bool dryRun = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of user provisioning service
/// </summary>
public class KeycloakProvisioningService : IKeycloakUserProvisioningService
{
    private readonly IKeycloakAdminClient _keycloakAdmin;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserProvisioningService> _logger;

    public KeycloakProvisioningService(
        IKeycloakAdminClient keycloakAdmin,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IUserRepository userRepository,
        ILogger<KeycloakProvisioningService> logger)
    {
        _keycloakAdmin = keycloakAdmin;
        _userManager = userManager;
        _roleManager = roleManager;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<ProvisioningResult> ProvisionUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return UserProvisioningResult.Failure($"User {userId} not found");
            }

            return await ProvisionUserInternalAsync(user, false, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error provisioning user {UserId}", userId);
            return UserProvisioningResult.Failure($"Exception: {ex.Message}");
        }
    }

    public async Task<ProvisioningResult> SyncUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ProvisioningResult.Failure($"User {userId} not found");
            }

            return await ProvisionUserInternalAsync(user, true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing user {UserId}", userId);
            return ProvisioningResult.Failure($"Exception: {ex.Message}");
        }
    }

    public async Task<BulkProvisioningResult> ProvisionAllUsersAsync(bool dryRun = false, CancellationToken cancellationToken = default)
    {
        var result = new BulkProvisioningResult();

        try
        {
            _logger.LogInformation("Starting bulk user provisioning to Keycloak (DryRun: {DryRun})", dryRun);

            // Get all users (consider pagination for large datasets)
            var users = _userManager.Users.ToList();
            result.TotalUsers = users.Count;

            foreach (var user in users)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Bulk provisioning cancelled");
                    break;
                }

                ProvisioningResult provisionResult;
                
                if (dryRun)
                {
                    // In dry-run mode, just check if user exists
                    var existingUser = await _keycloakAdmin.GetUserByEmailAsync(user.Email!, cancellationToken);
                    if (existingUser != null)
                    {
                        result.SkippedProvisions++;
                    }
                    else
                    {
                        result.PendingCreates++;
                    }
                    continue;
                }
                
                provisionResult = await ProvisionUserInternalAsync(user, false, cancellationToken);
                
                if (provisionResult.Success)
                {
                    if (provisionResult.Action == ProvisioningAction.Created)
                    {
                        result.CreatedUsers++;
                    }
                    else if (provisionResult.Action == ProvisioningAction.Updated)
                    {
                        result.UpdatedUsers++;
                    }
                    else
                    {
                        result.SkippedProvisions++;
                    }
                }
                else
                {
                    result.FailedProvisions++;
                    result.Errors.Add($"{user.Email}: {provisionResult.ErrorMessage}");
                }

                // Log progress every 10 users
                if ((result.TotalProcessed) % 10 == 0)
                {
                    _logger.LogInformation("Progress: {Processed}/{Total} users processed", 
                        result.TotalProcessed, result.TotalUsers);
                }
            }

            _logger.LogInformation("Bulk provisioning complete: Created={Created}, Updated={Updated}, Skipped={Skipped}, Failed={Failed}, Total={Total}", 
                result.CreatedUsers, result.UpdatedUsers, result.SkippedProvisions, result.FailedProvisions, result.TotalUsers);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk user provisioning");
            result.Errors.Add($"Bulk provisioning exception: {ex.Message}");
            return result;
        }
    }


    private async Task<ProvisioningResult> ProvisionUserInternalAsync(
        ApplicationUser user,
        bool isSync,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Provisioning user {Email} to Keycloak", user.Email);

            // Check if user already exists in Keycloak by extUserId attribute or email
            var existingUser = await FindKeycloakUserAsync(user, cancellationToken);
            bool userExists = existingUser != null;
            
            if (userExists && !isSync)
            {
                _logger.LogInformation("User {Email} already exists in Keycloak (ID: {KeycloakId}), skipping", 
                    user.Email, existingUser!.Id);
                return ProvisioningResult.Skipped(existingUser.Id!);
            }

            // Build Keycloak user representation with all attributes
            var keycloakUser = await BuildKeycloakUserRepresentationAsync(user, cancellationToken);

            string keycloakUserId;
            ProvisioningAction action;
            
            if (userExists)
            {
                // Update existing user
                var updated = await _keycloakAdmin.UpdateUserAsync(existingUser!.Id!, keycloakUser, cancellationToken);
                if (!updated)
                {
                    return ProvisioningResult.Failure("Failed to update user in Keycloak");
                }
                keycloakUserId = existingUser!.Id!;
                action = ProvisioningAction.Updated;
                _logger.LogInformation("Updated user {Email} in Keycloak (ID: {KeycloakId})", user.Email, keycloakUserId);
            }
            else
            {
                // Create new user
                keycloakUserId = await _keycloakAdmin.CreateUserAsync(keycloakUser, cancellationToken);
                if (string.IsNullOrEmpty(keycloakUserId))
                {
                    return ProvisioningResult.Failure("Failed to create user in Keycloak");
                }
                
                // Set temporary password (user will need to change on first login)
                var tempPassword = GenerateTemporaryPassword();
                var passwordSet = await _keycloakAdmin.SetTemporaryPasswordAsync(
                    keycloakUserId, tempPassword, cancellationToken);

                if (!passwordSet)
                {
                    _logger.LogWarning("Failed to set temporary password for user {Email}", user.Email);
                }
                
                action = ProvisioningAction.Created;
                _logger.LogInformation("Created user {Email} in Keycloak (ID: {KeycloakId})", user.Email, keycloakUserId);
            }

            // Sync user roles (idempotent)
            await SyncUserRolesAsync(user, keycloakUserId, cancellationToken);

            return ProvisioningResult.Success(keycloakUserId, action);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error provisioning user {Email} internally", user.Email);
            return ProvisioningResult.Failure($"Exception: {ex.Message}");
        }
    }

    private string GenerateTemporaryPassword()
    {
        // Generate a secure random password
        // Format: 4 groups of 4 characters (e.g., Abcd-1234-Efgh-5678)
        const string upperChars = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lowerChars = "abcdefghijkmnopqrstuvwxyz";
        const string digitChars = "23456789";
        const string specialChars = "!@#$%^&*";

        var random = RandomNumberGenerator.Create();
        var password = new char[19]; // 16 chars + 3 dashes

        // Ensure at least one of each required character type
        password[0] = upperChars[GetRandomNumber(random, upperChars.Length)];
        password[1] = lowerChars[GetRandomNumber(random, lowerChars.Length)];
        password[2] = digitChars[GetRandomNumber(random, digitChars.Length)];
        password[3] = specialChars[GetRandomNumber(random, specialChars.Length)];
        password[4] = '-';

        // Fill remaining characters
        var allChars = upperChars + lowerChars + digitChars + specialChars;
        for (int i = 5; i < 19; i++)
        {
            if (i == 9 || i == 14)
            {
                password[i] = '-';
            }
            else
            {
                password[i] = allChars[GetRandomNumber(random, allChars.Length)];
            }
        }

        return new string(password);
    }

    private async Task<KeycloakUserRepresentation?> FindKeycloakUserAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        // First try to find by extUserId attribute
        var users = await _keycloakAdmin.GetUserByEmailAsync(user.Email!, cancellationToken);
        if (users != null)
        {
            // Check if it has our extUserId attribute
            if (users.Attributes?.TryGetValue("extUserId", out var extUserIds) == true &&
                extUserIds.Contains(user.Id.ToString()))
            {
                return users;
            }
        }
        
        // Fallback to email match
        return users;
    }

    private async Task<KeycloakUserRepresentation> BuildKeycloakUserRepresentationAsync(
        ApplicationUser user, 
        CancellationToken cancellationToken)
    {
        // Get user's tenant memberships (if any)
        var tenantUsers = await _userRepository.GetUserTenantsAsync(user.Id);
        var tenantIds = tenantUsers.Select(tu => tu.TenantId.ToString()).ToArray();
        var tenantNames = tenantUsers.Select(tu => tu.Tenant?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToArray();

        // Get user's permissions from role claims
        var roles = await _userManager.GetRolesAsync(user);
        var permissions = new List<string>();
        foreach (var roleName in roles)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                var claims = await _roleManager.GetClaimsAsync(role);
                var rolePermissions = claims
                    .Where(c => c.Type == "permission")
                    .Select(c => c.Value)
                    .Distinct();
                permissions.AddRange(rolePermissions);
            }
        }

        var attributes = new Dictionary<string, string[]>
        {
            ["extUserId"] = new[] { user.Id.ToString() },
            ["branchId"] = user.BranchId != null ? new[] { user.BranchId } : Array.Empty<string>(),
            ["branchName"] = user.BranchName != null ? new[] { user.BranchName } : Array.Empty<string>(),
            ["branchRegion"] = user.BranchRegion != null ? new[] { user.BranchRegion } : Array.Empty<string>(),
            ["tenantId"] = tenantIds,
            ["tenantName"] = tenantNames,
            ["permissions"] = permissions.Distinct().ToArray()
        };

        return new KeycloakUserRepresentation
        {
            Username = user.UserName!,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Enabled = !user.LockoutEnd.HasValue || user.LockoutEnd.Value < DateTimeOffset.UtcNow,
            EmailVerified = user.EmailConfirmed,
            Attributes = attributes
        };
    }

    private async Task SyncUserRolesAsync(ApplicationUser user, string keycloakUserId, CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        foreach (var roleName in roles)
        {
            var roleAssigned = await _keycloakAdmin.AssignRealmRoleAsync(
                keycloakUserId, roleName, cancellationToken);

            if (roleAssigned)
            {
                _logger.LogDebug("Assigned role {RoleName} to user {Email}", roleName, user.Email);
            }
            else
            {
                _logger.LogWarning("Failed to assign role {RoleName} to user {Email}", roleName, user.Email);
            }
        }
    }

    private int GetRandomNumber(RandomNumberGenerator random, int max)
    {
        var bytes = new byte[4];
        random.GetBytes(bytes);
        var value = BitConverter.ToUInt32(bytes, 0);
        return (int)(value % (uint)max);
    }
}

/// <summary>
/// Result of user provisioning operation
/// </summary>
public class ProvisioningResult
{
    public bool Success { get; set; }
    public string? KeycloakUserId { get; set; }
    public string? ErrorMessage { get; set; }
    public ProvisioningAction Action { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();

    public static ProvisioningResult CreateSuccess(string keycloakUserId, ProvisioningAction action)
    {
        return new ProvisioningResult
        {
            Success = true,
            KeycloakUserId = keycloakUserId,
            Action = action
        };
    }

    public static ProvisioningResult CreateSkipped(string keycloakUserId)
    {
        return new ProvisioningResult
        {
            Success = true,
            KeycloakUserId = keycloakUserId,
            Action = ProvisioningAction.Skipped
        };
    }

    public static ProvisioningResult CreateFailure(string errorMessage)
    {
        return new ProvisioningResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Action = ProvisioningAction.Failed
        };
    }
}

/// <summary>
/// Action taken during provisioning
/// </summary>
public enum ProvisioningAction
{
    Created,
    Updated,
    Skipped,
    Failed
}

/// <summary>
/// Result of bulk user provisioning operation
/// </summary>
public class BulkProvisioningResult
{
    public int TotalUsers { get; set; }
    public int CreatedUsers { get; set; }
    public int UpdatedUsers { get; set; }
    public int SkippedProvisions { get; set; }
    public int FailedProvisions { get; set; }
    public int PendingCreates { get; set; }
    public List<string> Errors { get; set; } = new();

    public int TotalProcessed => CreatedUsers + UpdatedUsers + SkippedProvisions + FailedProvisions;
    public double SuccessRate => TotalUsers > 0 
        ? (double)(CreatedUsers + UpdatedUsers) / TotalUsers * 100 
        : 0;
}

/// <summary>
/// Command to provision a user
/// </summary>
public class ProvisionCommand
{
    public string UserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
}
