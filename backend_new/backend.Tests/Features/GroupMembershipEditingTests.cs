using System.Net;
using System.Net.Http.Json;
using Backend.Features;
using Backend.Features.GroupMembershipEditing;
using Backend.Features.GroupMembershipListing;
using Backend.Groups;
using Backend.Tests.Api;
using Backend.Tests.Auth;
using Backend.Tests.Database;
using Microsoft.AspNetCore.Http.HttpResults;
using Shouldly;
using Wolverine.Http;
using Endpoints = Backend.Features.GroupMembershipEditing.Endpoints;

namespace Backend.Tests.Features;

public class GroupMembershipEditingTests
{
    private static readonly Guid Person = Guid.NewGuid();
    private static readonly TestUser Admin = TestUser.LocalAdmin(7);

    [Fact]
    public void Validator_RejectsAnInvalidGroupId() =>
        new UpdateGroupMember.Validator().Validate(new UpdateGroupMember("1", Person.ToString(), "Leder", null))
            .Errors.First().ErrorMessage.ShouldBe(GroupMembershipRequests.InvalidGroupId);

    [Fact]
    public void Validate_NotAMember_IsNotFound() =>
        Endpoints.Validate(GroupStreams.Of(tenantId: 7), null).Detail.ShouldBe(GroupMembershipRequests.MemberNotFound);

    [Fact]
    public void Validate_AMember_Passes()
    {
        var group = GroupStreams.Of(tenantId: 7);

        Endpoints.Validate(group, GroupStreams.MembershipIn(group, Person)).ShouldBeSameAs(WolverineContinue.NoProblems);
    }

    [Fact]
    public void Put_ChangesTheTitle()
    {
        var (group, membership) = Member(title: "Leder", link: "https://x");

        var (result, append) = Endpoints.Put(Request(group, title: "Kasserer"), group, membership, Admin.Principal());

        result.Result.ShouldBeOfType<Ok<Backend.Api.ApiSuccess>>();
        var changed = append.AllEvents.ShouldHaveSingleItem().ShouldBeOfType<MembershipTitleChanged>();
        changed.ShouldBe(new MembershipTitleChanged(group.Id, Person, "Kasserer", Admin.PersonUid, changed.OccurredAt));
        append.Streams.ShouldHaveSingleItem().Version.ShouldBe(group.Version);
    }

    [Fact]
    public void Put_ChangesBoth_AndClearsWithAnEmptyString()
    {
        var (group, membership) = Member(title: "Leder", link: "https://x");

        var (_, append) = Endpoints.Put(Request(group, title: "", link: "https://y"), group, membership, Admin.Principal());

        append.AllEvents.Select(e => e.GetType()).ShouldBe([typeof(MembershipTitleChanged), typeof(MembershipLinkChanged)]);
        append.AllEvents.OfType<MembershipTitleChanged>().Single().Title.ShouldBe("");
        append.AllEvents.OfType<MembershipLinkChanged>().Single().Link.ShouldBe("https://y");
    }

