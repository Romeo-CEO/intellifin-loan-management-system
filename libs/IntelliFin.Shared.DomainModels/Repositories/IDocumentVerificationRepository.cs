using IntelliFin.Shared.DomainModels.Entities;

namespace IntelliFin.Shared.DomainModels.Repositories;

public interface IDocumentVerificationRepository
{
    Task<DocumentVerification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DocumentVerification?> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<IEnumerable<DocumentVerification>> GetByClientIdAllAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<DocumentVerification> CreateAsync(DocumentVerification verification, CancellationToken cancellationToken = default);
    Task<DocumentVerification> UpdateAsync(DocumentVerification verification, CancellationToken cancellationToken = default);
    Task<IEnumerable<DocumentVerification>> GetPendingVerificationsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<DocumentVerification>> GetVerificationsByOfficerAsync(string officerId, DateTime? fromDate = null, CancellationToken cancellationToken = default);
}