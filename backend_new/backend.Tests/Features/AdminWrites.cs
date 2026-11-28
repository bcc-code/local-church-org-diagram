using System.Net;
using Backend.Groups;
using Backend.Tests.Auth;
using Backend.Tests.Database;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Backend.Tests.Features;

/// <summary>
/// The authorization matrix of the admin write endpoints: only the group's admins (Global Admins, and Local Admins of
/// the group's tenant whatever their own church) may change it. Everyone else signed in gets 404, as for an unknown
/// group; a person without membership is refused by the default policy (403) and signed out users get 401.
/// </summary>
public static class AdminWrites
{
    /// <summary>The tenant of the groups the matrix tests change.</summary>
    public static readonly long Tenant = Seed.NewTenant();

    public static TheoryData<string, TestUser?, HttpStatusCode> Callers(HttpStatusCode allowed)
    {
        var otherTenant = Seed.NewTenant();
        return new()
        {
            { "signed out", null, HttpStatusCode.Unauthorized },
            { "church member without membership", TestUser.NonMember(Tenant), HttpStatusCode.Forbidden },
            { "member of the group's church", TestUser.Member(Tenant), HttpStatusCode.NotFound },
            { "Local Admin of another tenant", TestUser.LocalAdmin(otherTenant, churchId: Tenant), HttpStatusCode.NotFound },
            { "Local Admin of the group's tenant, other church", TestUser.LocalAdmin(Tenant, churchId: otherTenant), allowed },
            { "Global Admin of another church", TestUser.Global(churchId: otherTenant), allowed },
        };
    }

    public static HttpClient Client(DatabaseBackendFactory factory, TestUser? caller) =>
        caller is null
            ? factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })
            : factory.CreateClient(caller);
}

/// <summary>Groups and memberships in memory, for unit tests of the admin endpoints' pure methods.</summary>
public static class GroupStreams
{
    private static readonly DateTimeOffset Day1 = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static GroupStream Of(long tenantId, long version = 1, Guid? parentId = null, int? sortOrder = null) =>
        new(new GroupView(Guid.NewGuid(), "Group", parentId, tenantId, sortOrder, Day1, MemberCount: 2), version);

    public static MembershipView MembershipIn(
        GroupStream group, Guid personUid, string? title = null, string? link = null) =>
        new(GroupIds.Membership(group.Id, personUid), group.Id, personUid, title, link, group.Group.TenantId, Day1);
}
