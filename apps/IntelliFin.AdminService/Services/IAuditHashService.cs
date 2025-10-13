using IntelliFin.AdminService.Models;

namespace IntelliFin.AdminService.Services;

public interface IAuditHashService
{
    string CalculateHash(AuditEvent auditEvent, string? previousHash);
    bool VerifyHash(AuditEvent auditEvent, string? previousHash);
}
