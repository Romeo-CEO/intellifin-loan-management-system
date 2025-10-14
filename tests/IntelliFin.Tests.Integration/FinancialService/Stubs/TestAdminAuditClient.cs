using System.Text;
using IntelliFin.FinancialService.Clients;
using IntelliFin.FinancialService.Exceptions;
using IntelliFin.FinancialService.Models.Audit;

namespace IntelliFin.Tests.Integration.FinancialService.Stubs;

internal sealed class TestAdminAuditClient : IAdminAuditClient
{
    public AuditEventPageResponse EventsResponse { get; set; } = new()
    {
        Data = new List<AuditEventDto>
        {
            new()
            {
                EventId = Guid.NewGuid(),
                Actor = "system",
                Action = "CollectionsPaymentRecorded",
                EntityType = "CollectionsPayment",
                EntityId = "PAY-1",
                Timestamp = DateTime.UtcNow,
                IntegrityStatus = "VERIFIED"
            }
        },
        Pagination = new PaginationMetadataDto
        {
            CurrentPage = 1,
            PageSize = 50,
            TotalCount = 1,
            TotalPages = 1
        }
    };

    public AuditIntegrityStatusResponse IntegrityStatus { get; set; } = new()
    {
        ChainStatus = new AuditChainStatus
        {
            TotalEvents = 100,
            VerifiedEvents = 100,
            BrokenEvents = 0,
            CoveragePercentage = 100
        }
    };

    public AuditIntegrityHistoryResponse IntegrityHistory { get; set; } = new()
    {
        Data = new List<AuditVerificationHistoryItem>
        {
            new()
            {
                VerificationId = Guid.NewGuid(),
                ChainStatus = "VALID",
                EventsVerified = 100,
                StartTime = DateTime.UtcNow.AddMinutes(-10),
                EndTime = DateTime.UtcNow,
                InitiatedBy = "ops"
            }
        },
        Pagination = new PaginationMetadataDto
        {
            CurrentPage = 1,
            PageSize = 50,
            TotalCount = 1,
            TotalPages = 1
        }
    };

    public bool ShouldThrow { get; set; }

    public Task<AuditEventPageResponse> GetEventsAsync(AuditEventQuery query, CancellationToken cancellationToken = default)
    {
        ThrowIfRequired();
        return Task.FromResult(EventsResponse);
    }

    public Task<AuditExportResult> ExportEventsAsync(DateTime? startDate, DateTime? endDate, string format, CancellationToken cancellationToken = default)
    {
        ThrowIfRequired();
        var content = System.Text.Encoding.UTF8.GetBytes("actor,action\nsystem,CollectionsPaymentRecorded");
        return Task.FromResult(new AuditExportResult(content, "text/csv", "audit.csv"));
    }

    public Task<AuditIntegrityStatusResponse> GetIntegrityStatusAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfRequired();
        return Task.FromResult(IntegrityStatus);
    }

    public Task<AuditIntegrityHistoryResponse> GetIntegrityHistoryAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        ThrowIfRequired();
        return Task.FromResult(IntegrityHistory);
    }

    private void ThrowIfRequired()
    {
        if (ShouldThrow)
        {
            throw new AuditForwardingException("Admin Service unavailable");
        }
    }
}
