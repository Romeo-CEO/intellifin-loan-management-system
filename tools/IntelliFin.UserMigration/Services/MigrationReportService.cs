using System.Text.Json;
using IntelliFin.UserMigration.Models;
using IntelliFin.UserMigration.Options;
using IntelliFin.UserMigration.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.UserMigration.Services;

public sealed class MigrationReportService
{
    private readonly MigrationOptions _options;
    private readonly ILogger<MigrationReportService> _logger;

    public MigrationReportService(IOptions<MigrationOptions> options, ILogger<MigrationReportService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> WriteMigrationReportAsync(MigrationResult migrationResult, ValidationResult? validationResult, CancellationToken cancellationToken)
    {
        var directory = string.IsNullOrWhiteSpace(_options.ReportsDirectory)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(_options.ReportsDirectory);

        Directory.CreateDirectory(directory);
        var fileName = $"migration-report-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        var filePath = Path.Combine(directory, fileName);

        var payload = new
        {
            generatedOnUtc = DateTime.UtcNow,
            migrationResult = new
            {
                migrationResult.SuccessCount,
                migrationResult.SkippedCount,
                FailureCount = migrationResult.FailedUsers.Count,
                FailedUsers = migrationResult.FailedUsers
            },
            validation = validationResult is null ? null : new
            {
                validationResult.UserCountMatches,
                validationResult.RoleCountMatches,
                validationResult.AssignmentCountMatches,
                validationResult.SampleErrors,
                validationResult.IsSuccessful
            }
        };

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, payload, new JsonSerializerOptions
        {
            WriteIndented = true
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Migration report written to {Path}", filePath);
        return filePath;
    }
}
