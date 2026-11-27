using System.Net;
using Backend.Tests.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace Backend.Tests.Api;

/// <summary>/api errors as the Flask backend answers them: the status code and <c>{"error": "..."}</c>.</summary>
public class ApiErrorTests(BackendFactory factory) : IClassFixture<BackendFactory>
{
    private HttpClient Anonymous() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task ShouldBeError(HttpResponseMessage response, HttpStatusCode status, string error)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        (await response.ReadJsonAsync()).ShouldBeJson(new { error });
    }

    [Theory]
    [InlineData("/api/tree")]
    [InlineData("/api/persons?group_id=1")]
    [InlineData("/api/titles")]
    public async Task SignedOut_IsUnauthorized(string url) =>
        await ShouldBeError(await Anonymous().GetAsync(url), HttpStatusCode.Unauthorized, "Authentication required");

    [Fact]
    public async Task NotAMember_IsForbidden() =>
        await ShouldBeError(await factory.CreateClient(TestUser.NonMember()).GetAsync("/api/tree"),
            HttpStatusCode.Forbidden, "Forbidden");

    // A problem a Wolverine.Http endpoint returns (here from Before)
    [Fact]
    public async Task AnotherTenant_IsForbidden() =>
        await ShouldBeError(await factory.CreateClient(TestUser.Member(TestUser.Copenhagen)).GetAsync("/api/test/auth/tenants/1"),
            HttpStatusCode.Forbidden, "Not allowed for tenant 1");

    [Theory]
    [InlineData("/api/persons/search", "Query parameter 'q' is required")]
    [InlineData("/api/persons/search?q=%20%20", "Query parameter 'q' is required")]
    [InlineData("/api/persons/search?q=ab", "Search query must be at least 3 characters")]
    public async Task InvalidSearch_IsABadRequest(string url, string error) =>
        await ShouldBeError(await factory.CreateClient(TestUser.Member()).GetAsync(url), HttpStatusCode.BadRequest, error);

    [Fact]
    public async Task OpenApi_DocumentsTheErrorsAndSnakeCase()
    {
        await using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

        var document = (await (await development.CreateClient().GetAsync("/openapi/v1.json")).ReadJsonAsync())!;

        var tree = document["paths"]!["/api/tree"]!["get"]!["responses"]!;
        tree["401"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>()
            .ShouldBe("#/components/schemas/ApiError");
        tree["403"].ShouldNotBeNull();
        tree["404"].ShouldBeNull();
        var schemas = document["components"]!["schemas"]!.AsObject();
        schemas.Select(s => s.Key).ShouldNotContain("ProblemDetails");
        schemas["GroupNode"]!["properties"]!.AsObject().Select(p => p.Key)
            .ShouldBe(["group_id", "label", "parent_group_id", "member_count", "sort_order"], ignoreOrder: true);
        document["paths"]!["/api/persons"]!["get"]!["parameters"]!.AsArray()
            .Select(p => p!["name"]!.GetValue<string>()).ShouldBe(["group_id"]);
    }
}

/// <summary>The SPA's pages, as the Flask backend's index().</summary>
public class AppPageTests(AppPageTests.WithIndex factory) : IClassFixture<AppPageTests.WithIndex>
{
    /// <summary>A web root with an index.html, like the built SPA's.</summary>
    public sealed class WithIndex : BackendFactory
    {
        public string WebRoot { get; } = Directory.CreateTempSubdirectory("org-diagram-webroot-").FullName;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            File.WriteAllText(Path.Combine(WebRoot, "index.html"), "<!doctype html><title>Org chart</title>");
            Directory.CreateDirectory(Path.Combine(WebRoot, "assets"));
            File.WriteAllText(Path.Combine(WebRoot, "assets", "index-abc123.js"), "console.log('org chart')");
            builder.UseWebRoot(WebRoot);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            // DisposeAsync and Dispose both end up here
            if (Directory.Exists(WebRoot))
                Directory.Delete(WebRoot, recursive: true);
        }
    }

    private HttpClient Anonymous() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/", "/login?returnUrl=%2F")]
    [InlineData("/51/admin", "/login?returnUrl=%2F51%2Fadmin")]
    [InlineData("/51/admin?x=1", "/login?returnUrl=%2F51%2Fadmin%3Fx%3D1")]
    public async Task SignedOut_RedirectsToSignInAndBack(string url, string location)
    {
        var response = await Anonymous().GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.PathAndQuery.ShouldBe(location);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/51/admin")]
    public async Task SignedIn_GetsTheSpa(string url)
    {
        var response = await factory.CreateClient(TestUser.Member(TestUser.Copenhagen)).GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        response.Headers.CacheControl!.NoCache.ShouldBeTrue();
        (await response.Content.ReadAsStringAsync()).ShouldContain("<title>Org chart</title>");
    }

    [Fact]
    public async Task Assets_AreServedToAnyone()
    {
        var response = await Anonymous().GetAsync("/assets/index-abc123.js");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/javascript");
        (await response.Content.ReadAsStringAsync()).ShouldBe("console.log('org chart')");
    }

    [Fact]
    public async Task UnknownFile_IsNotFound() =>
        (await Anonymous().GetAsync("/assets/missing.js")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

    [Fact]
    public async Task AnotherChurchsAdmin_IsForbidden()
    {
        var response = await factory.CreateClient(TestUser.Member(TestUser.Copenhagen)).GetAsync("/1/admin");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Unauthorized");
    }

    [Fact]
    public async Task NotAMember_IsForbiddenWithoutARedirect() =>
        (await factory.CreateClient(TestUser.NonMember()).GetAsync("/")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
}

public class AppPageWithoutSpaTests(BackendFactory factory) : IClassFixture<BackendFactory>
{
    [Fact]
    public async Task WithoutABuiltSpa_IsNotFound() =>
        (await factory.CreateClient(TestUser.Member()).GetAsync("/")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
}
