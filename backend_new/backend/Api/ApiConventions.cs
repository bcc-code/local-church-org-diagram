using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api;

/// <summary>An /api error, in the Flask backend's shape: <c>{"error": "..."}</c>.</summary>
public record ApiError(string Error);

/// <summary>
/// A problem whose /api error body has more fields than <c>error</c> (its detail), e.g.
/// <c>{"error": "...", "missing_group_ids": [...]}</c>. Name the fields as they're written (snake_case).
/// </summary>
public sealed class ApiErrorProblem : ProblemDetails
{
    public Dictionary<string, object?> Fields { get; } = [];
}

/// <summary>The body of a successful /api change, as the Flask backend's: <c>{"success": true}</c>.</summary>
public sealed record ApiSuccess(bool Success)
{
    public static readonly ApiSuccess Instance = new(true);
}

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

    public const string NoJsonData = "No JSON data provided";
    public const string InvalidJson = "Invalid JSON";

    /// <summary>A change that raced another on the same data (a Marten concurrency failure): 409.</summary>
    public const string ConcurrentChange = "Changed by someone else at the same time; try again";

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

    /// <summary>
    /// /api endpoints that read a JSON body (the changes) need one with something in it, as the Flask backend's
    /// <c>if not data</c>:
    /// a missing, empty, <c>null</c>, <c>{}</c> or <c>[]</c> body answers 400 <see cref="NoJsonData"/>, malformed JSON
    /// 400 <see cref="InvalidJson"/>, before the endpoint binds it (Wolverine.Http would answer the serializer's message,
    /// and a null body fails its validation middleware). Goes after authorization: signed-out requests still get 401.
    /// </summary>
    public static IApplicationBuilder UseApiJsonBodies(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (!context.Request.IsApiRequest() ||
                context.GetEndpoint()?.Metadata.GetMetadata<IAcceptsMetadata>() is not { RequestType: not null })
            {
                await next(context);
                return;
            }

            context.Request.EnableBuffering();
            string? error;
            try
            {
                using var body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
                error = body.RootElement switch
                {
                    { ValueKind: JsonValueKind.Null } => NoJsonData,
                    { ValueKind: JsonValueKind.Object } o when !o.EnumerateObject().Any() => NoJsonData,
                    { ValueKind: JsonValueKind.Array } a when a.GetArrayLength() == 0 => NoJsonData,
                    _ => null,
                };
            }
            catch (JsonException)
            {
                error = context.Request.Body.Length == 0 ? NoJsonData : InvalidJson;
            }

            if (error is not null)
            {
                await context.WriteApiErrorAsync(StatusCodes.Status400BadRequest, error);
                return;
            }

            context.Request.Body.Position = 0;
            await next(context);
        });

    public static Task WriteApiErrorAsync(this HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new ApiError(message), ApiJson(context), ErrorContentType);
    }

    /// <summary>A problem's <see cref="ApiError"/>, with an <see cref="ApiErrorProblem"/>'s fields next to <c>error</c>.</summary>
    private static Task WriteApiErrorAsync(this HttpContext context, int statusCode, ProblemDetails problem)
    {
        if (problem is not ApiErrorProblem { Fields.Count: > 0 } withFields)
            return context.WriteApiErrorAsync(statusCode, Message(problem));

        var options = ApiJson(context);
        var body = new JsonObject { ["error"] = Message(problem) };
        foreach (var (name, value) in withFields.Fields)
            body[name] = JsonSerializer.SerializeToNode(value, options);
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(body, options, ErrorContentType);
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
            return new ValueTask(context.HttpContext.WriteApiErrorAsync(statusCode, context.ProblemDetails));
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
