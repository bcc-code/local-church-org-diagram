using System.Net;
using System.Net.Http.Json;
using Backend.CoreApi;
using Backend.Features.PersonSearch;
using Backend.Tests.Auth;
using Backend.Tests.CoreApi;
using Shouldly;

namespace Backend.Tests.Features.PersonSearch;

public class SearchRankingTests
{
    private static PersonSummary Person(string name) => new(Guid.NewGuid(), name, null);

    [Theory]
    [InlineData("Kari Nordmann", "kari nordmann", SearchRanking.ExactMatch)]
    // "kari" at 0: 500 + 100/1 + 50/1; "nord" at 5: + 100/2 + 50/6
    [InlineData("Kari Nordmann", "kari nord", 500 + 150 + 50 + 8)]
    // only the second word matches, at 5: 500 + 100/2 + 50/6
    [InlineData("Kari Nordmann", "xyz nord", 500 + 50 + 8)]
    [InlineData("Kari Nordmann", "xyz", SearchRanking.PartialMatch)]
    // Like Python's split(" "): the empty word between two spaces matches at 0 (100/2 + 50/1)
    [InlineData("Kari Nordmann", "kari  xyz", 500 + 150 + 50 + 50)]
    public void Score(string name, string query, int expected) => SearchRanking.Score(name, query).ShouldBe(expected);

    [Fact]
    public void Rank_BestFirstKeepingCoreApiOrderForTies()
    {
        var ranked = SearchRanking.Rank(
            [Person("Per Hansen"), Person("Hans Hansen"), Person("Anne Hansen"), Person("Hans")], "hans", 3);

        ranked.Select(p => p.DisplayName).ShouldBe(["Hans", "Hans Hansen", "Per Hansen"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData(" ab ")]
    public void Validator_RejectsShortOrMissingQueries(string? q) =>
        new PersonSearchQuery.Validator().Validate(new PersonSearchQuery(q)).IsValid.ShouldBeFalse();

    [Fact]
    public void Validator_AcceptsThreeCharacters() =>
        new PersonSearchQuery.Validator().Validate(new PersonSearchQuery(" abc ")).IsValid.ShouldBeTrue();
}

/// <summary>Through HTTP, without a database: search only calls the Core API.</summary>
public class PersonSearchEndpointTests
{
    [Fact]
    public async Task SearchesTheCoreApiAndReturnsTheTopFive()
    {
        var stub = new CoreApiStub
        {
            Persons = _ => Enumerable.Range(1, 8).Select(i => CoreApiStub.Person(Guid.NewGuid(), $"Hans {i}")),
        };
        await using var factory = new BackendFactory().WithStubbedCoreApi(stub);

        var results = await factory.CreateClient(TestUser.Member())
            .GetFromJsonAsync<List<PersonSearchResult>>("/api/persons/search?q=%20hans%20");

        results!.Count.ShouldBe(Endpoints.Results);
        var query = stub.PersonQueries.ShouldHaveSingleItem().Query;
        query["search"].ShouldBe("hans");
        query["limit"].ShouldBe($"{Endpoints.Candidates}");
    }

    [Theory]
    [InlineData("/api/persons/search")]
    [InlineData("/api/persons/search?q=ab")]
    public async Task RejectsShortQueries(string url)
    {
        await using var factory = new BackendFactory();

        var response = await factory.CreateClient(TestUser.Member()).GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequiresMembership()
    {
        await using var factory = new BackendFactory();

        (await factory.CreateClient(TestUser.NonMember()).GetAsync("/api/persons/search?q=hans")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RequiresSignIn()
    {
        await using var factory = new BackendFactory();

        (await factory.CreateClient().GetAsync("/api/persons/search?q=hans")).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
    }
}
