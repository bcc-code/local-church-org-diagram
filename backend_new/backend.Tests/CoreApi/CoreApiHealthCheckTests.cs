using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace Backend.Tests.CoreApi;

public class CoreApiHealthCheckTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly CoreApiStub stub = new();

    [Fact]
    public async Task Health_WhenCoreApiAnswers_IsHealthy()
    {
        var response = await factory.WithStubbedCoreApi(stub).CreateClient().GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
        stub.PersonQueries.ShouldHaveSingleItem().Query["limit"].ShouldBe("1");
    }

    [Fact]
    public async Task Health_WhenCoreApiFails_IsUnhealthy()
    {
        stub.PersonsStatus = HttpStatusCode.Unauthorized;

        var response = await factory.WithStubbedCoreApi(stub).CreateClient().GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Unhealthy");
    }

    [Fact]
    public async Task Health_WithoutCredentials_IsUnhealthyWithoutCallingTheCoreApi()
    {
        var response = await factory.WithCoreApi(stub).CreateClient().GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        stub.Requests.ShouldBeEmpty();
    }
}
