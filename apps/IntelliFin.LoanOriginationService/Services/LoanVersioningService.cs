using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IntelliFin.LoanOriginationService.Services;

/// <summary>
/// Implementation of loan versioning and number generation service.
/// Uses database-level locking for thread-safe sequence generation.
/// </summary>
public class LoanVersioningService : ILoanVersioningService
{
    private readonly LmsDbContext _context;
    private readonly ILogger<LoanVersioningService> _logger;

    public LoanVersioningService(LmsDbContext context, ILogger<LoanVersioningService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GenerateLoanNumberAsync(string branchCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branchCode))
            throw new ArgumentException("Branch code cannot be null or empty", nameof(branchCode));

        var year = DateTime.UtcNow.Year;
        var sequenceNumber = await GetNextSequenceNumberAsync(branchCode, year, cancellationToken);
        var loanNumber = $"{branchCode}-{year}-{sequenceNumber:D5}";

        _logger.LogInformation("Generated loan number {LoanNumber} for branch {BranchCode}", loanNumber, branchCode);
        return loanNumber;
    }

    /// <inheritdoc />
    public async Task<LoanApplication> CreateNewVersionAsync(
        Guid loanId,
        string reason,
        Dictionary<string, object> changes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason for version creation is required", nameof(reason));

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Fetch current version
            var currentVersion = await _context.LoanApplications
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == loanId, cancellationToken);

            if (currentVersion == null)
                throw new InvalidOperationException($"Loan application {loanId} not found");

            if (!currentVersion.IsCurrentVersion)
                throw new InvalidOperationException($"Loan application {loanId} is not the current version");

            // Create snapshot of current state
            var snapshot = new
            {
                currentVersion.Id,
                currentVersion.ClientId,
                currentVersion.Amount,
                currentVersion.TermMonths,
                currentVersion.ProductCode,
                currentVersion.ProductName,
                currentVersion.Status,
                currentVersion.RequestedAmount,
                currentVersion.RiskGrade,
                currentVersion.EffectiveAnnualRate,
                currentVersion.WorkflowInstanceId,
                currentVersion.DeclineReason,
                currentVersion.ApprovedAt,
                currentVersion.ApprovedBy,
                currentVersion.ApplicationDataJson,
                Changes = changes
            };

            var snapshotJson = JsonSerializer.Serialize(snapshot);

            // Mark current version as non-current
            currentVersion.IsCurrentVersion = false;
            currentVersion.LastModifiedAtUtc = DateTime.UtcNow;

            // Create new version
            var newVersion = new LoanApplication
            {
                Id = Guid.NewGuid(),
                LoanNumber = currentVersion.LoanNumber,
                Version = currentVersion.Version + 1,
                ParentVersionId = currentVersion.Id,
                IsCurrentVersion = true,
                ClientId = currentVersion.ClientId,
                Amount = currentVersion.Amount,
                TermMonths = currentVersion.TermMonths,
                ProductCode = currentVersion.ProductCode,
                ProductName = currentVersion.ProductName,
                Status = currentVersion.Status,
                CreatedAtUtc = DateTime.UtcNow,
                RequestedAmount = currentVersion.RequestedAmount,
                RiskGrade = currentVersion.RiskGrade,
                EffectiveAnnualRate = currentVersion.EffectiveAnnualRate,
                WorkflowInstanceId = currentVersion.WorkflowInstanceId,
                DeclineReason = currentVersion.DeclineReason,
                ApprovedAt = currentVersion.ApprovedAt,
                ApprovedBy = currentVersion.ApprovedBy,
                ApplicationDataJson = currentVersion.ApplicationDataJson,
                AgreementFileHash = currentVersion.AgreementFileHash,
                AgreementMinioPath = currentVersion.AgreementMinioPath,
                CreatedBy = currentVersion.CreatedBy,
                LastModifiedAtUtc = DateTime.UtcNow
            };

            // Apply changes from the changes dictionary
            foreach (var change in changes)
            {
                var property = typeof(LoanApplication).GetProperty(change.Key);
                if (property != null && property.CanWrite)
                {
                    var convertedValue = Convert.ChangeType(change.Value, property.PropertyType);
                    property.SetValue(newVersion, convertedValue);
                }
            }

            // Save both versions
            _context.LoanApplications.Update(currentVersion);
            await _context.LoanApplications.AddAsync(newVersion, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Created new version {Version} for loan {LoanNumber}. Reason: {Reason}",
                newVersion.Version,
                newVersion.LoanNumber,
                reason);

            return newVersion;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Gets the next sequence number using database-level atomic increment.
    /// Uses SQL UPDATE with OUTPUT clause for thread-safe operation.
    /// </summary>
    private async Task<int> GetNextSequenceNumberAsync(string branchCode, int year, CancellationToken cancellationToken)
    {
        // Try to update existing sequence
        var sql = @"
            UPDATE LoanNumberSequences 
            SET NextSequence = NextSequence + 1, 
                LastUpdatedUtc = GETUTCDATE()
            OUTPUT INSERTED.NextSequence 
            WHERE BranchCode = @BranchCode AND Year = @Year";

        var branchCodeParam = new SqlParameter("@BranchCode", branchCode);
        var yearParam = new SqlParameter("@Year", year);

        var result = await _context.Database
            .SqlQueryRaw<int>(sql, branchCodeParam, yearParam)
            .ToListAsync(cancellationToken);

        if (result.Count > 0)
        {
            return result[0];
        }

        // No existing sequence found - create new one
        var insertSql = @"
            INSERT INTO LoanNumberSequences (BranchCode, Year, NextSequence, LastUpdatedUtc)
            VALUES (@BranchCode, @Year, 2, GETUTCDATE());
            SELECT 1"; // Return 1 as the first sequence number

        var insertBranchCodeParam = new SqlParameter("@BranchCode", branchCode);
        var insertYearParam = new SqlParameter("@Year", year);

        try
        {
            var insertResult = await _context.Database
                .SqlQueryRaw<int>(insertSql, insertBranchCodeParam, insertYearParam)
                .ToListAsync(cancellationToken);

            return insertResult[0];
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 2627) // Duplicate key
        {
            // Race condition: another thread created the sequence
            // Retry the update
            return await GetNextSequenceNumberAsync(branchCode, year, cancellationToken);
        }
    }
}
