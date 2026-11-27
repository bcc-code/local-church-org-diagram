using BccCode.Core.Api.Client;
using BccCode.Platform;

namespace Backend.CoreApi;

public static class CoreApiServiceCollectionExtensions
{
    /// <summary>
    /// The Core API client must be granted all of these: requesting one that isn't granted fails every call. The SDK
    /// has no constant for the profile picture scope.
    /// </summary>
    public static readonly CoreApiScope[] Scopes =
    [
        CoreApiScope.PersonsNameRead,
        new("persons.profile_picture#read"),
    ];

    /// <summary>
    /// Registers <see cref="ICoreApiClient"/> (the SDK gets, caches and renews its access token), <see cref="IPersonDirectory"/>
    /// and the "core-api" health check. Starts without credentials: Core API calls then throw
    /// <see cref="CoreApiNotConfiguredException"/> and the health check is unhealthy.
    /// </summary>
    public static IServiceCollection AddCoreApi(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = CoreApiSettings.From(configuration);
        services.AddSingleton(settings);

        services
            .AddBccPlatform((AppEnvironment)settings.Environment)
            .AddBccCoreApiClient(new CoreApiClientOptions
            {
                Auth = new OAuthOptions { ClientId = settings.ClientId ?? "", ClientSecret = settings.ClientSecret ?? "" },
                Scopes = Scopes,
            });

        services.AddSingleton<IPersonDirectory, CoreApiPersonDirectory>();
        services.AddHealthChecks().AddCheck<CoreApiHealthCheck>("core-api");
        return services;
    }
}
