using JasperFx.CommandLine;
using Backend.Data;
using Backend.Tests.Auth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace Backend.Tests;

/// <summary>
/// Hosts the backend in memory without Postgres: Wolverine persistence and transports are disabled, Marten/Wolverine
/// schema setup and EF Core migrations are skipped, and "/health" doesn't check Postgres. Runs as "Testing".
/// For tests against a real database, see <see cref="Database.DatabaseBackendFactory"/>.
/// </summary>
public class BackendFactory : WebApplicationFactory<Program>
{
    // Program ends with RunJasperFxCommands, which only starts the host the factory intercepts when told to
    static BackendFactory() => JasperFxEnvironment.AutoStartHost = true;

    public const string TestClientId = "test-client-id";
    private const string BccSignOnScheme = Backend.Auth.BccSignOn.Scheme;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Marten and Npgsql need a syntactically valid connection string to build, but never connect
        builder.UseSetting("ConnectionStrings:Default", "Host=localhost");
        builder.UseSetting("Aspire:Npgsql:DisableHealthChecks", "true");

        builder.UseSetting("BccSignOn:ClientId", TestClientId);
        builder.UseSetting("BccSignOn:ClientSecret", "test-client-secret");

        builder.ConfigureTestServices(services =>
        {
            services.DisableAllWolverineMessagePersistence();
            services.DisableAllExternalWolverineTransports();

            // Test-only endpoints (Auth/AuthTestEndpoints)
            services.ConfigureWolverine(options => options.Discovery.IncludeAssembly(typeof(BackendFactory).Assembly));

            // The Data Protection key ring lives in Postgres; keep it in memory instead (sign-in state, session cookies)
            services.AddDataProtection().UseEphemeralDataProtectionProvider();

            // BCC sign-on's metadata, so a sign-in challenge doesn't fetch it from login.bcc.no
            services.PostConfigure<OpenIdConnectOptions>(BccSignOnScheme, options =>
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = "https://login.bcc.no/",
                    AuthorizationEndpoint = "https://login.bcc.no/authorize",
                    TokenEndpoint = "https://login.bcc.no/oauth/token",
                });

            // Requests with TestUser's header are signed in as that user (factory.CreateClient(TestUser))
            TestUserAuthentication.Register(services);

            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService)
                         && (d.ImplementationType?.Name == "ResourceSetupHostService"
                             || d.ImplementationType == typeof(MigrateOnStartup))).ToList())
                services.Remove(descriptor);
        });
    }
}
