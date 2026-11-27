using Backend.Auth;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Backend.Tests.Auth;

public class BccSignOnSettingsTests
{
    private static BccSignOnSettings From(params (string Key, string Value)[] values) =>
        BccSignOnSettings.From(new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, (string?)v.Value)))
            .Build());

    [Fact]
    public void Defaults_to_BCC_sign_on()
    {
        From().Authority.ShouldBe("https://login.bcc.no");
    }

    [Fact]
    public void Falls_back_to_the_Flask_variables()
    {
        var settings = From(("BCC_OIDC_CLIENT_ID", "legacy-id"), ("BCC_OIDC_CLIENT_SECRET", "legacy-secret"));

        settings.ClientId.ShouldBe("legacy-id");
        settings.ClientSecret.ShouldBe("legacy-secret");
    }

    [Fact]
    public void Prefers_the_BccSignOn_section()
    {
        var settings = From(
            ("BccSignOn:ClientId", "id"), ("BccSignOn:ClientSecret", "secret"),
            ("BCC_OIDC_CLIENT_ID", "legacy-id"), ("BCC_OIDC_CLIENT_SECRET", "legacy-secret"));

        settings.ClientId.ShouldBe("id");
        settings.ClientSecret.ShouldBe("secret");
    }
}
