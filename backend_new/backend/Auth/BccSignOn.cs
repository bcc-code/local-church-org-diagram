using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Backend.Auth;

/// <summary>
/// Users sign in with BCC sign-on (OpenID Connect, authorization code flow) and stay signed in with a cookie session,
/// as with the Flask backend: the SPA needs no tokens.
/// </summary>
public static class BccSignOn
{
    public const string Scheme = "BccSignOn";

    /// <summary>The Flask backend's callback, already registered on the OIDC client both backends share.</summary>
    public const string CallbackPath = "/authorize";

    /// <summary>How long a session lasts without activity; each request past the halfway point renews it.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    public static IServiceCollection AddBccSignOn(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = BccSignOnSettings.From(configuration);

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "org-diagram.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                // Pages redirect to GET /login?returnUrl=<path>, the only route that starts a sign-in. API calls get a
                // status code to act on instead of a redirect that fetch would follow silently (and an ApiError body,
                // from Api/ApiConventions, when the authorization middleware fails them).
                options.LoginPath = "/login";
                options.ReturnUrlParameter = "returnUrl";
                options.ExpireTimeSpan = SessionLifetime;
                options.SlidingExpiration = true;
                // Persistent, so the session survives closing the browser
                options.Events.OnSigningIn = context =>
                {
                    context.Properties.IsPersistent = true;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToLogin = context => RedirectOrStatus(context, StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = context => RedirectOrStatus(context, StatusCodes.Status403Forbidden);
            })
            .AddOpenIdConnect(Scheme, options =>
            {
                options.Authority = settings.Authority;
                options.ClientId = settings.ClientId;
                options.ClientSecret = settings.ClientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.CallbackPath = CallbackPath;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("church");

                // Keep the raw claim names (see BccClaims) instead of mapping them to ClaimTypes.*
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = BccClaims.Name;
                options.TokenValidationParameters.RoleClaimType = AppClaims.Role;
                // The ID token doesn't carry every claim of the requested scopes; userinfo does
                options.GetClaimsFromUserInfoEndpoint = true;
                // Nothing calls an API with the user's tokens, so keep them out of the cookie
                options.SaveTokens = false;

                options.Events = new OpenIdConnectEvents { OnRemoteFailure = HandleRemoteFailure };
            });

        services.AddAppAuthorization();

        return services;
    }

    /// <summary>
    /// GET /login?returnUrl=/path → signs in with BCC sign-on, then returns to the (local) path, or / by default.
    /// GET /logout → ends the session here (not at BCC sign-on, as with the Flask backend), then goes to /.
    /// </summary>
    public static IEndpointRouteBuilder MapBccSignOn(this IEndpointRouteBuilder app)
    {
        app.MapGet("/login", (string? returnUrl) =>
                Results.Challenge(new AuthenticationProperties { RedirectUri = LocalReturnUrl(returnUrl) }, [Scheme]))
            .ExcludeFromDescription();

        app.MapGet("/logout", () => Results.SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                [CookieAuthenticationDefaults.AuthenticationScheme]))
            .ExcludeFromDescription();

        return app;
    }

    /// <summary>The return URL if it is a path on this site, otherwise / (prevents open redirects).</summary>
    public static string LocalReturnUrl(string? returnUrl) =>
        returnUrl is ['/', ..] && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\") ? returnUrl : "/";

    /// <summary>
    /// A callback that can't complete (the user cancelled at BCC sign-on, a stale or replayed callback, cookies the
    /// browser didn't send back) answers with a short page linking to a new sign-in, instead of a 500 page. Not a
    /// redirect: the SPA sends every 401 to /login, so a callback that keeps failing would loop.
    /// </summary>
    private static async Task HandleRemoteFailure(RemoteFailureContext context)
    {
        var returnUrl = LocalReturnUrl(context.Properties?.RedirectUri);
        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(BccSignOn))
            .LogWarning(context.Failure, "BCC sign-on callback failed (return URL {ReturnUrl})", returnUrl);
        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "text/html; charset=utf-8";
        var loginUrl = HtmlEncoder.Default.Encode(QueryHelpers.AddQueryString("/login", "returnUrl", returnUrl));
        await context.Response.WriteAsync(
            $"<!doctype html><title>Sign-in failed</title><p>Sign-in failed. <a href=\"{loginUrl}\">Try again</a></p>");
    }

    private static Task RedirectOrStatus(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.StatusCode = statusCode;
        else
            context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
}
