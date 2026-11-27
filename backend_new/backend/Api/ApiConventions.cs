using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api;

/// <summary>An /api error, in the Flask backend's shape: <c>{"error": "..."}</c>.</summary>
public record ApiError(string Error);

/// <summary>
/// The /api routes answer like the Flask backend did, so the frontend works against either: snake_case JSON
/// (<c>group_id</c>, <c>member_count</c>, ...), and <see cref="ApiError"/> bodies for every error, including the
/// problems Wolverine.Http endpoints return (validation, authorization) and 401/403 from the authorization middleware.
/// </summary>
public static class ApiConventions
{
    public const string ErrorContentType = "application/json";
    public const string AuthenticationRequired = "Authentication required";
    public const string Forbidden = "Forbidden";

    public static bool IsApiRequest(this HttpRequest request) => request.Path.StartsWithSegments("/api");

    public static IServiceCollection AddApiConventions(this IServiceCollection services)
    {
        // Minimal APIs, Wolverine.Http and the OpenAPI document all use these options
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        // Ahead of AddProblemDetails' default writer, which then handles everything outside /api
        services.AddSingleton<IProblemDetailsWriter, ApiErrorWriter>();
        services.AddProblemDetails();

        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();
        return services;
    }

    public static Task WriteApiErrorAsync(this HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new ApiError(message), ApiJson(context), ErrorContentType);
    }

    /// <summary>The message of a problem: its first validation error, otherwise its detail or title.</summary>
    public static string Message(ProblemDetails problem) =>
        (problem as HttpValidationProblemDetails)?.Errors.Values.SelectMany(e => e).FirstOrDefault()
        ?? problem.Detail
        ?? problem.Title
        ?? "Error";

    private static JsonSerializerOptions ApiJson(HttpContext context) =>
        context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value.SerializerOptions;

    /// <summary>Writes the problems of /api requests as <see cref="ApiError"/>s.</summary>
    private sealed class ApiErrorWriter : IProblemDetailsWriter
    {
        public bool CanWrite(ProblemDetailsContext context) => context.HttpContext.Request.IsApiRequest();

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            var statusCode = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
            return new ValueTask(context.HttpContext.WriteApiErrorAsync(statusCode, Message(context.ProblemDetails)));
        }
    }

    /// <summary>
    /// A failed authorization of an /api request answers 401 or 403 with an <see cref="ApiError"/> (fetch would follow
    /// a sign-in redirect silently); a page's sign-in challenge still redirects to /login. A page's forbidden answers
    /// a plain 403: there is no access-denied page.
    /// </summary>
    private sealed class ApiAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _default = new();

        public Task HandleAsync(
            RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
        {
            if (result.Challenged && context.Request.IsApiRequest())
                return context.WriteApiErrorAsync(StatusCodes.Status401Unauthorized, AuthenticationRequired);
            if (result.Forbidden)
            {
                if (context.Request.IsApiRequest())
                    return context.WriteApiErrorAsync(StatusCodes.Status403Forbidden, Forbidden);
                return Results.Text(Forbidden, statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(context);
            }

            return _default.HandleAsync(next, context, policy, result);
        }
    }
}
