namespace IntelliFin.AdminService.Contracts.Responses;

public record EmergencyAccessDto(
    Guid EmergencyId,
    string IncidentTicketId,
    DateTime GrantedAt,
    DateTime ExpiresAt,
    string OneTimeToken,
    string RequestedBy,
    string ApprovedBy1,
    string ApprovedBy2);
