using System.Net;
using System.Net.Http.Json;
using Backend.Features;
using Backend.Api;
using Backend.Features.GroupMembershipAddition;
using Backend.Features.GroupMembershipListing;
using Backend.Features.MembershipTree;
using Endpoints = Backend.Features.GroupMembershipAddition.Endpoints;
using Backend.Groups;
using Backend.Tests.Api;
using Backend.Tests.Auth;
using Backend.Tests.Database;
using FluentValidation.TestHelper;
using Shouldly;
using Wolverine.Http;

namespace Backend.Tests.Features;

public class GroupMembershipAdditionTests
{
    private static readonly Guid Person = Guid.NewGuid();

    [Theory]
    [InlineData(null, "p")]
    [InlineData("g", null)]
    [InlineData("", "p")]
    public void Validator_RequiresBothIds(string? groupId, string? personUid) =>
        Validate(new AddGroupMember(groupId, personUid, null)).ShouldBe(GroupMembershipRequests.BothRequired);

    [Fact]
    public void Validator_RejectsAnInvalidGroupId() =>
        Validate(new AddGroupMember("1", Person.ToString(), null)).ShouldBe(GroupMembershipRequests.InvalidGroupId);

    [Fact]
    public void Validator_RejectsAnInvalidPersonUid() =>
        Validate(new AddGroupMember(Guid.NewGuid().ToString(), " ", null))
            .ShouldBe(GroupMembershipRequests.InvalidPersonUid);

