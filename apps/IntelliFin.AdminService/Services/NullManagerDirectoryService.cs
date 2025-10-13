using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.Services;

public sealed class NullManagerDirectoryService : IManagerDirectoryService
{
    private readonly ILogger<NullManagerDirectoryService> _logger;

    public NullManagerDirectoryService(ILogger<NullManagerDirectoryService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<ManagerUserAssignment>> GetManagerUserAssignmentsAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Manager directory service not configured; returning empty assignment list");
        return Task.FromResult<IReadOnlyList<ManagerUserAssignment>>(Array.Empty<ManagerUserAssignment>());
    }
}
