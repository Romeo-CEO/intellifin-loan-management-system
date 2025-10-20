using System;
using System.Collections.Generic;
using System.Text;
using IntelliFin.ApiGateway;
using IntelliFin.ApiGateway.Secrets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Forwarder;

namespace IntelliFin.Tests.Integration.ApiGateway;

public sealed class ApiGatewayWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string KeycloakSchemeName = "Keycloak";

    public TestForwarder Forwarder { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["Authentication:KeycloakJwt:Authority"] = TestTokens.Authority,
                ["Authentication:KeycloakJwt:Issuer"] = TestTokens.Issuer,
                ["Authentication:KeycloakJwt:Audience"] = TestTokens.Audience,
                ["Authentication:KeycloakJwt:RequireHttps"] = "false",
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=IntelliFin_Test;Trusted_Connection=True;MultipleActiveResultSets=true"
            };

            configBuilder.AddInMemoryCollection(overrides!);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISecretResolver>();
            services.AddSingleton<ISecretResolver>(new TestSecretResolver(new Dictionary<string, string?>
            {
                ["APIGATEWAY_DB_CONNECTION_STRING"] = "Server=(localdb)\\MSSQLLocalDB;Database=IntelliFin_Test;Trusted_Connection=True;MultipleActiveResultSets=true"
            }));

            services.RemoveAll<IHttpForwarder>();
            services.AddSingleton<TestForwarder>(_ => Forwarder);
            services.AddSingleton<IHttpForwarder>(sp => sp.GetRequiredService<TestForwarder>());

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestTokens.SigningKey));
            services.PostConfigure<JwtBearerOptions>(KeycloakSchemeName, options =>
            {
                var configuration = new OpenIdConnectConfiguration
                {
                    Issuer = TestTokens.Issuer
                };

                configuration.SigningKeys.Add(signingKey);

                options.Configuration = configuration;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = TestTokens.Issuer,
                    ValidateAudience = true,
                    ValidAudience = TestTokens.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ClockSkew = TimeSpan.FromSeconds(5)
                };
            });
        });
    }
}
