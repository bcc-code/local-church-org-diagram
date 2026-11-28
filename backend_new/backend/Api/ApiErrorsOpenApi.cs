using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Backend.Api;

/// <summary>
/// The endpoint answers 404 with an <see cref="ApiError"/> (e.g. an unknown group in its request body), documented as
/// such by <see cref="ApiErrorsOpenApi"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ApiNotFoundAttribute : Attribute;

/// <summary>
/// Documents /api errors as they are written (<see cref="ApiConventions"/>): every error response of an /api operation
/// but a 404 is an <see cref="ApiError"/> (not the ProblemDetails Wolverine.Http declares), and every operation can
/// answer 401 (signed out) and 403 (not a member of a church, or not allowed for the tenant).
/// </summary>
public sealed class ApiErrorsOpenApi : IOpenApiOperationTransformer, IOpenApiDocumentTransformer
{
    private const string SchemaName = nameof(ApiError);

    public async Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.RelativePath?.StartsWith("api/", StringComparison.Ordinal) != true ||
            context.Document is not { } document)
            return;

        if (document.Components?.Schemas?.ContainsKey(SchemaName) != true)
            document.AddComponent(SchemaName,
                await context.GetOrCreateSchemaAsync(typeof(ApiError), cancellationToken: cancellationToken));

        operation.Responses ??= [];
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });
        // Wolverine.Http declares a 404 on every endpoint: only routes with path parameters can answer it (one that
        // doesn't parse), with no body. Endpoints marked [ApiNotFound] answer it with an ApiError.
        var answersNotFound = context.Description.ActionDescriptor.EndpointMetadata.OfType<ApiNotFoundAttribute>().Any();
        if (answersNotFound)
            operation.Responses["404"] = new OpenApiResponse { Description = "Not Found" };
        else if (context.Description.ParameterDescriptions.All(p => p.Source.Id != "Path"))
            operation.Responses.Remove("404");

        foreach (var (status, response) in operation.Responses)
        {
            if (status == "404" && !answersNotFound || !status.StartsWith('4') && !status.StartsWith('5') ||
                response is not OpenApiResponse concrete)
                continue;
            concrete.Content = new Dictionary<string, OpenApiMediaType>
            {
                [ApiConventions.ErrorContentType] = new() { Schema = new OpenApiSchemaReference(SchemaName, document) },
            };
        }
    }

    /// <summary>Drops the ProblemDetails schemas the operations no longer reference.</summary>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components?.Schemas?.Remove("ProblemDetails");
        document.Components?.Schemas?.Remove("HttpValidationProblemDetails");
        return Task.CompletedTask;
    }
}
