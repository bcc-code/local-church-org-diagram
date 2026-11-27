using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace Backend.Tests;

public class HealthCheckTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Up_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/up");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HealthStatus>();
        body.ShouldNotBeNull().Status.ShouldBe("OK");
    }

    private record HealthStatus(string Status);
}
