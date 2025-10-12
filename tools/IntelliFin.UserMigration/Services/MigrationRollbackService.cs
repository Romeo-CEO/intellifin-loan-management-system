using IntelliFin.UserMigration.Data;
using IntelliFin.UserMigration.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.UserMigration.Services;

public sealed class MigrationRollbackService
{
    private readonly AdminDbContext _adminDbContext;
    private readonly KeycloakAdminClient _keycloakAdminClient;
    private readonly ILogger<MigrationRollbackService> _logger;

    public MigrationRollbackService(AdminDbContext adminDbContext, KeycloakAdminClient keycloakAdminClient, ILogger<MigrationRollbackService> logger)
    {
        _adminDbContext = adminDbContext;
        _keycloakAdminClient = keycloakAdminClient;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var userMappings = await _adminDbContext.UserIdMappings.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var mapping in userMappings)
        {
            try
            {
                await _keycloakAdminClient.DeleteUserAsync(mapping.KeycloakUserId, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Deleted Keycloak user {KeycloakUserId} during rollback.", mapping.KeycloakUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete Keycloak user {KeycloakUserId} during rollback.", mapping.KeycloakUserId);
            }
        }

        var roleMappings = await _adminDbContext.RoleIdMappings.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var role in roleMappings)
        {
            try
            {
                await _keycloakAdminClient.DeleteRealmRoleAsync(role.RoleName, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Deleted Keycloak role {RoleName} during rollback.", role.RoleName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete Keycloak role {RoleName} during rollback.", role.RoleName);
            }
        }

        _adminDbContext.UserIdMappings.RemoveRange(_adminDbContext.UserIdMappings);
        _adminDbContext.RoleIdMappings.RemoveRange(_adminDbContext.RoleIdMappings);

        await _adminDbContext.MigrationAuditLogs.AddAsync(new MigrationAuditLog
        {
            Action = "Rollback",
            Actor = Environment.UserName ?? "migration-tool",
            CreatedOnUtc = DateTime.UtcNow,
            Details = $"Rollback executed at {DateTime.UtcNow:u}."
        }, cancellationToken).ConfigureAwait(false);

        await _adminDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
