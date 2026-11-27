using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Backend.Tests.Api;

/// <summary>Reads /api responses, which are snake_case JSON like the Flask backend's (Backend.Api.ApiConventions).</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static Task<T?> GetApiAsync<T>(this HttpClient client, string url) =>
        client.GetFromJsonAsync<T>(url, Options);

    public static Task<T?> ReadApiAsync<T>(this HttpContent content) => content.ReadFromJsonAsync<T>(Options);

    /// <summary>The raw JSON, for asserting the exact shape (property names and JSON types).</summary>
    public static async Task<JsonNode?> ReadJsonAsync(this HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync());

    /// <summary>
    /// The JSON is exactly <paramref name="expected"/> serialized (give it snake_case names, e.g. an anonymous object
    /// <c>new { group_id = id }</c>): the same properties, values and JSON types, in any property order.
    /// </summary>
    public static void ShouldBeJson(this JsonNode? actual, object expected)
    {
        var expectedJson = JsonSerializer.SerializeToNode(expected, Options);
        if (!JsonNode.DeepEquals(actual, expectedJson))
            throw new Shouldly.ShouldAssertException(
                $"JSON should be\n{expectedJson?.ToJsonString()}\nbut was\n{actual?.ToJsonString()}");
    }
}
