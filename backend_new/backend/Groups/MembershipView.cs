using Marten.Events.Projections;

namespace Backend.Groups;

/// <summary>
/// A person's membership in a group (the legacy <c>group_membership</c> table). Its ID is
/// <see cref="GroupIds.Membership"/>: one per group and person, like the table's unique (group_id, bcc_person_uid).
/// </summary>
public record MembershipView(
    Guid Id,
    Guid GroupId,
    Guid PersonUid,
    string? Title,
    string? Link,
    long TenantId,
    DateTimeOffset CreatedAt);

/// <summary>
/// Inline. Memberships are events on their group's stream, so this projects across streams, one document per
/// membership.
/// </summary>
public class MembershipViewProjection : MultiStreamProjection<MembershipView, Guid>
{
    public MembershipViewProjection()
    {
        Identity<MembershipImported>(e => GroupIds.Membership(e.GroupId, e.PersonUid));
        Identity<MembershipAdded>(e => GroupIds.Membership(e.GroupId, e.PersonUid));
        Identity<MembershipTitleChanged>(e => GroupIds.Membership(e.GroupId, e.PersonUid));
        Identity<MembershipLinkChanged>(e => GroupIds.Membership(e.GroupId, e.PersonUid));
        Identity<MembershipRemoved>(e => GroupIds.Membership(e.GroupId, e.PersonUid));
        // A removed membership's document is deleted; adding the person again creates a new one
        DeleteEvent<MembershipRemoved>();
    }

    public static MembershipView Create(MembershipImported e) =>
        new(GroupIds.Membership(e.GroupId, e.PersonUid), e.GroupId, e.PersonUid, e.Title, e.Link, e.TenantId,
            e.CreatedAt);

    public static MembershipView Create(MembershipAdded e) =>
        new(GroupIds.Membership(e.GroupId, e.PersonUid), e.GroupId, e.PersonUid, e.Title, null, e.TenantId,
            e.OccurredAt);

    public static MembershipView Apply(MembershipTitleChanged e, MembershipView view) => view with { Title = e.Title };

    public static MembershipView Apply(MembershipLinkChanged e, MembershipView view) => view with { Link = e.Link };
}
