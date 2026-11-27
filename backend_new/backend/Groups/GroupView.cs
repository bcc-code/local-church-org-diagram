using Marten.Events.Aggregation;

namespace Backend.Groups;

/// <summary>A group in the org hierarchy (the legacy <c>groups</c> table), one per group stream.</summary>
public record GroupView(
    Guid Id,
    string Name,
    Guid? ParentId,
    long TenantId,
    int? SortOrder,
    DateTimeOffset CreatedAt);

/// <summary>Inline: updated in the transaction that appends the group's events.</summary>
public class GroupViewProjection : SingleStreamProjection<GroupView, Guid>
{
    public static GroupView Create(GroupImported e) =>
        new(e.GroupId, e.Name, e.ParentId, e.TenantId, e.SortOrder, e.CreatedAt);
}
