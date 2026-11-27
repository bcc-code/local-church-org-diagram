using System.Security.Claims;
using Backend.Auth;
using Backend.CoreApi;
using Backend.Groups;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace Backend.Features.GroupMembershipListing;

/// <param name="Name">The Core API display name, or "?" when the Core API doesn't know the person.</param>
public record GroupMember(Guid PersonUid, string Name, string? ProfilePicture, string? Title, string? Link);

/// <param name="GroupId">Required, a group ID (as the Flask backend's <c>?group_id=</c>).</param>
public record GroupMembersQuery([FromQuery(Name = "group_id")] string? GroupId)
{
    public Guid Id => Guid.Parse(GroupId!);

    public class Validator : AbstractValidator<GroupMembersQuery>
    {
        public Validator() =>
            RuleFor(x => x.GroupId).Cascade(CascadeMode.Stop)
                .Must(id => !string.IsNullOrEmpty(id)).WithMessage("No group_id provided")
                .Must(id => Guid.TryParse(id, out _)).WithMessage("Invalid group_id")
                // Keyed like the query parameter, not the property
                .OverridePropertyName("group_id");
    }
}

/// <summary>
/// A group's members in the user's church. A group of another church (or none) has no members there, so it lists
/// none, as with the Flask backend.
/// </summary>
public static class Endpoints
{
    public const string UnknownPersonName = "?";

    // The user's church: its members and its admins
    public static Task<ProblemDetails> Before(ClaimsPrincipal user, IAuthorizationService authorization) =>
        authorization.AuthorizeTenantAsync(user, user.GetChurchId()!.Value, AuthPolicies.TenantReader);

    [WolverineGet("/persons", OperationId = "ListGroupMembers")]
    public static async Task<IReadOnlyList<GroupMember>> Get(
        [FromQuery] GroupMembersQuery query, ClaimsPrincipal user, IQuerySession session, IPersonDirectory persons,
        CancellationToken ct)
    {
        var groupId = query.Id;
        var tenantId = user.GetChurchId()!.Value;
        var memberships = await session.Query<MembershipView>()
            .Where(m => m.GroupId == groupId && m.TenantId == tenantId)
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
