using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Backend.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Tests.Auth;

/// <summary>
/// A signed-in user for tests: the claims BCC sign-on issues (<see cref="Claims"/>), plus the roles
/// <see cref="RoleClaimsTransformation"/> would add from Admins rows (<see cref="GlobalAdmin"/>, <see cref="AdminOf"/>),
/// so tests without a database don't need the table.
/// </summary>
public sealed record TestUser(
    Guid PersonUid,
    long? ChurchId,
    bool HasMembership = true,
    bool GlobalAdmin = false,
    long[]? AdminOf = null)
{
    public const long Copenhagen = 51;

    public static TestUser Member(long? churchId = Copenhagen) => new(Guid.NewGuid(), churchId);

    public static TestUser NonMember(long? churchId = Copenhagen) => new(Guid.NewGuid(), churchId, HasMembership: false);

    public static TestUser LocalAdmin(long tenantId, long? churchId = null) =>
        new(Guid.NewGuid(), churchId ?? tenantId, AdminOf: [tenantId]);

    public static TestUser Global(long? churchId = Copenhagen) => new(Guid.NewGuid(), churchId, GlobalAdmin: true);

    /// <summary>BCC sign-on's claims only, as they arrive before <see cref="RoleClaimsTransformation"/>.</summary>
    public IEnumerable<Claim> Claims()
    {
        yield return new Claim(BccClaims.Subject, $"test|{PersonUid}");
        yield return new Claim(BccClaims.PersonUid, PersonUid.ToString());
        yield return new Claim(BccClaims.HasMembership, HasMembership ? "true" : "false", ClaimValueTypes.Boolean);
        yield return new Claim(BccClaims.Name, "Test User");
        if (ChurchId is { } churchId)
            yield return new Claim(BccClaims.ChurchId, churchId.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64);
    }

    /// <summary>What <see cref="RoleClaimsTransformation"/> adds for this user's Admins rows.</summary>
    public IEnumerable<Claim> RoleClaims()
    {
        if (GlobalAdmin)
            yield return new Claim(AppClaims.Role, AppRoles.GlobalAdmin);
        if (AdminOf is { Length: > 0 })
        {
            yield return new Claim(AppClaims.Role, AppRoles.LocalAdmin);
            foreach (var tenantId in AdminOf)
                yield return new Claim(AppClaims.AdminTenant, tenantId.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>The principal after <see cref="RoleClaimsTransformation"/>, for unit tests of policies and handlers.</summary>
    public ClaimsPrincipal Principal() =>
        new(new ClaimsIdentity(Claims().Concat(RoleClaims()), "Test", BccClaims.Name, AppClaims.Role));

    public string Header() =>
        string.Join(';', PersonUid, ChurchId?.ToString(CultureInfo.InvariantCulture) ?? "", HasMembership, GlobalAdmin,
            string.Join(',', AdminOf ?? []));

    public static TestUser FromHeader(string header)
    {
        var parts = header.Split(';');
        return new TestUser(
            Guid.Parse(parts[0]),
            parts[1].Length > 0 ? long.Parse(parts[1], CultureInfo.InvariantCulture) : null,
            bool.Parse(parts[2]),
            bool.Parse(parts[3]),
            parts[4].Length > 0 ? parts[4].Split(',').Select(id => long.Parse(id, CultureInfo.InvariantCulture)).ToArray() : []);
    }
}

/// <summary>
/// Signs in the <see cref="TestUser"/> in a request's <see cref="Header"/>; requests without it go to the app's own
/// cookie scheme, so sign-in redirects and 401s behave as in production.
/// </summary>
public sealed class TestUserAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string SelectorScheme = "TestOrCookie";
    public const string Header = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Header, out var header))
            return Task.FromResult(AuthenticateResult.NoResult());

        var user = TestUser.FromHeader(header.ToString());
        var identity = new ClaimsIdentity(user.Claims().Concat(user.RoleClaims()), SchemeName, BccClaims.Name, AppClaims.Role);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    /// <summary>
    /// Adds the test scheme behind a selector that picks it for requests with the <see cref="Header"/>, and drops
    /// <see cref="RoleClaimsTransformation"/> (it needs the database): the header carries the roles instead.
    /// </summary>
    public static void Register(IServiceCollection services)
    {
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, TestUserAuthentication>(SchemeName, null)
            .AddPolicyScheme(SelectorScheme, null, options => options.ForwardDefaultSelector = context =>
                context.Request.Headers.ContainsKey(Header) ? SchemeName : CookieAuthenticationDefaults.AuthenticationScheme);
        services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultScheme = SelectorScheme;
            options.DefaultChallengeScheme = SelectorScheme;
        });
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IClaimsTransformation)).ToList())
            services.Remove(descriptor);
        services.AddSingleton<IClaimsTransformation, NoopClaimsTransformation>();
    }
}

public static class TestAuthClientExtensions
{
    /// <summary>
    /// A client whose requests are signed in as the user (see <see cref="TestUserAuthentication"/>), for any factory
    /// that registers it (<see cref="BackendFactory"/>, <see cref="Database.DatabaseBackendFactory"/>).
    /// </summary>
    public static HttpClient CreateClient(
        this Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, TestUser user)
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add(TestUserAuthentication.Header, user.Header());
        return client;
    }
}
