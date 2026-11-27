namespace Backend.CoreApi;

/// <summary>
/// The "CoreApi" settings: the app's single BCC Core API OAuth client (client credentials). ClientId and ClientSecret
/// fall back to the Flask backend's BCC_OAUTH_CLIENT_ID and BCC_OAUTH_CLIENT_SECRET, so both backends can share one
/// environment during the migration.
/// </summary>
public class CoreApiSettings
{
    public const string Section = "CoreApi";
    public const string LegacyClientIdVariable = "BCC_OAUTH_CLIENT_ID";
    public const string LegacySecretVariable = "BCC_OAUTH_CLIENT_SECRET";

    /// <summary>BCC platform environment: prod, staging, sandbox or dev.</summary>
    public string Environment { get; set; } = "prod";

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public bool IsConfigured => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret);

    public static CoreApiSettings From(IConfiguration configuration)
    {
        var settings = configuration.GetSection(Section).Get<CoreApiSettings>() ?? new CoreApiSettings();
        if (string.IsNullOrEmpty(settings.ClientId))
            settings.ClientId = configuration[LegacyClientIdVariable];
        if (string.IsNullOrEmpty(settings.ClientSecret))
            settings.ClientSecret = configuration[LegacySecretVariable];
        return settings;
    }
}

/// <summary>A Core API call was made without <see cref="CoreApiSettings.ClientId"/> and ClientSecret.</summary>
public class CoreApiNotConfiguredException()
    : InvalidOperationException(
        $"BCC Core API credentials are not configured: set {CoreApiSettings.Section}:ClientId and " +
        $"{CoreApiSettings.Section}:ClientSecret (or {CoreApiSettings.LegacyClientIdVariable} and " +
        $"{CoreApiSettings.LegacySecretVariable})");
