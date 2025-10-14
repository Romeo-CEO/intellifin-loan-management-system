using Microsoft.Extensions.Configuration;

namespace IntelliFin.IdentityService.Tests.Unit.Configuration;

public class ConfigurationTests
{
    private static string GetAppSettingsDirectory()
    {
        var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        return Path.Combine(basePath, "apps", "IntelliFin.IdentityService");
    }

    [Fact]
    public void Configuration_LoadsConnectionStringFromAppSettings()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(GetAppSettingsDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("IdentityDb");

        Assert.Equal("Server=localhost;Database=IntellifinIdentity;Trusted_Connection=True;TrustServerCertificate=True;", connectionString);
    }

    [Fact]
    public void Configuration_AllowsEnvironmentVariableOverrides()
    {
        const string variableName = "IDENTITY_ConnectionStrings__IdentityDb";
        const string expected = "Server=override;Database=Identity;User Id=test;Password=test;";

        try
        {
            Environment.SetEnvironmentVariable(variableName, expected);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(GetAppSettingsDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables(prefix: "IDENTITY_")
                .Build();

            var connectionString = configuration.GetConnectionString("IdentityDb");
            Assert.Equal(expected, connectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }
}
