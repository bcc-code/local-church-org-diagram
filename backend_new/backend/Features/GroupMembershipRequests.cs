using System.Security.Claims;
using Backend.Auth;
using Backend.Groups;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Features;

/// <summary>
/// A request about one person's membership in a group (/api/group-membership), with the Flask backend's body: IDs as
/// strings, so a missing or malformed one gets its message rather than a JSON binding error.
/// </summary>
public interface IGroupMembershipRequest
{
    string? GroupId { get; }
    string? PersonUid { get; }
}

/// <summary>What the membership endpoints (add, edit, remove) share.</summary>
public static class GroupMembershipRequests
{
    public const string BothRequired = "Both group_id and person_uid are required";
    public const string InvalidGroupId = "Invalid group_id";
    public const string InvalidPersonUid = "Invalid person_uid";
    public const string MemberNotFound = "Member not found in group";

    /// <summary>Both IDs present, then each a Guid, with the Flask backend's messages.</summary>
    public static void AddMembershipKeyRules<T>(this AbstractValidator<T> validator) where T : IGroupMembershipRequest
    {
        validator.RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x.GroupId) && !string.IsNullOrEmpty(x.PersonUid))
            .WithMessage(BothRequired)
            .OverridePropertyName("group_id");
        validator.When(x => !string.IsNullOrEmpty(x.GroupId) && !string.IsNullOrEmpty(x.PersonUid), () =>
        {
            validator.RuleFor(x => x.GroupId)
                .Must(id => Guid.TryParse(id, out _)).WithMessage(InvalidGroupId)
                .OverridePropertyName("group_id");
            validator.RuleFor(x => x.PersonUid)
                .Must(uid => Guid.TryParse(uid, out _)).WithMessage(InvalidPersonUid)
                .OverridePropertyName("person_uid");
        });
    }

    /// <summary>The request's IDs; only after validation.</summary>
    public static (Guid GroupId, Guid PersonUid) Key(this IGroupMembershipRequest request) =>
        (Guid.Parse(request.GroupId!), Guid.Parse(request.PersonUid!));

    /// <summary>
    /// The group, if the user may administer it (<see cref="AuthPolicies.TenantAdmin"/> for the group's own tenant), and
    /// the person's membership in it. A group the user may not administer is treated as unknown (null), as the Flask
    /// backend's tenant-filtered queries did: 404, not 403.
    /// </summary>
    public static async Task<(GroupStream?, MembershipView?)> LoadAsync(
        IGroupMembershipRequest request, IQuerySession session, ClaimsPrincipal user,
        IAuthorizationService authorization, CancellationToken ct)
    {
        var (groupId, personUid) = request.Key();
        var group = await GroupStream.LoadAsync(session, groupId, ct);
        if (group is null || !await authorization.IsAllowedAsync(user, group.Group, AuthPolicies.TenantAdmin))
            return (null, null);

        var membership = await session.LoadAsync<MembershipView>(GroupIds.Membership(groupId, personUid), ct);
        return (group, membership);
    }
}
