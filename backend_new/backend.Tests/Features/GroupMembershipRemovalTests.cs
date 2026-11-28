using System.Net;
using System.Net.Http.Json;
using Backend.Api;
using Backend.Features;
using Backend.Features.GroupMembershipListing;
using Backend.Features.GroupMembershipRemoval;
using Backend.Features.MembershipTree;
using Backend.Groups;
using Backend.Tests.Api;
using Backend.Tests.Auth;
using Backend.Tests.Database;
using Shouldly;
using Wolverine.Http;
using Endpoints = Backend.Features.GroupMembershipRemoval.Endpoints;

namespace Backend.Tests.Features;

public class GroupMembershipRemovalTests
{
    private static readonly Guid Person = Guid.NewGuid();

    [Fact]
    public void Validator_RequiresBothIds() =>
        new RemoveGroupMember.Validator().Validate(new RemoveGroupMember(Guid.NewGuid().ToString(), null))
            .Errors.First().ErrorMessage.ShouldBe(GroupMembershipRequests.BothRequired);

    [Fact]
    public void Validate_UnknownGroupOrMember_IsNotFound()
    {
        Endpoints.Validate(null, null).Detail.ShouldBe(GroupMembershipRequests.MemberNotFound);
        Endpoints.Validate(GroupStreams.Of(tenantId: 7), null).Status.ShouldBe(404);
    }

    [Fact]
    public void Validate_AMember_Passes()
    {
        var group = GroupStreams.Of(tenantId: 7);

        Endpoints.Validate(group, GroupStreams.MembershipIn(group, Person)).ShouldBeSameAs(WolverineContinue.NoProblems);
    }

    [Fact]
    public void Delete_AppendsMembershipRemoved()
    {
        var group = GroupStreams.Of(tenantId: 7, version: 4);
        var admin = TestUser.Global();

        var (success, append) = Endpoints.Delete(
            new RemoveGroupMember(group.Id.ToString(), Person.ToString()), group, admin.Principal());

        success.ShouldBe(ApiSuccess.Instance);
        var stream = append.Streams.ShouldHaveSingleItem();
        stream.Version.ShouldBe(4);
        var removed = stream.Events.ShouldHaveSingleItem().ShouldBeOfType<MembershipRemoved>();
        removed.ShouldBe(new MembershipRemoved(group.Id, Person, admin.PersonUid, removed.OccurredAt));
    }

    [Fact]
    public void MembershipRemoved_UncountsTheMember()
    {
        var group = GroupStreams.Of(tenantId: 7);

        GroupViewProjection.Apply(new MembershipRemoved(group.Id, Person, Guid.NewGuid(), DateTimeOffset.UtcNow), group.Group)
            .MemberCount.ShouldBe(group.Group.MemberCount - 1);
    }
}

[Trait("Category", "Database")]
[Collection(DatabaseCollection.Name)]
public class GroupMembershipRemovalDatabaseTests(DatabaseBackendFactory factory)
{
    [DatabaseFact]
    public async Task RemovesTheMember()
    {
        var tenant = Seed.NewTenant();
        var group = Seed.Group(tenant, "Styre");
        var person = Guid.NewGuid();
        var other = Guid.NewGuid();
        await factory.StoreAsync(group, Seed.Member(group, person, "Leder"), Seed.Member(group, other));
        var admin = factory.CreateClient(TestUser.LocalAdmin(tenant));

        var response = await admin.DeleteAsJsonAsync(new { group_id = group.GroupId, person_uid = person });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).ShouldBeJson(new { success = true });
        (await admin.GetApiAsync<List<GroupMember>>($"/api/persons?group_id={group.GroupId}"))!
            .ShouldHaveSingleItem().PersonUid.ShouldBe(other);
        (await admin.GetApiAsync<List<GroupNode>>("/api/tree"))!.ShouldHaveSingleItem().MemberCount.ShouldBe(1);
        (await admin.GetApiAsync<List<string>>("/api/titles"))!.ShouldBeEmpty();
    }

    [DatabaseFact]
    public async Task RemovedThenAddedAgain_IsANewMembership()
    {
        var tenant = Seed.NewTenant();
        var group = Seed.Group(tenant, "Styre");
        var person = Guid.NewGuid();
        await factory.StoreAsync(group, Seed.Member(group, person, "Leder"));
        var admin = factory.CreateClient(TestUser.LocalAdmin(tenant));
        var key = new { group_id = group.GroupId, person_uid = person };

        (await admin.DeleteAsJsonAsync(key)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.PostAsJsonAsync("/api/group-membership", key)).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await admin.GetApiAsync<List<GroupMember>>($"/api/persons?group_id={group.GroupId}"))!
            .ShouldHaveSingleItem().ShouldBe(new GroupMember(person, "?", null, null, null));
        (await admin.GetApiAsync<List<GroupNode>>("/api/tree"))!.ShouldHaveSingleItem().MemberCount.ShouldBe(1);
    }

    [DatabaseFact]
    public async Task NotAMember_IsNotFound()
    {
        var tenant = Seed.NewTenant();
        var group = Seed.Group(tenant, "Styre");
        await factory.StoreAsync(group);

        var response = await factory.CreateClient(TestUser.LocalAdmin(tenant))
            .DeleteAsJsonAsync(new { group_id = group.GroupId, person_uid = Guid.NewGuid() });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadJsonAsync()).ShouldBeJson(new { error = GroupMembershipRequests.MemberNotFound });
    }

    public static TheoryData<string, TestUser?, HttpStatusCode> Callers => AdminWrites.Callers(HttpStatusCode.OK);

    [DatabaseTheory]
    [MemberData(nameof(Callers))]
    public async Task OnlyTheGroupsAdminsMayRemove(string _, TestUser? caller, HttpStatusCode expected)
    {
        var group = Seed.Group(AdminWrites.Tenant, "Styre");
        var person = Guid.NewGuid();
        await factory.StoreAsync(group, Seed.Member(group, person));

        var response = await AdminWrites.Client(factory, caller)
            .DeleteAsJsonAsync(new { group_id = group.GroupId, person_uid = person });

        response.StatusCode.ShouldBe(expected);
    }
}

public static class GroupMembershipClientExtensions
{
    /// <summary>DELETE /api/group-membership, whose IDs are in a JSON body as with the Flask backend.</summary>
    public static Task<HttpResponseMessage> DeleteAsJsonAsync(this HttpClient client, object body) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/group-membership")
        {
            Content = JsonContent.Create(body),
        });
}
