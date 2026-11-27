using System.Net;
using Backend.Auth;
using Backend.Data;
using Backend.Tests.Api;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Backend.Tests.Auth;

/// <summary>Development without BCC sign-on credentials (user secrets could hold some: they're cleared).</summary>
public class DevelopmentBackendFactory : BackendFactory
{
    protected override string Environment => Environments.Development;

    protected override void ConfigureSignIn(IWebHostBuilder builder)
    {
        WithoutBccSignOnCredentials(builder);
        // Roles come from the Admins table, which needs Postgres (Database/LocalDevSeedingTests)
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IClaimsTransformation)).ToList())
                services.Remove(descriptor);
            services.AddSingleton<IClaimsTransformation, NoopClaimsTransformation>();
        });
    }

    public static void WithoutBccSignOnCredentials(IWebHostBuilder builder)
    {
        builder.UseSetting($"{BccSignOnSettings.Section}:ClientId", "");
        builder.UseSetting($"{BccSignOnSettings.Section}:ClientSecret", "");
        builder.UseSetting(BccSignOnSettings.LegacyClientIdVariable, "");
        builder.UseSetting(BccSignOnSettings.LegacySecretVariable, "");
    }
}

/// <summary>
/// Production, with BCC sign-on configured as in <see cref="BackendFactory"/>. Marten and Wolverine code is generated
/// at runtime (production loads it pre-generated) and the database schema isn't touched.
/// </summary>
public class ProductionBackendFactory : BackendFactory
{
    protected override string Environment => Environments.Production;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.CritterStackDefaults(defaults =>
        {
            defaults.Production.GeneratedCodeMode = TypeLoadMode.Dynamic;
            defaults.Production.AssertAllPreGeneratedTypesExist = false;
            defaults.Production.ResourceAutoCreate = AutoCreate.None;
        }));
    }
}

public class ProductionWithoutCredentialsFactory : ProductionBackendFactory
{
    protected override void ConfigureSignIn(IWebHostBuilder builder)
    {
        base.ConfigureSignIn(builder);
        DevelopmentBackendFactory.WithoutBccSignOnCredentials(builder);
    }
}

public class LocalDevTests(DevelopmentBackendFactory factory) : IClassFixture<DevelopmentBackendFactory>
{
    private HttpClient Anonymous() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Every_request_is_signed_in_as_the_dev_user()
    {
        var response = await Anonymous().GetAsync("/api/test/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadApiAsync<WhoAmI>())!.PersonUid.ShouldBe(LocalDev.User.PersonUid);
    }

    [Fact]
    public void The_dev_user_has_BCC_sign_on_claims_in_the_imported_groups_tenant()
    {
        var user = LocalDev.Principal();

        user.GetPersonUid().ShouldBe(LocalDev.User.PersonUid);
        user.GetPersonId().ShouldBe(LocalDev.User.PersonId);
        user.GetChurchId().ShouldBe(51);
        user.GetHasMembership().ShouldBeTrue();
        user.Identity!.Name.ShouldBe("Dev Admin");
        // The role comes from the seeded Admins row, not from the principal
        user.IsGlobalAdmin().ShouldBeFalse();
    }

    [Fact]
    public async Task BCC_sign_on_is_not_registered()
    {
        var schemes = factory.Services.GetRequiredService<IAuthenticationSchemeProvider>();

        (await schemes.GetSchemeAsync(BccSignOn.Scheme)).ShouldBeNull();
        (await schemes.GetDefaultAuthenticateSchemeAsync())!.Name.ShouldBe(LocalDev.Scheme);
    }

    [Theory]
    [InlineData("/login?returnUrl=/51/admin", "/51/admin")]
    [InlineData("/login?returnUrl=https://evil.example", "/")]
    [InlineData("/login", "/")]
    [InlineData("/logout", "/")]
    public async Task Login_and_logout_go_straight_back(string path, string location)
    {
        var response = await Anonymous().GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe(location);
    }

    // Without a built SPA in the web root, the Vite dev server (appsettings.Development.json) serves it
    [Theory]
    [InlineData("/", "http://localhost:5173/")]
    [InlineData("/51/admin?group=1", "http://localhost:5173/51/admin?group=1")]
    public async Task Pages_redirect_to_the_Vite_dev_server(string path, string location)
    {
        var response = await Anonymous().GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe(location);
    }

    [Fact]
    public async Task Another_tenants_admin_page_is_still_forbidden()
    {
        (await Anonymous().GetAsync("/1/admin")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_sign_in_callback_is_not_mapped()
    {
        (await Anonymous().GetAsync("/authorize?code=abc")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public void Migrating_seeds_the_dev_users_Admins_row()
    {
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();

        options.FindExtension<CoreOptionsExtension>()!.AsyncSeeder.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void Refuses_to_register_outside_Development(string environmentName)
    {
        var environment = new TestEnvironment(environmentName);

        Should.Throw<InvalidOperationException>(() => new ServiceCollection().AddLocalDevAuthentication(environment));
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "backend";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

public class ProductionSignInTests(ProductionBackendFactory factory) : IClassFixture<ProductionBackendFactory>
{
    [Fact]
    public async Task Production_signs_in_with_BCC_sign_on_only()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync("/api/test/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var login = await client.GetAsync("/login");
        login.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        login.Headers.Location!.GetLeftPart(UriPartial.Path).ShouldBe("https://login.bcc.no/authorize");

        var schemes = factory.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        (await schemes.GetSchemeAsync(LocalDev.Scheme)).ShouldBeNull();
        (await schemes.GetSchemeAsync(BccSignOn.Scheme)).ShouldNotBeNull();
    }

    [Fact]
    public void Production_without_BCC_sign_on_credentials_fails_to_start()
    {
        using var withoutCredentials = new ProductionWithoutCredentialsFactory();

        var exception = Should.Throw<OptionsValidationException>(() => withoutCredentials.CreateClient());

        exception.Message.ShouldContain("BccSignOn:ClientId");
    }
}
