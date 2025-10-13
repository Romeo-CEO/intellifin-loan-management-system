using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;

namespace IntelliFin.AdminService.Services;

public interface IIncidentResponseService
{
    Task<IReadOnlyCollection<IncidentPlaybookDto>> GetPlaybooksAsync(CancellationToken cancellationToken);
    Task<IncidentPlaybookDto?> GetPlaybookAsync(Guid playbookId, CancellationToken cancellationToken);
    Task<IncidentPlaybookDto> CreatePlaybookAsync(CreateIncidentPlaybookRequest request, string createdBy, CancellationToken cancellationToken);
    Task<IncidentPlaybookDto?> UpdatePlaybookAsync(Guid playbookId, UpdateIncidentPlaybookRequest request, string updatedBy, CancellationToken cancellationToken);
    Task<IncidentPlaybookRunDto> RecordPlaybookRunAsync(Guid playbookId, RecordPlaybookUsageRequest request, string recordedBy, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AlertSilenceDto>> GetSilencesAsync(CancellationToken cancellationToken);
    Task<AlertSilenceDto> CreateSilenceAsync(CreateAlertSilenceRequest request, string createdBy, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OperationalIncidentDto>> GetIncidentsAsync(CancellationToken cancellationToken);
    Task<OperationalIncidentDto> CreateIncidentAsync(CreateOperationalIncidentRequest request, string createdBy, CancellationToken cancellationToken);
    Task<OperationalIncidentDto?> ResolveIncidentAsync(Guid incidentId, ResolveOperationalIncidentRequest request, string resolvedBy, CancellationToken cancellationToken);
}
