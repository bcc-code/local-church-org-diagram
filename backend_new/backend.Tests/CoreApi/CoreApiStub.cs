using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Backend.CoreApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Tests.CoreApi;

/// <summary>
/// Stands in for login.bcc.no and the BCC Core API as the primary handler of every HttpClient the SDK makes: answers
/// token requests with an unsigned JWT and person queries with <see cref="Persons"/>, and records every request.
/// </summary>
public class CoreApiStub : HttpMessageHandler
{
    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

    /// <summary>Answers a person query (its query string); no persons by default.</summary>
    public Func<NameValueCollection, IEnumerable<object>> Persons { get; set; } = _ => [];

    /// <summary>Set to make the Core API (not the token endpoint) fail with this status.</summary>
    public HttpStatusCode? PersonsStatus { get; set; }

    public IEnumerable<RecordedRequest> PersonQueries => Requests.Where(r => r.Uri.AbsolutePath == "/v2/persons");

    public IEnumerable<RecordedRequest> TokenRequests => Requests.Where(r => r.Uri.AbsolutePath == "/oauth/token");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        var recorded = new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), body);
        Requests.Enqueue(recorded);

        if (recorded.Uri.AbsolutePath == "/oauth/token")
            return Json(new { access_token = Jwt(), expires_in = 3600, token_type = "Bearer" });
        if (PersonsStatus is { } status)
            return new HttpResponseMessage(status);
        var data = Persons(recorded.Query).ToList();
        return Json(new { data, meta = new { limit = 1000, skipped = 0, total = data.Count } });
    }

    public static object Person(Guid uid, string? displayName, string? profilePicture = null) =>
        new { uid, displayName, profilePicture };

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json"),
    };

    // The SDK reads the token's expiry from the JWT
    private static string Jwt()
    {
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        return $"{Base64Url("""{"alg":"none","typ":"JWT"}""")}.{Base64Url($$"""{"exp":{{exp}},"aud":"api.bcc.no"}""")}.sig";

        static string Base64Url(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

public record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? Body)
{
    public NameValueCollection Query => HttpUtility.ParseQueryString(Uri.Query);

    public NameValueCollection Form => HttpUtility.ParseQueryString(Body ?? "");

    /// <summary>The UIDs of a <c>{"uid": {"_in": [...]}}</c> filter.</summary>
    public List<Guid> FilteredUids()
    {
        using var filter = JsonDocument.Parse(Query["filter"]!);
        return [.. filter.RootElement.GetProperty("uid").GetProperty("_in").EnumerateArray().Select(e => e.GetGuid())];
    }
}

public static class CoreApiTestHost
{
    /// <summary>
    /// The app with these Core API credentials (none by default) and its HTTP calls going to <paramref name="stub"/>.
    /// Sets every credential setting, so a developer's own user secrets or BCC_OAUTH_* variables don't leak in.
    /// </summary>
    public static WebApplicationFactory<Program> WithCoreApi(
        this WebApplicationFactory<Program> factory, CoreApiStub? stub = null, string clientId = "", string secret = "") =>
        factory.WithWebHostBuilder(builder => builder.UseCoreApi(stub, clientId, secret));

    /// <summary><see cref="WithCoreApi"/> for a factory's own <c>ConfigureWebHost</c>.</summary>
    public static IWebHostBuilder UseCoreApi(
        this IWebHostBuilder builder, CoreApiStub? stub = null, string clientId = "", string secret = "")
    {
        builder.UseSetting($"{CoreApiSettings.Section}:ClientId", clientId);
        builder.UseSetting($"{CoreApiSettings.Section}:ClientSecret", secret);
        builder.UseSetting(CoreApiSettings.LegacyClientIdVariable, "");
        builder.UseSetting(CoreApiSettings.LegacySecretVariable, "");
        if (stub is not null)
            builder.ConfigureTestServices(services => services.ConfigureHttpClientDefaults(
                http => http.ConfigurePrimaryHttpMessageHandler(() => stub)));
        return builder;
    }

    /// <summary>A client ID unique to this test run: the SDK caches access tokens per client across the process.</summary>
    public static string UniqueClientId() => $"test-client-{Guid.NewGuid()}";

    /// <summary>
    /// <see cref="WithCoreApi"/> with credentials; the client ID is unique (test-client-&lt;guid&gt;), because the SDK
    /// caches access tokens per client across the whole process.
    /// </summary>
    public static WebApplicationFactory<Program> WithStubbedCoreApi(
        this WebApplicationFactory<Program> factory, CoreApiStub stub) =>
        factory.WithCoreApi(stub, clientId: UniqueClientId(), secret: "test-secret");
}