    [Fact]
    public void Put_TheCurrentValues_ChangesNothing()
    {
        var (group, membership) = Member(title: "Leder", link: null);

        var (result, append) = Endpoints.Put(Request(group, title: "Leder"), group, membership, Admin.Principal());

        result.Result.ShouldBeOfType<Ok<Backend.Api.ApiSuccess>>();
        append.AllEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Put_NeitherField_IsNotModified()
    {
        var (group, membership) = Member(title: "Leder", link: null);

        var (result, append) = Endpoints.Put(Request(group), group, membership, Admin.Principal());

        result.Result.ShouldBeOfType<StatusCodeHttpResult>().StatusCode.ShouldBe(304);
        append.AllEvents.ShouldBeEmpty();
    }

    [Fact]
    public void TitleAndLinkChanged_SetThem()
    {
        var (group, membership) = Member(title: "Leder", link: null);

        var view = MembershipViewProjection.Apply(
            new MembershipLinkChanged(group.Id, Person, "https://x", Guid.NewGuid(), DateTimeOffset.UtcNow),
            MembershipViewProjection.Apply(
                new MembershipTitleChanged(group.Id, Person, "", Guid.NewGuid(), DateTimeOffset.UtcNow), membership));

        view.ShouldBe(membership with { Title = "", Link = "https://x" });
    }

    private static (GroupStream, MembershipView) Member(string? title, string? link)
    {
        var group = GroupStreams.Of(tenantId: 7, version: 2);
        return (group, GroupStreams.MembershipIn(group, Person, title, link));
    }

    private static UpdateGroupMember Request(GroupStream group, string? title = null, string? link = null) =>
        new(group.Id.ToString(), Person.ToString(), title, link);
}

[Trait("Category", "Database")]
[Collection(DatabaseCollection.Name)]
public class GroupMembershipEditingDatabaseTests(DatabaseBackendFactory factory)
{
    [DatabaseFact]
    public async Task SetsTitleAndLink()
    {
        var tenant = Seed.NewTenant();
        var group = Seed.Group(tenant, "Styre");
        var person = Guid.NewGuid();
        await factory.StoreAsync(group, Seed.Member(group, person, "Leder"));
        var admin = factory.CreateClient(TestUser.LocalAdmin(tenant));

        var title = await admin.PutAsJsonAsync("/api/group-membership",
            new { group_id = group.GroupId, person_uid = person, title = "Kasserer" });
        var link = await admin.PutAsJsonAsync("/api/group-membership",
            new { group_id = group.GroupId, person_uid = person, link = "https://x" });

        title.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await title.ReadJsonAsync()).ShouldBeJson(new { success = true });
        link.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.GetApiAsync<List<GroupMember>>($"/api/persons?group_id={group.GroupId}"))!
            .ShouldHaveSingleItem().ShouldBe(new GroupMember(person, "?", null, "Kasserer", "https://x"));
    }

    // Cleared titles are stored as empty strings, as the Flask backend did, and the title listings leave them out
    [DatabaseFact]
    public async Task AClearedTitle_IsLeftOutOfTheTitles()
    {
        var tenant = Seed.NewTenant();
        var group = Seed.Group(tenant, "Styre");
        var person = Guid.NewGuid();
        await factory.StoreAsync(group, Seed.Member(group, person, "Leder"));
        var admin = factory.CreateClient(TestUser.LocalAdmin(tenant));

        (await admin.PutAsJsonAsync("/api/group-membership",
            new { group_id = group.GroupId, person_uid = person, title = "" })).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await admin.GetApiAsync<List<GroupMember>>($"/api/persons?group_id={group.GroupId}"))!
            .ShouldHaveSingleItem().Title.ShouldBe("");
        (await admin.GetApiAsync<List<string>>("/api/titles"))!.ShouldBeEmpty();
        (await admin.GetAsync("/api/titles/%20/persons")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.GetApiAsync<List<object>>("/api/titles/%20/persons"))!.ShouldBeEmpty();
    }

    [DatabaseFact]
    public async Task NeitherField_IsNotModified()
    {
        var tenant = Seed.NewTenant();
        var group = Seed.Group(tenant, "Styre");
        var person = Guid.NewGuid();
        await factory.StoreAsync(group, Seed.Member(group, person));

        var response = await factory.CreateClient(TestUser.LocalAdmin(tenant))
            .PutAsJsonAsync("/api/group-membership", new { group_id = group.GroupId, person_uid = person });

        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [DatabaseFact]
    public async Task NotAMember_IsNotFound()
    {
        var tenant = Seed.NewTenant();
        var group = Seed.Group(tenant, "Styre");
        await factory.StoreAsync(group);

        var response = await factory.CreateClient(TestUser.LocalAdmin(tenant))
            .PutAsJsonAsync("/api/group-membership", new { group_id = group.GroupId, person_uid = Guid.NewGuid(), title = "X" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadJsonAsync()).ShouldBeJson(new { error = GroupMembershipRequests.MemberNotFound });
    }

    public static TheoryData<string, TestUser?, HttpStatusCode> Callers => AdminWrites.Callers(HttpStatusCode.OK);

    [DatabaseTheory]
    [MemberData(nameof(Callers))]
    public async Task OnlyTheGroupsAdminsMayEdit(string _, TestUser? caller, HttpStatusCode expected)
    {
        var group = Seed.Group(AdminWrites.Tenant, "Styre");
        var person = Guid.NewGuid();
        await factory.StoreAsync(group, Seed.Member(group, person));

        var response = await AdminWrites.Client(factory, caller)
            .PutAsJsonAsync("/api/group-membership", new { group_id = group.GroupId, person_uid = person, title = "X" });

        response.StatusCode.ShouldBe(expected);
    }
}
