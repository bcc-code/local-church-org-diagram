using System.Net;
using System.Net.Http.Json;
using Backend.Features.MembershipTree;
using Backend.Groups;
using Backend.Tests.Auth;
using Backend.Tests.Database;
using Shouldly;

namespace Backend.Tests.Features.MembershipTree;

public class MembershipTreeTests
{
    private static readonly DateTimeOffset Day1 = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static GroupView Group(string name, int? sortOrder, DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), name, null, 7, sortOrder, createdAt);

    [Fact]
    public void ToNodes_OrdersBySortOrderUnsetLastThenCreation()
    {
        var nodes = Endpoints.ToNodes([
            Group("unset", null, Day1),
            Group("1 later", 1, Day1.AddDays(1)),
            Group("1 earlier", 1, Day1),
            Group("0", 0, Day1.AddDays(2)),
        ]);

        nodes.Select(n => n.Name).ShouldBe(["0", "1 earlier", "1 later", "unset"]);
    }

    [Fact]
    public void ToNodes_MapsTheView()
    {
        var parent = Guid.NewGuid();
        var view = new GroupView(Guid.NewGuid(), "Styre", parent, 7, 3, Day1, MemberCount: 4);

        Endpoints.ToNodes([view]).ShouldHaveSingleItem()
            .ShouldBe(new GroupNode(view.Id, "Styre", parent, 3, 4));
    }

    [Fact]
    public void GroupView_CountsMemberships()
    {
        var created = GroupViewProjection.Create(new GroupImported(Guid.NewGuid(), 1, "Styre", null, null, 7, null, Day1));
        var membership = new MembershipImported(created.Id, 2, 1, Guid.NewGuid(), null, null, 7, Day1);

        created.MemberCount.ShouldBe(0);
        GroupViewProjection.Apply(membership, GroupViewProjection.Apply(membership, created)).MemberCount.ShouldBe(2);
    }
}

[Trait("Category", "Database")]
[Collection(DatabaseCollection.Name)]
public class MembershipTreeDatabaseTests(DatabaseBackendFactory factory)
{
    [DatabaseFact]
    public async Task ListsTheChurchsGroupsWithMemberCounts()
    {
        var tenant = Seed.NewTenant();
        var root = Seed.Group(tenant, "Root", sortOrder: 0, createdAfter: 0);
        var child = Seed.Group(tenant, "Child", root, sortOrder: 0, createdAfter: 1);
        var empty = Seed.Group(tenant, "Empty", root, sortOrder: 1, createdAfter: 0);
        var otherChurch = Seed.Group(Seed.NewTenant(), "Other");
        await factory.StoreAsync(root, child, empty, otherChurch,
            Seed.Member(child, Guid.NewGuid()), Seed.Member(child, Guid.NewGuid()), Seed.Member(otherChurch, Guid.NewGuid()));

        var nodes = await factory.CreateClient(TestUser.Member(tenant)).GetFromJsonAsync<List<GroupNode>>("/api/groups");

        nodes.ShouldBe([
            new GroupNode(root.GroupId, "Root", null, 0, 0),
            new GroupNode(child.GroupId, "Child", root.GroupId, 0, 2),
            new GroupNode(empty.GroupId, "Empty", root.GroupId, 1, 0),
        ]);
    }

    [DatabaseFact]
    public async Task RequiresSignIn() =>
        (await factory.CreateClient().GetAsync("/api/groups")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    [DatabaseFact]
    public async Task RequiresMembership() =>
        (await factory.CreateClient(TestUser.NonMember(Seed.NewTenant())).GetAsync("/api/groups")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

    [DatabaseFact]
    public async Task RequiresAChurch() =>
        (await factory.CreateClient(TestUser.Member(churchId: null)).GetAsync("/api/groups")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
}
