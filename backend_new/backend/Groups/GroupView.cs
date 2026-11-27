using Marten.Events.Aggregation;

namespace Backend.Groups;

/// <summary>A group in the org hierarchy (the legacy <c>groups</c> table), one per group stream.</summary>
/// <param name="MemberCount">Memberships on the group's stream, so listings need no count query.</param>
public record GroupView(
    Guid Id,
    string Name,
    Guid? ParentId,
    long TenantId,
    int? SortOrder,
    DateTimeOffset CreatedAt,
    int MemberCount = 0);

/// <summary>
/// Inline: updated in the transaction that appends the group's events. A change here only reaches existing views
/// through a rebuild (see README, "Rebuilding projections").
/// </summary>
public class GroupViewProjection : SingleStreamProjection<GroupView, Guid>
{
    public static GroupView Create(GroupImported e) =>
        new(e.GroupId, e.Name, e.ParentId, e.TenantId, e.SortOrder, e.CreatedAt);

    public static GroupView Apply(MembershipImported _, GroupView view) =>
        view with { MemberCount = view.MemberCount + 1 };
}
