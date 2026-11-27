using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Backend.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Auth;

/// <summary>
/// Local development without BCC sign-on (Development only, see Program): every request is signed in as
/// <see cref="User"/>, with the claims BCC sign-on would issue. Its Global Admin role comes from an Admins row seeded
/// by EF Core (<see cref="UseLocalDevSeeding"/>), read by <see cref="RoleClaimsTransformation"/> as in production.
/// </summary>
public static class LocalDev
{
    public const string Scheme = "LocalDev";

    /// <summary>A made-up person (unknown to BCC sign-on and the Core API) in BCC København, the imported groups' tenant.</summary>
    public static readonly LocalDevUser User = new(
        PersonUid: Guid.Parse("de7de7de-0000-4000-8000-000000000001"),
        PersonId: 1,
        Name: "Dev Admin",
        Email: "dev.admin@example.com",
        ChurchId: 51,
        ChurchName: "København");

    public static IServiceCollection AddLocalDevAuthentication(this IServiceCollection services, IHostEnvironment environment)
    {
        EnsureDevelopment(environment);

        services.AddAuthentication(Scheme)
            .AddScheme<AuthenticationSchemeOptions, LocalDevAuthentication>(Scheme, null);
        services.AddAppAuthorization();

        return services;
    }

    /// <summary>
    /// GET /login?returnUrl=/path → back to the (local) path: the user is always signed in.
    /// GET /logout → / (and still signed in).
    /// </summary>
    public static IEndpointRouteBuilder MapLocalDevAuthentication(this IEndpointRouteBuilder app)
    {
        EnsureDevelopment(app.ServiceProvider.GetRequiredService<IHostEnvironment>());

        app.MapGet("/login", (string? returnUrl) => Results.Redirect(BccSignOn.LocalReturnUrl(returnUrl)))
            .ExcludeFromDescription();
        app.MapGet("/logout", () => Results.Redirect("/"))
            .ExcludeFromDescription();

        return app;
    }

    /// <summary>Seeds <see cref="User"/>'s Global Admin row when EF Core migrates (MigrateOnStartup, dotnet ef database update).</summary>
    public static DbContextOptionsBuilder UseLocalDevSeeding(this DbContextOptionsBuilder options) =>
        options
            .UseSeeding((context, _) => SeedAsync(context).GetAwaiter().GetResult())
            .UseAsyncSeeding((context, _, cancellationToken) => SeedAsync(context, cancellationToken));

    /// <summary>Inserts <see cref="User"/>'s Global Admin row if it's missing.</summary>
    public static async Task SeedAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        var admins = context.Set<Admin>();
        if (await admins.AnyAsync(a => a.PersonUid == User.PersonUid && a.TenantId == null, cancellationToken))
            return;
        admins.Add(new Admin { PersonUid = User.PersonUid, Name = User.Name, TenantId = null });
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary><see cref="User"/> with the claims BCC sign-on issues (<see cref="BccClaims"/>), before roles are added.</summary>
    public static ClaimsPrincipal Principal() =>
        new(new ClaimsIdentity(
            [
                new Claim(BccClaims.Subject, $"localdev|{User.PersonUid}"),
                new Claim(BccClaims.PersonUid, User.PersonUid.ToString()),
                new Claim(BccClaims.PersonId, User.PersonId.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer32),
                new Claim(BccClaims.HasMembership, "true", ClaimValueTypes.Boolean),
                new Claim(BccClaims.Name, User.Name),
                new Claim(BccClaims.Email, User.Email),
                new Claim(BccClaims.ChurchId, User.ChurchId.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
                new Claim(BccClaims.ChurchName, User.ChurchName),
            ],
            Scheme, BccClaims.Name, AppClaims.Role));

    internal static void EnsureDevelopment(IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException(
                $"Local dev sign-in is for Development only, not {environment.EnvironmentName}");
    }
}

public record LocalDevUser(Guid PersonUid, int PersonId, string Name, string Email, long ChurchId, string ChurchName);

/// <summary>Signs every request in as <see cref="LocalDev.User"/>.</summary>
public sealed class LocalDevAuthentication(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHostEnvironment environment)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        LocalDev.EnsureDevelopment(environment);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(LocalDev.Principal(), LocalDev.Scheme)));
    }
}
