using Backend.Auth;

namespace Backend.Api;

/// <summary>
/// The SPA's pages, as the Flask backend's index(): signed-out users are sent to /login (and back afterwards), and
/// /{tenantId}/admin is only for the user's own church. The SPA (index.html) is served from the web root; its assets
/// are static files.
/// </summary>
public static class AppPages
{
    public const string Unauthorized = "Unauthorized";

    public static IEndpointRouteBuilder MapAppPages(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", (HttpContext context) => Index(context))
            .RequireAuthorization()
            .ExcludeFromDescription();

        app.MapGet("/{tenantId:long}/admin", (long tenantId, HttpContext context) =>
                context.User.GetChurchId() == tenantId
                    ? Index(context)
                    : Results.Text(Unauthorized, statusCode: StatusCodes.Status403Forbidden))
            .RequireAuthorization()
            .ExcludeFromDescription();

        return app;
    }

    /// <summary>
    /// The Vite dev server (appsettings.Development.json). In Development, without a built SPA in the web root, pages
    /// redirect to the same path there: it serves the SPA (with hot reload) and proxies /api and /login back here.
    /// </summary>
    public const string DevServerSetting = "Frontend:DevServerUrl";

    private static IResult Index(HttpContext context)
    {
        var environment = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var index = environment.WebRootFileProvider.GetFileInfo("index.html");
        if (!index.Exists)
        {
            if (environment.IsDevelopment() &&
                context.RequestServices.GetRequiredService<IConfiguration>()[DevServerSetting] is { Length: > 0 } devServer)
                return Results.Redirect(devServer.TrimEnd('/') + context.Request.Path + context.Request.QueryString);
            return Results.Text("Not found", statusCode: StatusCodes.Status404NotFound);
        }

        // index.html names the current build's assets: never use a stale copy
        context.Response.Headers.CacheControl = "no-cache";
        return Results.Stream(index.CreateReadStream(), "text/html; charset=utf-8");
    }
}
