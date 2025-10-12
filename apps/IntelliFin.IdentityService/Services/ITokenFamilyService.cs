using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

public interface ITokenFamilyService
{
    Task<TokenFamilyRegistration> RegisterTokenAsync(string refreshToken, TimeSpan familyTtl, string? familyId = null, CancellationToken cancellationToken = default);
    Task<bool> IsTokenLatestAsync(string familyId, string refreshToken, CancellationToken cancellationToken = default);
    Task<bool> IsFamilyRevokedAsync(string familyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> RevokeFamilyAsync(string familyId, TimeSpan familyTtl, CancellationToken cancellationToken = default);
}
