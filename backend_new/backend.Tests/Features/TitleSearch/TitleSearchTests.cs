using System.Net;
using System.Net.Http.Json;
using Backend.CoreApi;
using Backend.Features.TitleSearch;
using Backend.Tests.Auth;
using Backend.Tests.CoreApi;
using Backend.Tests.Database;
using Shouldly;

namespace Backend.Tests.Features.TitleSearch;

public class TitleSearchTests
{
    [Fact]
    public void NormalizeTitles_TrimsDropsBlanksAndDuplicatesAndSorts() =>
        TitleSearchEndpoint.NormalizeTitles(["Leder", " Leder ", "", "   ", null, "Ansvarlig", "Økonomi", "kasserer"])
            .ShouldBe(["Ansvarlig", "Leder", "kasserer", "Økonomi"]);

    [Fact]
    public void ToHolders_SortsByNameCaseInsensitive()
    {
        var holders = TitleSearchEndpoint.ToHolders([
            new PersonSummary(Guid.NewGuid(), "ola", null),
            new PersonSummary(Guid.NewGuid(), "Kari", "https://pic"),
        ]);

        holders.Select(h => h.Name).ShouldBe(["Kari", "ola"]);
        holders[0].ProfilePicture.ShouldBe("https://pic");
    }
}

[Trait("Category", "Database")]
[Collection(DatabaseCollection.Name)]
public class TitleSearchDatabaseTests(DatabaseBackendFactory factory)
{
    [DatabaseFact]
    public async Task ListsTheChurchsTitles()
    {
        var tenant = Seed.NewTenant();
        var group = Seed.Group(tenant, "Styre");
        var other = Seed.Group(Seed.NewTenant(), "Theirs");
        await factory.StoreAsync(group, other,
            Seed.Member(group, Guid.NewGuid(), "Leder "), Seed.Member(group, Guid.NewGuid(), "Leder"),
            Seed.Member(group, Guid.NewGuid(), "Kasserer"), Seed.Member(group, Guid.NewGuid()),
            Seed.Member(other, Guid.NewGuid(), "Sekretær"));

        var titles = await factory.CreateClient(TestUser.Member(tenant)).GetFromJsonAsync<List<string>>("/api/titles");

        titles.ShouldBe(["Kasserer", "Leder"]);
    }

    [DatabaseFact]
    public async Task ListsDistinctHoldersOfTheTrimmedTitle()
    {
        var tenant = Seed.NewTenant();
        var styre = Seed.Group(tenant, "Styre");
        var ungdom = Seed.Group(tenant, "Ungdom");
        var other = Seed.Group(Seed.NewTenant(), "Theirs");
        var both = Guid.NewGuid();
        var spaced = Guid.NewGuid();
        await factory.StoreAsync(styre, ungdom, other,
            Seed.Member(styre, both, "Leder"), Seed.Member(ungdom, both, "Leder"), Seed.Member(ungdom, spaced, " Leder "),
            Seed.Member(styre, Guid.NewGuid(), "Kasserer"), Seed.Member(other, Guid.NewGuid(), "Leder"));
        factory.CoreApi.Persons = _ => [CoreApiStub.Person(spaced, "Ola"), CoreApiStub.Person(both, "Kari")];

        var holders = await factory.CreateClient(TestUser.Member(tenant))
            .GetFromJsonAsync<List<TitleHolder>>("/api/titles/Leder/persons");

        holders.ShouldBe([new TitleHolder(both, "Kari", null), new TitleHolder(spaced, "Ola", null)]);
        factory.CoreApi.PersonQueries.Last().FilteredUids().ShouldBe([both, spaced], ignoreOrder: true);
    }

    [DatabaseTheory]
    [InlineData("/api/titles")]
    [InlineData("/api/titles/Leder/persons")]
    public async Task RequiresAChurch(string url) =>
        (await factory.CreateClient(TestUser.Member(churchId: null)).GetAsync(url)).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
}
