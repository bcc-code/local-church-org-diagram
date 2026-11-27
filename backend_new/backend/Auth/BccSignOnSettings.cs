namespace Backend.Auth;

/// <summary>
/// The "BccSignOn" settings: the OpenID Connect client users sign in with at BCC sign-on. ClientId and ClientSecret fall
/// back to the Flask backend's BCC_OIDC_CLIENT_ID and BCC_OIDC_CLIENT_SECRET: both backends use the same client (and
/// its registered /authorize callback).
/// </summary>
public class BccSignOnSettings
{
    public const string Section = "BccSignOn";
    public const string LegacyClientIdVariable = "BCC_OIDC_CLIENT_ID";
    public const string LegacySecretVariable = "BCC_OIDC_CLIENT_SECRET";

    public string Authority { get; set; } = "https://login.bcc.no";

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public static BccSignOnSettings From(IConfiguration configuration)
    {
        var settings = configuration.GetSection(Section).Get<BccSignOnSettings>() ?? new BccSignOnSettings();
        if (string.IsNullOrEmpty(settings.ClientId))
            settings.ClientId = configuration[LegacyClientIdVariable];
        if (string.IsNullOrEmpty(settings.ClientSecret))
            settings.ClientSecret = configuration[LegacySecretVariable];
        return settings;
    }
}
