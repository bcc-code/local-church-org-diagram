using System.Security.Claims;
using Backend.Auth;
using Backend.Groups;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace Backend.Features.PersonMembershipListing;

/// <summary>A group the person is a member of.</summary>
public record PersonGroup(Guid Id, string Name, int MemberCount);

public static class PersonMembershipListingEndpoint
{
    public static Task<ProblemDetails> Before(ClaimsPrincipal user, IAuthorizationService authorization) =>
        authorization.AuthorizeOwnChurchAsync(user);

    [WolverineGet("/persons/{personUid}/groups", OperationId = "ListPersonGroups")]
    public static async Task<IReadOnlyList<PersonGroup>> Get(
        Guid personUid, ClaimsPrincipal user, IQuerySession session, CancellationToken ct)
    {
        var tenantId = user.OwnChurchId();
        var groupIds = await session.Query<MembershipView>()
            .Where(m => m.PersonUid == personUid && m.TenantId == tenantId)
            .Select(m => m.GroupId)
            .ToListAsync(ct);
        if (groupIds.Count == 0)
            return [];

        var groups = await session.LoadManyAsync<GroupView>(ct, groupIds.Distinct());
        return ToPersonGroups(groups, tenantId);
    }

    /// <summary>The church's groups, in creation order (the Flask backend's legacy ID order).</summary>
    public static IReadOnlyList<PersonGroup> ToPersonGroups(IEnumerable<GroupView> groups, long tenantId) =>
    [
        .. groups
            .Where(g => g.TenantId == tenantId)
            .OrderBy(g => g.CreatedAt)
            .ThenBy(g => g.Id)
            .Select(g => new PersonGroup(g.Id, g.Name, g.MemberCount)),
    ];
}
