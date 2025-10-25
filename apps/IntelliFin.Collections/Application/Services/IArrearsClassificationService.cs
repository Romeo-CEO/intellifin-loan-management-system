using IntelliFin.Collections.Domain.Entities;

namespace IntelliFin.Collections.Application.Services;

public interface IArrearsClassificationService
{
    /// <summary>
    /// Classifies all active loans based on Days Past Due (DPD) per BoZ directives.
    /// </summary>
    Task<int> ClassifyAllLoansAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Classifies a specific loan based on DPD.
    /// </summary>
    Task ClassifyLoanAsync(
        Guid loanId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets classification history for a loan.
    /// </summary>
    Task<List<ArrearsClassificationHistory>> GetClassificationHistoryAsync(
        Guid loanId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current arrears summary by classification.
    /// </summary>
    Task<Dictionary<string, int>> GetArrearsSummaryAsync(
        CancellationToken cancellationToken = default);
}
