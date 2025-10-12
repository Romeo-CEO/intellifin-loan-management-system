using IntelliFin.UserMigration.Data;
using IntelliFin.UserMigration.Models;
using IntelliFin.UserMigration.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IntelliFin.UserMigration.Options;

namespace IntelliFin.UserMigration.Services;

public sealed class MigrationValidationService
{
    private readonly IdentityDbContext _identityDbContext;
    private readonly AdminDbContext _adminDbContext;
    private readonly KeycloakAdminClient _keycloakAdminClient;
    private readonly MigrationOptions _options;
    private readonly ILogger<MigrationValidationService> _logger;

    public MigrationValidationService(
        IdentityDbContext identityDbContext,
        AdminDbContext adminDbContext,
        KeycloakAdminClient keycloakAdminClient,
        IOptions<MigrationOptions> options,
        ILogger<MigrationValidationService> logger)
    {
        _identityDbContext = identityDbContext;
        _adminDbContext = adminDbContext;
        _keycloakAdminClient = keycloakAdminClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateAsync(CancellationToken cancellationToken)
    {
        var result = new ValidationResult();
        var sourceUserCount = await _identityDbContext.Users.CountAsync(cancellationToken).ConfigureAwait(false);
        var targetUserCount = await _keycloakAdminClient.GetRealmUsersCountAsync(cancellationToken).ConfigureAwait(false);
        result.UserCountMatches = sourceUserCount == targetUserCount;
        _logger.LogInformation("User count validation: source={Source} target={Target}", sourceUserCount, targetUserCount);

        var sourceRoleCount = await _identityDbContext.Roles.CountAsync(cancellationToken).ConfigureAwait(false);
        var targetRoles = await _keycloakAdminClient.GetRealmRolesAsync(cancellationToken).ConfigureAwait(false);
        result.RoleCountMatches = sourceRoleCount == targetRoles.Count;
        _logger.LogInformation("Role count validation: source={Source} target={Target}", sourceRoleCount, targetRoles.Count);

        var sourceAssignments = await _identityDbContext.UserRoles.CountAsync(cancellationToken).ConfigureAwait(false);
        var keycloakAssignments = await CountKeycloakAssignmentsAsync(cancellationToken).ConfigureAwait(false);
        result.AssignmentCountMatches = sourceAssignments == keycloakAssignments;
        _logger.LogInformation("Assignment count validation: source={Source} target={Target}", sourceAssignments, keycloakAssignments);

        await PerformSampleValidationAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<int> CountKeycloakAssignmentsAsync(CancellationToken cancellationToken)
    {
        var roleMappings = await _adminDbContext.RoleIdMappings.AsNoTracking().ToDictionaryAsync(r => r.KeycloakRoleId, cancellationToken).ConfigureAwait(false);
        if (roleMappings.Count == 0)
        {
            return 0;
        }

        var assignmentCount = 0;
        var userMappings = await _adminDbContext.UserIdMappings.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var mapping in userMappings)
        {
            var roles = await _keycloakAdminClient.GetUserRealmRoleMappingsAsync(mapping.KeycloakUserId, cancellationToken).ConfigureAwait(false);
            assignmentCount += roles.Count(role => role.Id is not null && roleMappings.ContainsKey(role.Id));
        }

        return assignmentCount;
    }

    private async Task PerformSampleValidationAsync(ValidationResult validationResult, CancellationToken cancellationToken)
    {
        var totalUsers = await _identityDbContext.Users.CountAsync(cancellationToken).ConfigureAwait(false);
        if (totalUsers == 0)
        {
            return;
        }

        var sampleSize = Math.Max(1, (int)Math.Ceiling(totalUsers * (_options.ValidationSamplePercentage / 100m)));
        var sampleUsers = await _identityDbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderBy(_ => Guid.NewGuid())
            .Take(sampleSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var user in sampleUsers)
        {
            var mapping = await _adminDbContext.UserIdMappings.AsNoTracking().FirstOrDefaultAsync(m => m.AspNetUserId == user.Id, cancellationToken).ConfigureAwait(false);
            if (mapping is null)
            {
                validationResult.SampleErrors.Add($"User {user.Email} missing in mapping table");
                continue;
            }

            var keycloakUser = await _keycloakAdminClient.GetUserByIdAsync(mapping.KeycloakUserId, cancellationToken).ConfigureAwait(false);
            if (keycloakUser is null)
            {
                validationResult.SampleErrors.Add($"User {user.Email} missing from Keycloak");
                continue;
            }

            if (!string.Equals(keycloakUser.Email, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                validationResult.SampleErrors.Add($"User {user.Email} email mismatch: {keycloakUser.Email}");
            }

            if (keycloakUser.EmailVerified != user.EmailConfirmed)
            {
                validationResult.SampleErrors.Add($"User {user.Email} email verification mismatch");
            }

            var keycloakRoles = await _keycloakAdminClient.GetUserRealmRoleMappingsAsync(mapping.KeycloakUserId, cancellationToken).ConfigureAwait(false);
            var aspNetRoles = user.UserRoles.Select(ur => ur.Role.Name).OrderBy(name => name).ToList();
            var keycloakRoleNames = keycloakRoles.Select(r => r.Name).OrderBy(name => name).ToList();
            if (!aspNetRoles.SequenceEqual(keycloakRoleNames))
            {
                validationResult.SampleErrors.Add($"User {user.Email} role mismatch: aspnet=[{string.Join(',', aspNetRoles)}] keycloak=[{string.Join(',', keycloakRoleNames)}]");
            }
        }
    }
}
