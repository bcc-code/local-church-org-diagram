using JasperFx.CommandLine;
using Backend.Data;
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Marten and Npgsql need a syntactically valid connection string to build, but never connect
        builder.UseSetting("ConnectionStrings:Default", "Host=localhost");
        builder.UseSetting("Aspire:Npgsql:DisableHealthChecks", "true");

        builder.ConfigureTestServices(services =>
        {
            services.DisableAllWolverineMessagePersistence();
            services.DisableAllExternalWolverineTransports();

            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService)
                         && (d.ImplementationType?.Name == "ResourceSetupHostService"
                             || d.ImplementationType == typeof(MigrateOnStartup))).ToList())
                services.Remove(descriptor);
        });
    }
}
