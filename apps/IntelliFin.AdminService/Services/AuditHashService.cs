using System.Security.Cryptography;
using System.Text;
using IntelliFin.AdminService.Models;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.Services;

public sealed class AuditHashService : IAuditHashService, IDisposable
{
    private readonly SHA256 _sha256 = SHA256.Create();
    private bool _disposed;
    private readonly ILogger<AuditHashService> _logger;

    public AuditHashService(ILogger<AuditHashService> logger)
    {
        _logger = logger;
    }

    public string CalculateHash(AuditEvent auditEvent, string? previousHash)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AuditHashService));
        }

        var builder = new StringBuilder();
        builder.Append(previousHash ?? string.Empty);
        builder.Append(auditEvent.EventId.ToString("N"));
        builder.Append(auditEvent.Timestamp.ToUniversalTime().ToString("O"));
        builder.Append(auditEvent.Actor ?? string.Empty);
        builder.Append(auditEvent.Action ?? string.Empty);
        builder.Append(auditEvent.EntityType ?? string.Empty);
        builder.Append(auditEvent.EntityId ?? string.Empty);
        builder.Append(auditEvent.EventData ?? string.Empty);

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = _sha256.ComputeHash(bytes);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();

        _logger.LogDebug("Calculated audit hash for event {EventId}: {Hash}", auditEvent.EventId, hex);
        return hex;
    }

    public bool VerifyHash(AuditEvent auditEvent, string? previousHash)
    {
        var calculated = CalculateHash(auditEvent, previousHash);
        return string.Equals(calculated, auditEvent.CurrentEventHash, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _sha256.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
