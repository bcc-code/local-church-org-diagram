using Backend.CoreApi;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Backend.Tests.CoreApi;

public class CoreApiSettingsTests
{
    private static CoreApiSettings From(params (string Key, string Value)[] values) =>
        CoreApiSettings.From(new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, (string?)v.Value)))
            .Build());

    [Fact]
    public void From_CoreApiSection()
    {
        var settings = From(("CoreApi:Environment", "sandbox"), ("CoreApi:ClientId", "id"), ("CoreApi:ClientSecret", "secret"));

        settings.Environment.ShouldBe("sandbox");
        settings.ClientId.ShouldBe("id");
        settings.ClientSecret.ShouldBe("secret");
        settings.IsConfigured.ShouldBeTrue();
    }

    [Fact]
    public void From_FallsBackToFlaskVariables()
    {
        var settings = From(("BCC_OAUTH_CLIENT_ID", "legacy-id"), ("BCC_OAUTH_CLIENT_SECRET", "legacy-secret"));

        settings.Environment.ShouldBe("prod");
        settings.ClientId.ShouldBe("legacy-id");
        settings.ClientSecret.ShouldBe("legacy-secret");
    }

    [Fact]
    public void From_PrefersCoreApiSection()
    {
        var settings = From(
            ("CoreApi:ClientId", "id"), ("CoreApi:ClientSecret", "secret"),
            ("BCC_OAUTH_CLIENT_ID", "legacy-id"), ("BCC_OAUTH_CLIENT_SECRET", "legacy-secret"));

        settings.ClientId.ShouldBe("id");
        settings.ClientSecret.ShouldBe("secret");
    }

    [Theory]
    [InlineData("id", "")]
    [InlineData("", "secret")]
    [InlineData("", "")]
    public void IsConfigured_NeedsBoth(string clientId, string secret) =>
        From(("CoreApi:ClientId", clientId), ("CoreApi:ClientSecret", secret)).IsConfigured.ShouldBeFalse();
}
