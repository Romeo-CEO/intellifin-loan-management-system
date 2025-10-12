using IntelliFin.UserMigration.Models;
using IntelliFin.UserMigration.Validation;
using Microsoft.Extensions.Logging;

namespace IntelliFin.UserMigration.Services;

public sealed class MigrationOrchestrator
{
    private readonly RoleMigrationService _roleMigrationService;
    private readonly UserMigrationService _userMigrationService;
    private readonly MigrationValidationService _validationService;
    private readonly MigrationRollbackService _rollbackService;
    private readonly MigrationReportService _reportService;
    private readonly ILogger<MigrationOrchestrator> _logger;

    public MigrationOrchestrator(
        RoleMigrationService roleMigrationService,
        UserMigrationService userMigrationService,
        MigrationValidationService validationService,
        MigrationRollbackService rollbackService,
        MigrationReportService reportService,
        ILogger<MigrationOrchestrator> logger)
    {
        _roleMigrationService = roleMigrationService;
        _userMigrationService = userMigrationService;
        _validationService = validationService;
        _rollbackService = rollbackService;
        _reportService = reportService;
        _logger = logger;
    }

    public async Task<(MigrationResult Migration, ValidationResult Validation)> ExecuteFullMigrationAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting role migration.");
        var roleMappings = await _roleMigrationService.MigrateRolesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Starting user migration.");
        var migrationResult = await _userMigrationService.MigrateUsersAsync(roleMappings, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Running migration validation.");
        var validationResult = await _validationService.ValidateAsync(cancellationToken).ConfigureAwait(false);

        await _reportService.WriteMigrationReportAsync(migrationResult, validationResult, cancellationToken).ConfigureAwait(false);
        return (migrationResult, validationResult);
    }

    public async Task<MigrationResult> ExecuteUserMigrationOnlyAsync(CancellationToken cancellationToken)
    {
        var roleMappings = await _roleMigrationService.MigrateRolesAsync(cancellationToken).ConfigureAwait(false);
        var result = await _userMigrationService.MigrateUsersAsync(roleMappings, cancellationToken).ConfigureAwait(false);
        await _reportService.WriteMigrationReportAsync(result, null, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public Task<ValidationResult> ValidateAsync(CancellationToken cancellationToken) => _validationService.ValidateAsync(cancellationToken);

    public Task ExecuteRollbackAsync(CancellationToken cancellationToken) => _rollbackService.ExecuteAsync(cancellationToken);
}
