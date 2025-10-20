using System;
using System.Collections.Generic;
using System.Threading;
using IntelliFin.AdminService.Options;
using IntelliFin.AdminService.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using FluentAssertions;
using Moq;
using VaultSharp;
using VaultSharp.V1;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp.V1.SecretsEngines.Database;
using VaultSharp.V1.SecretsEngines.KeyValue;
using VaultSharp.V1.SecretsEngines.KeyValue.V2;
using VaultSharp.V1.AuthMethods.Token;
using Xunit;

namespace IntelliFin.Tests.Unit.AdminService;

public class VaultSecretResolverTests
{
    [Fact]
    public async Task GetAdminConnectionStringAsync_UsesVaultCredentialsAndCaches()
    {
        var vaultClientMock = CreateVaultClientMock(out var databaseMock, out _, out _);
        databaseMock
            .Setup(m => m.GetCredentialsAsync("admin-service", "database"))
            .ReturnsAsync(new Secret<UsernamePasswordCredentials>
            {
                Data = new UsernamePasswordCredentials
                {
                    Username = "vault-admin",
                    Password = "vault-pass"
                },
                LeaseDurationSeconds = 600
            });

        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Server=sql;Database=Admin;TrustServerCertificate=True"
        });

        var options = Options.Create(new VaultOptions
        {
            Enabled = true,
            Token = "token",
            SecretsEnginePath = "database",
            SecretPaths = new VaultSecretPaths
            {
                RabbitMq = new VaultKeyValueSecretPath { MountPoint = "kv", Path = "messaging/audit" },
                Minio = new VaultKeyValueSecretPath { MountPoint = "kv", Path = "object-storage/admin-service" }
            }
        });

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var resolver = new VaultSecretResolver(cache, vaultClientMock.Object, options, NullLogger<VaultSecretResolver>.Instance, configuration);

        var first = await resolver.GetAdminConnectionStringAsync(CancellationToken.None);
        var second = await resolver.GetAdminConnectionStringAsync(CancellationToken.None);

        first.Should().Contain("User ID=vault-admin");
        first.Should().Contain("Password=vault-pass");
        second.Should().Be(first);
        databaseMock.Verify(m => m.GetCredentialsAsync("admin-service", "database"), Times.Once);
    }

    [Fact]
    public async Task GetRabbitMqCredentialsAsync_FallsBackToEnvironment()
    {
        Environment.SetEnvironmentVariable("AUDIT_RABBITMQ_USERNAME", "audit_user");
        Environment.SetEnvironmentVariable("AUDIT_RABBITMQ_PASSWORD", "audit_pass");

        try
        {
            var vaultClientMock = CreateVaultClientMock(out _, out _, out _);
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Server=sql;Database=Admin;TrustServerCertificate=True"
            });

            var options = Options.Create(new VaultOptions
            {
                Enabled = false,
                SecretPaths = new VaultSecretPaths
                {
                    RabbitMq = new VaultKeyValueSecretPath { MountPoint = "kv", Path = "messaging/audit" },
                    Minio = new VaultKeyValueSecretPath { MountPoint = "kv", Path = "object-storage/admin-service" }
                }
            });

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var resolver = new VaultSecretResolver(cache, vaultClientMock.Object, options, NullLogger<VaultSecretResolver>.Instance, configuration);

            var credentials = await resolver.GetRabbitMqCredentialsAsync(CancellationToken.None);

            credentials.UserName.Should().Be("audit_user");
            credentials.Password.Should().Be("audit_pass");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AUDIT_RABBITMQ_USERNAME", null);
            Environment.SetEnvironmentVariable("AUDIT_RABBITMQ_PASSWORD", null);
        }
    }

    [Fact]
    public void Constructor_Throws_WhenVaultTokenMissing()
    {
        var vaultClientMock = new Mock<IVaultClient>();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Server=sql;Database=Admin;TrustServerCertificate=True"
        });

        var options = Options.Create(new VaultOptions
        {
            Enabled = true,
            Token = null,
            SecretPaths = new VaultSecretPaths
            {
                RabbitMq = new VaultKeyValueSecretPath { MountPoint = "kv", Path = "messaging/audit" },
                Minio = new VaultKeyValueSecretPath { MountPoint = "kv", Path = "object-storage/admin-service" }
            }
        });

        using var cache = new MemoryCache(new MemoryCacheOptions());

        var act = () => new VaultSecretResolver(cache, vaultClientMock.Object, options, NullLogger<VaultSecretResolver>.Instance, configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Vault token*");
    }

    private static IConfigurationRoot BuildConfiguration(IDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static Mock<IVaultClient> CreateVaultClientMock(out Mock<IDatabaseSecretsEngine> databaseMock, out Mock<IKeyValueSecretsEngine> keyValueMock, out Mock<IKeyValueSecretsEngineV2> keyValueV2Mock)
    {
        databaseMock = new Mock<IDatabaseSecretsEngine>();
        keyValueV2Mock = new Mock<IKeyValueSecretsEngineV2>();
        keyValueMock = new Mock<IKeyValueSecretsEngine>();
        keyValueMock.SetupGet(k => k.V2).Returns(keyValueV2Mock.Object);

        var secretsEngineMock = new Mock<ISecretsEngine>();
        secretsEngineMock.SetupGet(s => s.Database).Returns(databaseMock.Object);
        secretsEngineMock.SetupGet(s => s.KeyValue).Returns(keyValueMock.Object);

        var v1Mock = new Mock<IVaultClientV1>();
        v1Mock.SetupGet(v => v.Secrets).Returns(secretsEngineMock.Object);

        var clientMock = new Mock<IVaultClient>();
        clientMock.SetupGet(c => c.V1).Returns(v1Mock.Object);
        clientMock.SetupGet(c => c.Settings).Returns(new VaultClientSettings("http://vault", new TokenAuthMethodInfo("token")));

        return clientMock;
    }
}
