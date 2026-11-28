using System.Net;
using System.Net.Http.Json;
using Backend.Api;
using Backend.Features.GroupReordering;
using Backend.Features.MembershipTree;
using Backend.Groups;
using Backend.Tests.Api;
using Backend.Tests.Auth;
using Backend.Tests.Database;
using Shouldly;
using Wolverine.Http;
using Endpoints = Backend.Features.GroupReordering.Endpoints;

namespace Backend.Tests.Features;

public class GroupReorderingTests
{
    private static readonly TestUser Admin = TestUser.Global();

    public static TheoryData<ReorderGroups, string> InvalidRequests
    {
        get
        {
            var id = Guid.NewGuid().ToString();
            return new()
            {
                { new ReorderGroups(null), ReorderGroups.NoUpdates },
                { new ReorderGroups([]), ReorderGroups.NoUpdates },
                { new ReorderGroups([null]), ReorderGroups.EachRequiresBoth },
                { new ReorderGroups([new(id, null)]), ReorderGroups.EachRequiresBoth },
                { new ReorderGroups([new(null, 1)]), ReorderGroups.EachRequiresBoth },
                { new ReorderGroups([new("1", 1)]), ReorderGroups.InvalidGroupId },
                { new ReorderGroups([new(id, int.MaxValue + 1L)]), ReorderGroups.InvalidSortOrder },
                { new ReorderGroups([new(id, 0), new(id.ToUpperInvariant(), 1)]), ReorderGroups.DuplicateGroupId },
            };
        }
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void Validator_RejectsInvalidUpdates(ReorderGroups request, string message) =>
        new ReorderGroups.Validator().Validate(request).Errors.ShouldHaveSingleItem().ErrorMessage.ShouldBe(message);

    [Fact]
    public void Validator_AcceptsSortOrders() =>
        new ReorderGroups.Validator().Validate(new ReorderGroups([new(Guid.NewGuid().ToString(), -1), new(Guid.NewGuid().ToString(), 0)]))
            .IsValid.ShouldBeTrue();

    [Fact]
    public void Validate_ListsTheMissingGroups()
    {
        var found = GroupStreams.Of(tenantId: 7);
        var missing = new[] { Guid.NewGuid(), Guid.NewGuid() }.Order().ToList();

        var problem = Endpoints.Validate(Request((found.Id, 0), (missing[1], 1), (missing[0], 2)), [found]);

        problem.Status.ShouldBe(404);
        problem.Detail.ShouldBe($"Group(s) {missing[0]}, {missing[1]} not found or not updatable");
        problem.ShouldBeOfType<ApiErrorProblem>().Fields[Endpoints.MissingGroupIds].ShouldBe(missing);
    }

    [Fact]
    public void Validate_RequiresSiblings()
    {
        var parent = Guid.NewGuid();
        var a = GroupStreams.Of(tenantId: 7, parentId: parent);
        var cousin = GroupStreams.Of(tenantId: 7, parentId: Guid.NewGuid());
        var otherTenantsRoot = GroupStreams.Of(tenantId: 8);
        var root = GroupStreams.Of(tenantId: 7);

        Endpoints.Validate(Request((a.Id, 0), (cousin.Id, 1)), [a, cousin]).Detail.ShouldBe(Endpoints.NotSiblings);
        Endpoints.Validate(Request((root.Id, 0), (otherTenantsRoot.Id, 1)), [root, otherTenantsRoot]).Status.ShouldBe(400);
    }

    [Fact]
    public void Validate_Siblings_Pass()
    {
        var parent = Guid.NewGuid();
        var a = GroupStreams.Of(tenantId: 7, parentId: parent);
        var b = GroupStreams.Of(tenantId: 7, parentId: parent);

        Endpoints.Validate(Request((a.Id, 1), (b.Id, 0)), [a, b]).ShouldBeSameAs(WolverineContinue.NoProblems);
    }

    [Fact]
    public void Put_AppendsAChangeForEachGroupWhoseSortOrderChanges()
    {
        var a = GroupStreams.Of(tenantId: 7, version: 3, sortOrder: 0);
        var b = GroupStreams.Of(tenantId: 7, version: 5, sortOrder: null);
        var unchanged = GroupStreams.Of(tenantId: 7, sortOrder: 2);

        var (success, append) = Endpoints.Put(
            Request((a.Id, 1), (b.Id, 0), (unchanged.Id, 2)), [a, b, unchanged], Admin.Principal());

        success.ShouldBe(ApiSuccess.Instance);
        append.Streams.Select(s => (s.GroupId, s.Version)).ShouldBe([(a.Id, 3L), (b.Id, 5L)]);
        var changes = append.AllEvents.Cast<GroupSortOrderChanged>().ToList();
        changes.Select(c => (c.GroupId, c.SortOrder)).ShouldBe([(a.Id, 1), (b.Id, 0)]);
        changes.ShouldAllBe(c => c.ActorPersonUid == Admin.PersonUid);
    }

    [Fact]
    public void GroupSortOrderChanged_SetsTheSortOrder()
    {
        var group = GroupStreams.Of(tenantId: 7, sortOrder: 0);

        GroupViewProjection.Apply(new GroupSortOrderChanged(group.Id, 4, Guid.NewGuid(), DateTimeOffset.UtcNow), group.Group)
            .ShouldBe(group.Group with { SortOrder = 4 });
    }

    private static ReorderGroups Request(params (Guid GroupId, int SortOrder)[] updates) =>
        new([.. updates.Select(u => new GroupSortOrderUpdate(u.GroupId.ToString(), u.SortOrder))]);
}

[Trait("Category", "Database")]
[Collection(DatabaseCollection.Name)]
public class GroupReorderingDatabaseTests(DatabaseBackendFactory factory)
{
    [DatabaseFact]
    public async Task ReordersTheSiblings()
    {
        var tenant = Seed.NewTenant();
        var root = Seed.Group(tenant, "Root", sortOrder: 0);
        var a = Seed.Group(tenant, "A", root, sortOrder: 0);
        var b = Seed.Group(tenant, "B", root, sortOrder: 1);
        await factory.StoreAsync(root, a, b);
        var admin = factory.CreateClient(TestUser.LocalAdmin(tenant));

        var response = await admin.PutAsJsonAsync("/api/groups/sort-order", new
        {
            updates = new[] { new { group_id = b.GroupId, sort_order = 0 }, new { group_id = a.GroupId, sort_order = 1 } },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).ShouldBeJson(new { success = true });
        (await admin.GetApiAsync<List<GroupNode>>("/api/tree"))!.Select(g => (g.Label, g.SortOrder))
            .ShouldBe([("Root", (int?)0), ("B", 0), ("A", 1)], ignoreOrder: true);
    }

    [DatabaseFact]
    public async Task AMissingGroup_ChangesNothing()
    {
        var tenant = Seed.NewTenant();
        var a = Seed.Group(tenant, "A", sortOrder: 0);
        var theirs = Seed.Group(Seed.NewTenant(), "Theirs", sortOrder: 0);
        var unknown = Guid.NewGuid();
        await factory.StoreAsync(a, theirs);
        var admin = factory.CreateClient(TestUser.LocalAdmin(tenant));

        var response = await admin.PutAsJsonAsync("/api/groups/sort-order", new
        {
            updates = new[]
            {
                new { group_id = a.GroupId, sort_order = 5 },
                new { group_id = theirs.GroupId, sort_order = 1 },
                new { group_id = unknown, sort_order = 2 },
            },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var missing = new[] { theirs.GroupId, unknown }.Order().ToArray();
        (await response.ReadJsonAsync()).ShouldBeJson(new
        {
            error = $"Group(s) {missing[0]}, {missing[1]} not found or not updatable",
            missing_group_ids = missing,
        });
        (await admin.GetApiAsync<List<GroupNode>>("/api/tree"))!.ShouldHaveSingleItem().SortOrder.ShouldBe(0);
    }

    [DatabaseFact]
    public async Task NoUpdates_IsABadRequest()
    {
        var response = await factory.CreateClient(TestUser.Global())
            .PutAsJsonAsync("/api/groups/sort-order", new { updates = Array.Empty<object>() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).ShouldBeJson(new { error = ReorderGroups.NoUpdates });
    }

    public static TheoryData<string, TestUser?, HttpStatusCode> Callers => AdminWrites.Callers(HttpStatusCode.OK);

    [DatabaseTheory]
    [MemberData(nameof(Callers))]
    public async Task OnlyTheGroupsAdminsMayReorder(string _, TestUser? caller, HttpStatusCode expected)
    {
        var group = Seed.Group(AdminWrites.Tenant, "A", sortOrder: 0);
        await factory.StoreAsync(group);

        var response = await AdminWrites.Client(factory, caller).PutAsJsonAsync("/api/groups/sort-order",
            new { updates = new[] { new { group_id = group.GroupId, sort_order = 1 } } });

        response.StatusCode.ShouldBe(expected);
    }
}
