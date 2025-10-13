using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;

namespace IntelliFin.AdminService.Services;

public interface IMfaService
{
    Task<MfaChallengeResponse> InitiateChallengeAsync(
        string userId,
        string userName,
        string operation,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);

    Task<MfaValidationResponse> ValidateChallengeAsync(
        string userId,
        Guid challengeId,
        string otpCode,
        CancellationToken cancellationToken);

    Task<MfaEnrollmentResponse> GenerateEnrollmentAsync(
        string userId,
        string userName,
        string? userEmail,
        CancellationToken cancellationToken);

    Task VerifyEnrollmentAsync(
        string userId,
        string secretKey,
        string otpCode,
        CancellationToken cancellationToken);

    Task<MfaEnrollmentStatusResponse> GetEnrollmentStatusAsync(string userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MfaConfigDto>> GetMfaConfigurationAsync(CancellationToken cancellationToken);

    Task UpdateMfaConfigurationAsync(
        string operationName,
        MfaConfigUpdateDto update,
        string adminId,
        CancellationToken cancellationToken);
}
