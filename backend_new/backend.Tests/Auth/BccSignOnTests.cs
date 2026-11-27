using System.Net;
using System.Net.Http.Json;
using System.Web;
using Backend.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace Backend.Tests.Auth;

public class BccSignOnTests(BackendFactory factory) : IClassFixture<BackendFactory>
{
    private HttpClient Anonymous() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Login_redirects_to_BCC_sign_on()
    {
        var response = await Anonymous().GetAsync("/login");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location.ShouldNotBeNull();
        location.GetLeftPart(UriPartial.Path).ShouldBe("https://login.bcc.no/authorize");
        var query = HttpUtility.ParseQueryString(location.Query);
        query["client_id"].ShouldBe(BackendFactory.TestClientId);
        query["response_type"].ShouldBe("code");
        query["scope"].ShouldBe("openid profile email church");
        // The Flask backend's callback, registered on the shared client
        new Uri(query["redirect_uri"]!).AbsolutePath.ShouldBe("/authorize");
    }

    [Theory]
    [InlineData("/1/admin", "/1/admin")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("https://evil.example", "/")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("admin", "/")]
    public void LocalReturnUrl_only_allows_paths_on_this_site(string? returnUrl, string expected) =>
        BccSignOn.LocalReturnUrl(returnUrl).ShouldBe(expected);

    [Fact]
    public async Task Logout_ends_the_session_and_goes_home()
    {
        var response = await Anonymous().GetAsync("/logout");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/");
        response.Headers.GetValues("Set-Cookie").ShouldContain(cookie => cookie.StartsWith("org-diagram.session=;"));
    }

    [Fact]
    public async Task Failed_callback_offers_a_new_sign_in_instead_of_a_redirect()
    {
        // No state: the callback page opened again, or a replayed callback
        var response = await Anonymous().GetAsync("/authorize?code=abc");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("href=\"/login?returnUrl=%2F\"");
    }

    [Fact]
    public async Task Api_answers_401_instead_of_redirecting_to_sign_in()
    {
        var response = await Anonymous().GetAsync("/api/test/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Api_endpoints_require_a_signed_in_member_by_default()
    {
        var member = TestUser.Member();

        var response = await factory.CreateClient(member).GetAsync("/api/test/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<WhoAmI>())!.PersonUid.ShouldBe(member.PersonUid);
        (await factory.CreateClient(TestUser.NonMember()).GetAsync("/api/test/auth/me"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AllowAnonymous_endpoints_stay_open()
    {
        (await Anonymous().GetAsync("/api/test/auth/anonymous")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Policy_endpoints_answer_403_to_other_members()
    {
        (await factory.CreateClient(TestUser.Member()).GetAsync("/api/test/auth/global"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await factory.CreateClient(TestUser.Global()).GetAsync("/api/test/auth/global"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Tenant_endpoints_authorize_against_the_tenant()
    {
        var member = factory.CreateClient(TestUser.Member(TestUser.Copenhagen));
        var admin = factory.CreateClient(TestUser.LocalAdmin(TestUser.Copenhagen));

        (await member.GetAsync("/api/test/auth/tenants/51")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await member.GetAsync("/api/test/auth/tenants/1")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.PostAsync("/api/test/auth/tenants/51", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.PostAsync("/api/test/auth/tenants/51", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.PostAsync("/api/test/auth/tenants/1", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
