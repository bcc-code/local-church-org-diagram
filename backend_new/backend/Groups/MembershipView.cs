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
    }

    public static MembershipView Create(MembershipImported e) =>
        new(GroupIds.Membership(e.GroupId, e.PersonUid), e.GroupId, e.PersonUid, e.Title, e.Link, e.TenantId,
            e.CreatedAt);
}
