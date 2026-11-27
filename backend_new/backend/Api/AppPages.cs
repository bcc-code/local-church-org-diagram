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

    private static IResult Index(HttpContext context)
    {
        var index = context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider
            .GetFileInfo("index.html");
        if (!index.Exists)
            return Results.Text("Not found", statusCode: StatusCodes.Status404NotFound);

        // index.html names the current build's assets: never use a stale copy
        context.Response.Headers.CacheControl = "no-cache";
        return Results.Stream(index.CreateReadStream(), "text/html; charset=utf-8");
    }
}
