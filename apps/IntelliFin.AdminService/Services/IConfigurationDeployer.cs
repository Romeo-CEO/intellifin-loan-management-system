using System;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Models;

namespace IntelliFin.AdminService.Services;

public interface IConfigurationDeployer
{
    Task<string?> GetCurrentValueAsync(ConfigurationPolicy policy, CancellationToken cancellationToken);
    Task<ConfigDeploymentResult> ApplyChangeAsync(ConfigurationChange change, ConfigurationPolicy policy, CancellationToken cancellationToken);
}

public sealed record ConfigDeploymentResult(string? GitCommitSha, DateTime AppliedAt);
