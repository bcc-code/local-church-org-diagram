using System.Net.Http.Json;
using Backend.Features.PersonMembershipListing;
using Backend.Groups;
using Backend.Tests.Auth;
using Backend.Tests.Database;
using Shouldly;

namespace Backend.Tests.Features.PersonMembershipListing;

public class PersonMembershipListingTests
{
    private static readonly DateTimeOffset Day1 = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToPersonGroups_KeepsTheChurchsGroupsInCreationOrder()
    {
        var later = new GroupView(Guid.NewGuid(), "Later", null, 7, 0, Day1.AddDays(1), MemberCount: 2);
        var earlier = new GroupView(Guid.NewGuid(), "Earlier", null, 7, 5, Day1, MemberCount: 1);
        var theirs = new GroupView(Guid.NewGuid(), "Theirs", null, 8, 0, Day1);

        Endpoints.ToPersonGroups([later, theirs, earlier], 7).ShouldBe([
            new PersonGroup(earlier.Id, "Earlier", 1),
            new PersonGroup(later.Id, "Later", 2),
        ]);
    }
}

[Trait("Category", "Database")]
[Collection(DatabaseCollection.Name)]
public class PersonMembershipListingDatabaseTests(DatabaseBackendFactory factory)
{
    [DatabaseFact]
    public async Task ListsThePersonsGroupsInTheChurch()
    {
        var tenant = Seed.NewTenant();
        var person = Guid.NewGuid();
        var styre = Seed.Group(tenant, "Styre", createdAfter: 1);
        var ungdom = Seed.Group(tenant, "Ungdom", createdAfter: 0);
        var notMember = Seed.Group(tenant, "Other");
        var theirs = Seed.Group(Seed.NewTenant(), "Theirs");
        await factory.StoreAsync(styre, ungdom, notMember, theirs,
            Seed.Member(styre, person), Seed.Member(styre, Guid.NewGuid()), Seed.Member(ungdom, person),
            Seed.Member(notMember, Guid.NewGuid()), Seed.Member(theirs, person));

        var groups = await factory.CreateClient(TestUser.Member(tenant))
            .GetFromJsonAsync<List<PersonGroup>>($"/api/persons/{person}/groups");

        groups.ShouldBe([new PersonGroup(ungdom.GroupId, "Ungdom", 1), new PersonGroup(styre.GroupId, "Styre", 2)]);
    }

    [DatabaseFact]
    public async Task NoMemberships_IsEmpty() =>
        (await factory.CreateClient(TestUser.Member(Seed.NewTenant()))
            .GetFromJsonAsync<List<PersonGroup>>($"/api/persons/{Guid.NewGuid()}/groups"))
        .ShouldBeEmpty();
}
