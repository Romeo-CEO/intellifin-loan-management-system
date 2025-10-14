using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace IntelliFin.Tests.Unit.AdminService;

public class AdminServiceVaultStartupTests
{
    [Fact]
    public void StartupFailsWhenVaultTokenMissing()
    {
        using var factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Vault:Enabled"] = "true",
                        ["Vault:Token"] = "",
                        ["ConnectionStrings:Default"] = "Server=(localdb)\\mssqllocaldb;Database=Test;Trusted_Connection=True",
                        ["Vault:SecretPaths:AdminDatabaseRole"] = "admin-service",
                        ["Vault:SecretPaths:FinancialDatabaseRole"] = "financial-service",
                        ["Vault:SecretPaths:IdentityDatabaseRole"] = "identity-service",
                        ["Vault:SecretPaths:RabbitMq:MountPoint"] = "kv",
                        ["Vault:SecretPaths:RabbitMq:Path"] = "messaging/audit",
                        ["Vault:SecretPaths:Minio:MountPoint"] = "kv",
                        ["Vault:SecretPaths:Minio:Path"] = "object-storage/admin-service"
                    });
                });
            });

        Action act = () => factory.CreateClient();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Vault token*");
    }
}
