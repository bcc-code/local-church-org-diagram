using System.Net;
using Backend.CoreApi;
using Backend.Features.GroupMembershipListing;
using Backend.Groups;
using Backend.Tests.Api;
using Backend.Tests.Auth;
using Backend.Tests.CoreApi;
using Backend.Tests.Database;
using Shouldly;

namespace Backend.Tests.Features;

public class MemberOrderingTests
{
    private static GroupMember Member(string name, string? title = null) => new(Guid.NewGuid(), name, null, title, null);

    [Fact]
    public void Sort_LeadersFirstThenOtherTitlesThenNoTitle()
    {
        var sorted = MemberOrdering.Sort([
            Member("Ola"),
            Member("Kari", "Sekretær"),
            Member("Per", "ansvarlig"),
            Member("Anne", "Kasserer"),
            Member("Nils", " Leder "),
            Member("Bjørn", ""),
        ]);

        sorted.Select(m => m.Name).ShouldBe(["Nils", "Per", "Anne", "Kari", "Bjørn", "Ola"]);
    }

    [Fact]
    public void Sort_ByNameWithinATitle_CaseInsensitiveByCodePoint()
    {
        var sorted = MemberOrdering.Sort([Member("Åse", "Leder"), Member("bente", "Leder"), Member("Arne", "Leder")]);

        sorted.Select(m => m.Name).ShouldBe(["Arne", "bente", "Åse"]);
    }
}

public class GroupMembershipListingTests
{
    private static readonly Guid Known = Guid.NewGuid();
    private static readonly Guid Unknown = Guid.NewGuid();

    private static MembershipView Membership(Guid personUid, string? title, string? link = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), personUid, title, link, 7, DateTimeOffset.UnixEpoch);

    [Fact]
    public void ToMembers_JoinsPersonDetailsAndKeepsUnknownPersons()
    {
        var members = Endpoints.ToMembers(
            [Membership(Unknown, null), Membership(Known, "Leder", "https://x")],
            [new PersonSummary(Known, "Kari Nordmann", "https://pic")]);

        members.ShouldBe([
            new GroupMember(Known, "Kari Nordmann", "https://pic", "Leder", "https://x"),
            new GroupMember(Unknown, Endpoints.UnknownPersonName, null, null, null),
        ]);
    }
}

[Trait("Category", "Database")]
[Collection(DatabaseCollection.Name)]
public class GroupMembershipListingDatabaseTests(DatabaseBackendFactory factory)
{
    [DatabaseFact]
    public async Task ListsMembersWithCoreApiDetails()
    {
        var tenant = Seed.NewTenant();
        var group = Seed.Group(tenant, "Styre");
        var leader = Guid.NewGuid();
        var member = Guid.NewGuid();
        await factory.StoreAsync(group, Seed.Member(group, member), Seed.Member(group, leader, "Leder", "https://x"));
        factory.CoreApi.Persons = _ => [CoreApiStub.Person(leader, "Kari"), CoreApiStub.Person(member, "Ola", "https://pic")];

        var members = await factory.CreateClient(TestUser.Member(tenant))
            .GetApiAsync<List<GroupMember>>($"/api/persons?group_id={group.GroupId}");

        members.ShouldBe([
            new GroupMember(leader, "Kari", null, "Leder", "https://x"),
            new GroupMember(member, "Ola", "https://pic", null, null),
        ]);
        factory.CoreApi.PersonQueries.Last().FilteredUids().ShouldBe([member, leader], ignoreOrder: true);
    }

    [DatabaseFact]
    public async Task EmptyGroup_DoesNotCallTheCoreApi()
    {
        var tenant = Seed.NewTenant();
        var group = Seed.Group(tenant, "Empty");
        await factory.StoreAsync(group);
        var queriesBefore = factory.CoreApi.PersonQueries.Count();

        var members = await factory.CreateClient(TestUser.Member(tenant))
            .GetApiAsync<List<GroupMember>>($"/api/persons?group_id={group.GroupId}");

        members.ShouldBeEmpty();
        factory.CoreApi.PersonQueries.Count().ShouldBe(queriesBefore);
    }

    // As with the Flask backend, which filters memberships by the user's church
    [DatabaseFact]
    public async Task AnotherChurchsGroup_HasNoMembers()
    {
        var group = Seed.Group(Seed.NewTenant(), "Theirs");
        await factory.StoreAsync(group, Seed.Member(group, Guid.NewGuid()));

        (await factory.CreateClient(TestUser.Member(Seed.NewTenant()))
                .GetApiAsync<List<GroupMember>>($"/api/persons?group_id={group.GroupId}"))
            .ShouldBeEmpty();
    }

    [DatabaseFact]
    public async Task UnknownGroup_HasNoMembers() =>
        (await factory.CreateClient(TestUser.Member(Seed.NewTenant()))
            .GetApiAsync<List<GroupMember>>($"/api/persons?group_id={Guid.NewGuid()}"))
        .ShouldBeEmpty();

    [DatabaseTheory]
    [InlineData("/api/persons", "No group_id provided")]
    [InlineData("/api/persons?group_id=", "No group_id provided")]
    [InlineData("/api/persons?group_id=12", "Invalid group_id")]
    public async Task RejectsAMissingOrInvalidGroupId(string url, string error)
    {
        var response = await factory.CreateClient(TestUser.Member(Seed.NewTenant())).GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).ShouldBeJson(new { error });
    }

    [DatabaseFact]
    public async Task RequiresSignIn() =>
        (await factory.CreateClient().GetAsync($"/api/persons?group_id={Guid.NewGuid()}")).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
}
