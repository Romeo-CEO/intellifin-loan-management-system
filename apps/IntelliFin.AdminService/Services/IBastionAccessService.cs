using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;

namespace IntelliFin.AdminService.Services;

public interface IBastionAccessService
{
    Task<BastionAccessRequestDto> RequestAccessAsync(
        BastionAccessRequestInput request,
        string userId,
        string userName,
        string userEmail,
        CancellationToken cancellationToken);

    Task<BastionAccessRequestStatusDto?> GetAccessRequestStatusAsync(
        Guid requestId,
        CancellationToken cancellationToken);

    Task<BastionCertificateDto?> GetSshCertificateAsync(
        Guid requestId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BastionSessionDto>> GetActiveSessionsAsync(CancellationToken cancellationToken);

    Task<SessionRecordingDto?> GetSessionRecordingAsync(
        string sessionId,
        CancellationToken cancellationToken);

    Task<EmergencyAccessDto> RequestEmergencyAccessAsync(
        EmergencyAccessRequest request,
        string requestedBy,
        CancellationToken cancellationToken);

    Task RecordSessionAsync(
        BastionSessionIngestRequest request,
        CancellationToken cancellationToken);
}
