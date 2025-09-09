using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

public interface IJwtTokenService
{
    Task<string> GenerateAccessTokenAsync(UserClaims userClaims, CancellationToken cancellationToken = default);
    Task<string> GenerateRefreshTokenAsync(string userId, string deviceId, CancellationToken cancellationToken = default);
    Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<UserClaims?> GetClaimsFromTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> IsTokenRevokedAsync(string tokenId, CancellationToken cancellationToken = default);
    Task RevokeTokenAsync(string tokenId, CancellationToken cancellationToken = default);
    Task<bool> ValidateRefreshTokenAsync(string refreshToken, string userId, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<TokenResponse> RefreshTokensAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
}