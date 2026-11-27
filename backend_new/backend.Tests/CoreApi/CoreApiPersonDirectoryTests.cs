using Backend.CoreApi;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Backend.Tests.CoreApi;

public class CoreApiPersonDirectoryTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly CoreApiStub stub = new();

    private IPersonDirectory Directory() =>
        factory.WithStubbedCoreApi(stub).Services.GetRequiredService<IPersonDirectory>();

    [Fact]
    public async Task GetByUids_GetsTokenWithClientCredentials()
    {
        await Directory().GetByUidsAsync([Guid.NewGuid()], Ct);

        var token = stub.TokenRequests.ShouldHaveSingleItem();
        token.Uri.ShouldBe(new Uri("https://login.bcc.no/oauth/token"));
        token.Form["grant_type"].ShouldBe("client_credentials");
        token.Form["client_id"].ShouldStartWith("test-client-");
        token.Form["client_secret"].ShouldBe("test-secret");
        token.Form["audience"].ShouldBe("api.bcc.no");
        token.Form["scope"].ShouldBe("persons.name#read persons.profile_picture#read");
    }

    [Fact]
    public async Task GetByUids_QueriesPersonsByUid()
    {
        var uid = Guid.NewGuid();
        var other = Guid.NewGuid();

        await Directory().GetByUidsAsync([uid, other, uid], Ct);

        var query = stub.PersonQueries.ShouldHaveSingleItem();
        query.Uri.GetLeftPart(UriPartial.Path).ShouldBe("https://core.api.bcc.no/v2/persons");
        query.Authorization.ShouldStartWith("Bearer ");
        query.Query["fields"].ShouldBe(CoreApiPersonDirectory.QueryFields);
        query.FilteredUids().ShouldBe([uid, other], ignoreOrder: true);
    }

    [Fact]
    public async Task GetByUids_MapsFoundPersonsAndLeavesOutUnknownOnes()
    {
        var withPicture = Guid.NewGuid();
        var withoutPicture = Guid.NewGuid();
        stub.Persons = _ =>
        [
            CoreApiStub.Person(withPicture, " Ola Nordmann ", "https://example.org/ola.jpg"),
            CoreApiStub.Person(withoutPicture, "Kari Nordmann", ""),
        ];

        var persons = await Directory().GetByUidsAsync([withPicture, withoutPicture, Guid.NewGuid()], Ct);

        persons.ShouldBe(
            [
                new PersonSummary(withPicture, "Ola Nordmann", "https://example.org/ola.jpg"),
                new PersonSummary(withoutPicture, "Kari Nordmann", null),
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task GetByUids_WithManyUids_QueriesInBatches()
    {
        var uids = Enumerable.Range(0, CoreApiPersonDirectory.BatchSize + 1).Select(_ => Guid.NewGuid()).ToList();
        stub.Persons = query => [.. uids.Where(u => query["filter"]!.Contains(u.ToString())).Select(u => CoreApiStub.Person(u, "X"))];

        var persons = await Directory().GetByUidsAsync(uids, Ct);

        stub.PersonQueries.Select(q => q.FilteredUids().Count).ShouldBe([CoreApiPersonDirectory.BatchSize, 1]);
        persons.Select(p => p.Uid).ShouldBe(uids, ignoreOrder: true);
        stub.TokenRequests.Count().ShouldBe(1, "the SDK should reuse its token");
    }

    [Fact]
    public async Task GetByUids_WithoutUids_DoesNotCallTheCoreApi()
    {
        var persons = await Directory().GetByUidsAsync([], Ct);

        persons.ShouldBeEmpty();
        stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_SendsTextAndLimit()
    {
        var uid = Guid.NewGuid();
        stub.Persons = _ => [CoreApiStub.Person(uid, "Ola Nordmann")];

        var persons = await Directory().SearchAsync("Ola Nord", 50, Ct);

        var query = stub.PersonQueries.ShouldHaveSingleItem();
        query.Query["search"].ShouldBe("Ola Nord");
        query.Query["limit"].ShouldBe("50");
        query.Query["fields"].ShouldBe(CoreApiPersonDirectory.QueryFields);
        persons.ShouldBe([new PersonSummary(uid, "Ola Nordmann", null)]);
    }

    [Fact]
    public async Task WithoutCredentials_ThrowsWithoutCallingTheCoreApi()
    {
        var directory = factory.WithCoreApi(stub).Services.GetRequiredService<IPersonDirectory>();

        await Should.ThrowAsync<CoreApiNotConfiguredException>(() => directory.GetByUidsAsync([Guid.NewGuid()], Ct));
        await Should.ThrowAsync<CoreApiNotConfiguredException>(() => directory.SearchAsync("Ola", 5, Ct));
        stub.Requests.ShouldBeEmpty();
    }
}
