using IntelliFin.UserMigration.Data;
using IntelliFin.UserMigration.Models;
using IntelliFin.UserMigration.Models.Keycloak;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.UserMigration.Services;

public sealed class RoleMigrationService
{
    private readonly IdentityDbContext _identityDbContext;
    private readonly AdminDbContext _adminDbContext;
    private readonly KeycloakAdminClient _keycloakAdminClient;
    private readonly ILogger<RoleMigrationService> _logger;

    public RoleMigrationService(
        IdentityDbContext identityDbContext,
        AdminDbContext adminDbContext,
        KeycloakAdminClient keycloakAdminClient,
        ILogger<RoleMigrationService> logger)
    {
        _identityDbContext = identityDbContext;
        _adminDbContext = adminDbContext;
        _keycloakAdminClient = keycloakAdminClient;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, RoleIdMapping>> MigrateRolesAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, RoleIdMapping>();
        var roles = await _identityDbContext.Roles.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var role in roles)
        {
            try
            {
                var mapping = await _adminDbContext.RoleIdMappings.FirstOrDefaultAsync(r => r.AspNetRoleId == role.Id, cancellationToken).ConfigureAwait(false);
                if (mapping is not null)
                {
                    _logger.LogInformation("Role {RoleName} already migrated as {KeycloakRoleId}.", role.Name, mapping.KeycloakRoleId);
                    result[role.Id] = mapping;
                    continue;
                }

                var keycloakRole = new KeycloakRoleRepresentation
                {
                    Name = role.Name,
                    Description = string.IsNullOrWhiteSpace(role.Description) ? role.Name : role.Description
                };

                var createdRole = await _keycloakAdminClient.CreateRealmRoleAsync(keycloakRole, cancellationToken).ConfigureAwait(false);
                mapping = new RoleIdMapping
                {
                    AspNetRoleId = role.Id,
                    KeycloakRoleId = createdRole.Id ?? throw new InvalidOperationException("Keycloak did not return a role identifier."),
                    RoleName = createdRole.Name,
                    MigrationDate = DateTime.UtcNow
                };

                await _adminDbContext.RoleIdMappings.AddAsync(mapping, cancellationToken).ConfigureAwait(false);
                result[role.Id] = mapping;

                _logger.LogInformation("Migrated role {RoleName} to Keycloak id {KeycloakRoleId}.", role.Name, mapping.KeycloakRoleId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate role {RoleName} ({RoleId}).", role.Name, role.Id);
                throw;
            }
        }

        await _adminDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
