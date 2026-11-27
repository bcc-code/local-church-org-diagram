using Backend.Tests.Auth;
using Backend.Tests.CoreApi;
using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Npgsql;
using Wolverine;

namespace Backend.Tests.Database;

/// <summary>
/// Hosts the backend against a real Postgres: a fresh database, <see cref="DatabaseName"/>, is created on the server
/// that <c>TEST_DATABASE_URL</c> points to (CI provides a postgres service), so startup creates every schema object
/// (Marten, Wolverine, EF Core migrations) from scratch. Runs as "Testing", with <see cref="CoreApi"/> for the Core
/// API and <see cref="TestUserAuthentication"/> signing requests in. Tests share one instance through
/// <see cref="DatabaseCollection"/> and use <see cref="DatabaseFactAttribute"/>, which skips them without
/// <c>TEST_DATABASE_URL</c>.
/// </summary>
public class DatabaseBackendFactory : WebApplicationFactory<Program>
{
    public const string DatabaseName = "org_diagram_test";

    static DatabaseBackendFactory() => JasperFxEnvironment.AutoStartHost = true;

    public string ConnectionString { get; } = CreateDatabase();

    /// <summary>The Core API; tests in <see cref="DatabaseCollection"/> run one at a time, so each sets what it needs.</summary>
    public CoreApiStub CoreApi { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseCoreApi(CoreApi, CoreApiTestHost.UniqueClientId(), "test-secret");
        // Requests without a TestUser get the app's cookie scheme, whose 401 still needs BCC sign-on configured
        builder.UseSetting("BccSignOn:ClientId", BackendFactory.TestClientId);
        builder.UseSetting("BccSignOn:ClientSecret", "test-client-secret");

        builder.ConfigureTestServices(services =>
        {
            // Test-only messages, handlers and documents
            services.ConfigureWolverine(options => options.Discovery.IncludeAssembly(typeof(DatabaseBackendFactory).Assembly));
            // Requests with TestUser's header are signed in as that user (factory.CreateClient(TestUser))
            TestUserAuthentication.Register(services);
        });
    }

    private static string CreateDatabase()
    {
        var server = TestDatabase.ServerConnectionString()
            ?? throw new InvalidOperationException($"{TestDatabase.Variable} is not set");

        using (var connection = new NpgsqlConnection(server))
        {
            connection.Open();
            using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE)", connection);
            drop.ExecuteNonQuery();
            using var create = new NpgsqlCommand($"CREATE DATABASE {DatabaseName}", connection);
            create.ExecuteNonQuery();
        }

        return new NpgsqlConnectionStringBuilder(server) { Database = DatabaseName }.ConnectionString;
    }
}

public static class TestDatabase
{
    public const string Variable = "TEST_DATABASE_URL";

    /// <summary>
    /// <c>TEST_DATABASE_URL</c> (a <c>postgresql://</c> URL, as for the Flask backend's tests) as an Npgsql connection
    /// string, or null when it isn't set. The tests drop and create a database on that server, so only localhost or
    /// a database whose name contains "test" is accepted.
    /// </summary>
    public static string? ServerConnectionString()
    {
        var url = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var uri = new Uri(url);
        var database = uri.AbsolutePath.Trim('/') is { Length: > 0 } path ? Uri.UnescapeDataString(path) : "postgres";
        if (uri.Host is not ("localhost" or "127.0.0.1" or "::1") && !database.Contains("test"))
            throw new InvalidOperationException(
                $"{Variable} must point to localhost or a database named *test*: the tests drop and create databases");

        var userInfo = uri.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            Database = database,
        }.ConnectionString;
    }
}

/// <summary>A test against a real Postgres (<see cref="DatabaseBackendFactory"/>): skipped without <c>TEST_DATABASE_URL</c>.</summary>
public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(TestDatabase.Variable)))
            Skip = $"{TestDatabase.Variable} is not set";
    }
}

/// <summary>
/// Test classes against the database share one <see cref="DatabaseBackendFactory"/> and run one after another: each
/// factory drops and creates the same database.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseBackendFactory>
{
    public const string Name = "Database";
}

/// <summary><see cref="DatabaseFactAttribute"/> for a theory.</summary>
public sealed class DatabaseTheoryAttribute : TheoryAttribute
{
    public DatabaseTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(TestDatabase.Variable)))
            Skip = $"{TestDatabase.Variable} is not set";
    }
}
