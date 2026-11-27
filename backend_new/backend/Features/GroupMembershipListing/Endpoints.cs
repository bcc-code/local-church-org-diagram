using System.Security.Claims;
using Backend.Auth;
using Backend.CoreApi;
using Backend.Groups;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;
using Wolverine.Http.Marten;

namespace Backend.Features.GroupMembershipListing;

/// <param name="Name">The Core API display name, or "?" when the Core API doesn't know the person.</param>
public record GroupMember(Guid PersonUid, string Name, string? ProfilePicture, string? Title, string? Link);

public static class Endpoints
{
    public const string UnknownPersonName = "?";

    // Against the group's own tenant: its church's members and its admins
    public static Task<ProblemDetails> Before(GroupView group, ClaimsPrincipal user, IAuthorizationService authorization) =>
        authorization.AuthorizeTenantAsync(user, group.TenantId, AuthPolicies.TenantReader);

    [WolverineGet("/groups/{id}/members", OperationId = "ListGroupMembers")]
    public static async Task<IReadOnlyList<GroupMember>> Get(
        [Document] GroupView group, IQuerySession session, IPersonDirectory persons, CancellationToken ct)
    {
        var memberships = await session.Query<MembershipView>()
            .Where(m => m.GroupId == group.Id && m.TenantId == group.TenantId)
            .ToListAsync(ct);
        if (memberships.Count == 0)
            return [];

        var found = await persons.GetByUidsAsync(memberships.Select(m => m.PersonUid), ct);
        return ToMembers(memberships, found);
    }

    /// <summary>Every membership, with the person's details where the Core API has them, in <see cref="MemberOrdering"/>.</summary>
    public static IReadOnlyList<GroupMember> ToMembers(
        IEnumerable<MembershipView> memberships, IEnumerable<PersonSummary> persons)
    {
        var personsByUid = persons.ToDictionary(p => p.Uid);
        var members = memberships.Select(m =>
        {
            var person = personsByUid.GetValueOrDefault(m.PersonUid);
            return new GroupMember(m.PersonUid, person?.DisplayName ?? UnknownPersonName, person?.ProfilePicture,
                m.Title, m.Link);
        });
        return [.. MemberOrdering.Sort(members)];
    }
}
