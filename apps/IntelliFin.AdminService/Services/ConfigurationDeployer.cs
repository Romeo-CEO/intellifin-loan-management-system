using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using k8s;
using k8s.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class ConfigurationDeployer : IConfigurationDeployer, IDisposable
{
    private readonly IOptionsMonitor<ConfigurationManagementOptions> _optionsMonitor;
    private readonly ILogger<ConfigurationDeployer> _logger;
    private IKubernetes? _kubernetesClient;
    private bool _disposed;

    public ConfigurationDeployer(IOptionsMonitor<ConfigurationManagementOptions> optionsMonitor, ILogger<ConfigurationDeployer> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<string?> GetCurrentValueAsync(ConfigurationPolicy policy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(policy.KubernetesNamespace) ||
            string.IsNullOrWhiteSpace(policy.KubernetesConfigMap) ||
            string.IsNullOrWhiteSpace(policy.ConfigMapKey))
        {
            return policy.CurrentValue;
        }

        try
        {
            var client = GetKubernetesClient();
            var configMap = await client.ReadNamespacedConfigMapAsync(policy.KubernetesConfigMap, policy.KubernetesNamespace, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (configMap?.Data != null && configMap.Data.TryGetValue(policy.ConfigMapKey, out var value))
            {
                return value;
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to read configuration value for {ConfigKey} from ConfigMap {Namespace}/{ConfigMap}", policy.ConfigKey, policy.KubernetesNamespace, policy.KubernetesConfigMap);
        }

        return policy.CurrentValue;
    }

    public async Task<ConfigDeploymentResult> ApplyChangeAsync(ConfigurationChange change, ConfigurationPolicy policy, CancellationToken cancellationToken)
    {
        await ApplyToKubernetesAsync(change, policy, cancellationToken).ConfigureAwait(false);
        var commitSha = await CommitToGitAsync(change, policy, cancellationToken).ConfigureAwait(false);
        return new ConfigDeploymentResult(commitSha, DateTime.UtcNow);
    }

    private async Task ApplyToKubernetesAsync(ConfigurationChange change, ConfigurationPolicy policy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(policy.KubernetesNamespace) ||
            string.IsNullOrWhiteSpace(policy.KubernetesConfigMap) ||
            string.IsNullOrWhiteSpace(policy.ConfigMapKey))
        {
            _logger.LogDebug("Skipping Kubernetes deployment for {ConfigKey} because policy is missing namespace or configmap", policy.ConfigKey);
            return;
        }

        try
        {
            var client = GetKubernetesClient();
            var configMap = await client.ReadNamespacedConfigMapAsync(policy.KubernetesConfigMap, policy.KubernetesNamespace, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (configMap == null)
            {
                configMap = new V1ConfigMap
                {
                    Metadata = new V1ObjectMeta
                    {
                        Name = policy.KubernetesConfigMap,
                        NamespaceProperty = policy.KubernetesNamespace
                    },
                    Data = new System.Collections.Generic.Dictionary<string, string>
                    {
                        [policy.ConfigMapKey] = change.NewValue
                    }
                };

                await client.CreateNamespacedConfigMapAsync(configMap, policy.KubernetesNamespace, cancellationToken: cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Created new ConfigMap {Namespace}/{ConfigMap} with key {Key}", policy.KubernetesNamespace, policy.KubernetesConfigMap, policy.ConfigMapKey);
            }
            else
            {
                configMap.Data ??= new System.Collections.Generic.Dictionary<string, string>();
                configMap.Data[policy.ConfigMapKey] = change.NewValue;
                await client.ReplaceNamespacedConfigMapAsync(configMap, policy.KubernetesNamespace, policy.KubernetesConfigMap, cancellationToken: cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Updated ConfigMap {Namespace}/{ConfigMap} for key {Key}", policy.KubernetesNamespace, policy.KubernetesConfigMap, policy.ConfigMapKey);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Failed to apply configuration change {ChangeId} to Kubernetes", change.ChangeRequestId);
            throw;
        }
    }

    private async Task<string?> CommitToGitAsync(ConfigurationChange change, ConfigurationPolicy policy, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.GitRepository) || string.IsNullOrWhiteSpace(options.LocalRepoPath))
        {
            _logger.LogDebug("Git repository configuration missing; skipping commit for {ConfigKey}", change.ConfigKey);
            return null;
        }

        var repositoryPath = await EnsureRepositoryAsync(options, cancellationToken).ConfigureAwait(false);
        var configDirectory = Path.Combine(repositoryPath, "config");
        Directory.CreateDirectory(configDirectory);
        var filePath = Path.Combine(configDirectory, $"{policy.KubernetesConfigMap ?? policy.ConfigKey}.yaml");

        var content = File.Exists(filePath) ? await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false) : string.Empty;
        var updatedContent = UpsertYamlValue(content, policy.ConfigMapKey ?? policy.ConfigKey, change.NewValue);
        if (!string.Equals(content, updatedContent, StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(filePath, updatedContent, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }

        using var repository = new Repository(repositoryPath);
        var relativePath = Path.GetRelativePath(repository.Info.WorkingDirectory, filePath);
        Commands.Stage(repository, relativePath);

        if (!repository.RetrieveStatus().IsDirty)
        {
            _logger.LogDebug("No Git changes detected for {ConfigKey}; skipping commit", change.ConfigKey);
            return change.GitCommitSha;
        }

        var authorName = string.IsNullOrWhiteSpace(options.GitAuthorName) ? options.GitUsername ?? "IntelliFin Admin" : options.GitAuthorName;
        var authorEmail = string.IsNullOrWhiteSpace(options.GitAuthorEmail) ? "admin@intellifin.local" : options.GitAuthorEmail;
        var author = new Signature(authorName!, authorEmail!, DateTimeOffset.UtcNow);
        var committer = author;

        var commitMessage = BuildCommitMessage(change);
        var commit = repository.Commit(commitMessage, author, committer);

        var pushOptions = new PushOptions();
        if (!string.IsNullOrWhiteSpace(options.GitUsername) && !string.IsNullOrWhiteSpace(options.GitToken))
        {
            pushOptions.CredentialsProvider = (_, _, _) => new UsernamePasswordCredentials
            {
                Username = options.GitUsername,
                Password = options.GitToken
            };
        }

        try
        {
            repository.Network.Push(repository.Head, pushOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push configuration change commit {CommitId} to remote", commit.Sha);
        }

        return commit.Sha;
    }

    private async Task<string> EnsureRepositoryAsync(ConfigurationManagementOptions options, CancellationToken cancellationToken)
    {
        var path = options.LocalRepoPath!;
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        if (!Repository.IsValid(path))
        {
            try
            {
                var cloneOptions = new CloneOptions
                {
                    BranchName = options.GitBranch,
                    CredentialsProvider = string.IsNullOrWhiteSpace(options.GitUsername) || string.IsNullOrWhiteSpace(options.GitToken)
                        ? null
                        : (_, _, _) => new UsernamePasswordCredentials
                        {
                            Username = options.GitUsername,
                            Password = options.GitToken
                        }
                };

                Repository.Clone(options.GitRepository, path, cloneOptions);
                _logger.LogInformation("Cloned configuration repository {Repository} into {Path}", options.GitRepository, path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clone repository {Repository}. Falling back to local-only operations", options.GitRepository);
            }
        }
        else
        {
            try
            {
                using var repository = new Repository(path);
                var remote = repository.Network.Remotes["origin"];
                if (remote != null)
                {
                    Commands.Fetch(repository, remote.Name, Array.Empty<string>(), new FetchOptions
                    {
                        CredentialsProvider = string.IsNullOrWhiteSpace(options.GitUsername) || string.IsNullOrWhiteSpace(options.GitToken)
                            ? null
                            : (_, _, _) => new UsernamePasswordCredentials
                            {
                                Username = options.GitUsername,
                                Password = options.GitToken
                            }
                    }, null);
                }

                if (!string.IsNullOrWhiteSpace(options.GitBranch))
                {
                    var branch = repository.Branches[options.GitBranch] ?? repository.Branches[$"origin/{options.GitBranch}"];
                    if (branch != null)
                    {
                        Commands.Checkout(repository, branch);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh repository {Repository}", options.GitRepository);
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
        return path;
    }

    private IKubernetes GetKubernetesClient()
    {
        if (_kubernetesClient != null)
        {
            return _kubernetesClient;
        }

        var options = _optionsMonitor.CurrentValue;
        KubernetesClientConfiguration config;
        if (options.InCluster)
        {
            config = KubernetesClientConfiguration.InClusterConfig();
        }
        else if (!string.IsNullOrWhiteSpace(options.KubeConfigPath) && File.Exists(options.KubeConfigPath))
        {
            config = KubernetesClientConfiguration.BuildConfigFromConfigFile(options.KubeConfigPath);
        }
        else
        {
            config = KubernetesClientConfiguration.BuildDefaultConfig();
        }

        _kubernetesClient = new Kubernetes(config);
        return _kubernetesClient;
    }

    private static string BuildCommitMessage(ConfigurationChange change)
    {
        var oldValue = string.IsNullOrEmpty(change.OldValue) ? "<empty>" : TrimForCommit(change.OldValue!);
        var newValue = string.IsNullOrEmpty(change.NewValue) ? "<empty>" : TrimForCommit(change.NewValue);
        var message = $"[ConfigChange] {change.ConfigKey}: {oldValue} -> {newValue}";
        if (!string.IsNullOrWhiteSpace(change.ApprovedBy))
        {
            message += $" (Approved by {change.ApprovedBy})";
        }

        return message;
    }

    private static string TrimForCommit(string value)
    {
        var normalized = value.Replace('\n', ' ').Replace('\r', ' ');
        return normalized.Length > 40 ? normalized[..40] + "…" : normalized;
    }

    private static string UpsertYamlValue(string content, string key, string value)
    {
        var lines = string.IsNullOrWhiteSpace(content)
            ? new System.Collections.Generic.List<string>()
            : content.Split('\n').ToList();

        var index = lines.FindIndex(l => l.TrimStart().StartsWith(key + ":", StringComparison.Ordinal));
        var newLine = $"{key}: {value}";
        if (index >= 0)
        {
            lines[index] = newLine;
        }
        else
        {
            lines.Add(newLine);
        }

        return string.Join('\n', lines);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _kubernetesClient?.Dispose();
        _disposed = true;
    }
}