    [Fact]
    public void Validator_AcceptsGuids() =>
        new AddGroupMember.Validator().TestValidate(new AddGroupMember(Guid.NewGuid().ToString(), Person.ToString(), "Leder"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_UnknownGroup_IsNotFound()
    {
        var problem = Endpoints.Validate(null, null);

        problem.Status.ShouldBe(404);
        problem.Detail.ShouldBe(Endpoints.GroupNotFound);
    }

    [Fact]
    public void Validate_AnExistingMember_IsAConflict()
    {
        var group = GroupStreams.Of(tenantId: 7);

        Endpoints.Validate(group, GroupStreams.MembershipIn(group, Person)).Status.ShouldBe(409);
    }

    [Fact]
    public void Validate_ANewMember_Passes() =>
        Endpoints.Validate(GroupStreams.Of(tenantId: 7), null).ShouldBeSameAs(WolverineContinue.NoProblems);

    [Fact]
    public void Post_AppendsMembershipAdded_AtTheLoadedVersion()
    {
        var group = GroupStreams.Of(tenantId: 7, version: 3);
        var admin = TestUser.LocalAdmin(7);

        var (created, append) = Endpoints.Post(
            new AddGroupMember(group.Id.ToString(), Person.ToString(), "Leder"), group, admin.Principal());

        created.StatusCode.ShouldBe(201);
        created.Value.ShouldBe(ApiSuccess.Instance);
        var stream = append.Streams.ShouldHaveSingleItem();
        stream.GroupId.ShouldBe(group.Id);
        stream.Version.ShouldBe(3);
        var added = stream.Events.ShouldHaveSingleItem().ShouldBeOfType<MembershipAdded>();
        added.ShouldBe(new MembershipAdded(group.Id, Person, "Leder", 7, admin.PersonUid, added.OccurredAt));
        added.OccurredAt.ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void MembershipAdded_CreatesTheMembershipAndCountsIt()
    {
        var at = DateTimeOffset.UtcNow;
        var group = GroupStreams.Of(tenantId: 7);
        var added = new MembershipAdded(group.Id, Person, "Leder", 7, Guid.NewGuid(), at);

        MembershipViewProjection.Create(added)
            .ShouldBe(new MembershipView(GroupIds.Membership(group.Id, Person), group.Id, Person, "Leder", null, 7, at));
        GroupViewProjection.Apply(added, group.Group).MemberCount.ShouldBe(group.Group.MemberCount + 1);
    }

    private static string Validate(AddGroupMember request) =>
        new AddGroupMember.Validator().Validate(request).Errors.First().ErrorMessage;
}

[Trait("Category", "Database")]
[Collection(DatabaseCollection.Name)]
public class GroupMembershipAdditionDatabaseTests(DatabaseBackendFactory factory)
{
    [DatabaseFact]
    public async Task AddsTheMember()
    {
        var tenant = Seed.NewTenant();
        var group = Seed.Group(tenant, "Styre");
        var person = Guid.NewGuid();
        await factory.StoreAsync(group);
        var admin = factory.CreateClient(TestUser.LocalAdmin(tenant));

        var response = await admin.PostAsJsonAsync("/api/group-membership",
            new { group_id = group.GroupId, person_uid = person, title = "Leder" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.ReadJsonAsync()).ShouldBeJson(new { success = true });
        var members = await admin.GetApiAsync<List<GroupMember>>($"/api/persons?group_id={group.GroupId}");
        members!.ShouldHaveSingleItem().ShouldBe(new GroupMember(person, "?", null, "Leder", null));
        (await admin.GetApiAsync<List<GroupNode>>("/api/tree"))!
            .ShouldHaveSingleItem().MemberCount.ShouldBe(1);
    }

    [DatabaseFact]
    public async Task AnExistingMember_IsAConflict()
    {
        var tenant = Seed.NewTenant();
        var group = Seed.Group(tenant, "Styre");
        var person = Guid.NewGuid();
        await factory.StoreAsync(group, Seed.Member(group, person));

        var response = await factory.CreateClient(TestUser.Global())
            .PostAsJsonAsync("/api/group-membership", new { group_id = group.GroupId, person_uid = person });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadJsonAsync()).ShouldBeJson(new { error = Endpoints.AlreadyMember });
    }

    [DatabaseFact]
    public async Task UnknownGroup_IsNotFound()
    {
        var response = await factory.CreateClient(TestUser.Global())
            .PostAsJsonAsync("/api/group-membership", new { group_id = Guid.NewGuid(), person_uid = Guid.NewGuid() });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadJsonAsync()).ShouldBeJson(new { error = Endpoints.GroupNotFound });
    }

    [DatabaseFact]
    public async Task MissingIds_IsABadRequest()
    {
        var response = await factory.CreateClient(TestUser.Global())
            .PostAsJsonAsync("/api/group-membership", new { group_id = Guid.NewGuid() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).ShouldBeJson(new { error = GroupMembershipRequests.BothRequired });
    }

    [DatabaseTheory]
    [InlineData("", ApiConventions.NoJsonData)]
    [InlineData("null", ApiConventions.NoJsonData)]
    [InlineData("{}", ApiConventions.NoJsonData)]
    [InlineData("[]", ApiConventions.NoJsonData)]
    [InlineData("{\"group_id\":", ApiConventions.InvalidJson)]
    public async Task NoJsonData_IsABadRequest(string body, string error)
    {
        var response = await factory.CreateClient(TestUser.Global()).PostAsync("/api/group-membership",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).ShouldBeJson(new { error });
    }

    [DatabaseFact]
    public async Task SignedOut_WithoutABody_IsUnauthorized() =>
        (await AdminWrites.Client(factory, null).PostAsync("/api/group-membership", null))
        .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    public static TheoryData<string, TestUser?, HttpStatusCode> Callers => AdminWrites.Callers(HttpStatusCode.Created);

    [DatabaseTheory]
    [MemberData(nameof(Callers))]
    public async Task OnlyTheGroupsAdminsMayAdd(string _, TestUser? caller, HttpStatusCode expected)
    {
        var group = Seed.Group(AdminWrites.Tenant, "Styre");
        await factory.StoreAsync(group);

        var response = await AdminWrites.Client(factory, caller)
            .PostAsJsonAsync("/api/group-membership", new { group_id = group.GroupId, person_uid = Guid.NewGuid() });

        response.StatusCode.ShouldBe(expected);
    }
}
