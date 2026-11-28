using Marten;
using Wolverine.Marten;

namespace Backend.Groups;

/// <summary>
/// A group as a command decides on it: its view and the stream version that view reflects. Events decided from it are
/// appended expecting that version (<see cref="AppendGroupEvents"/>), so a command that raced another on the same group
/// fails with a concurrency error (409) instead of acting on stale state.
/// </summary>
public sealed record GroupStream(GroupView Group, long Version)
{
    public Guid Id => Group.Id;

    /// <summary>The group's stream, or null when there's no such group.</summary>
    public static async Task<GroupStream?> LoadAsync(IQuerySession session, Guid groupId, CancellationToken ct)
    {
        // The version first: a view loaded after it is at least as new, and a newer one fails the append
        var state = await session.Events.FetchStreamStateAsync(groupId, ct);
        if (state is null)
            return null;
        var group = await session.LoadAsync<GroupView>(groupId, ct);
        return group is null ? null : new GroupStream(group, state.Version);
    }

    public GroupEvents Append(params IEnumerable<object> events) => new(Id, Version, [.. events]);
}

/// <summary>Events for one group's stream, decided at <paramref name="Version"/>.</summary>
public sealed record GroupEvents(Guid GroupId, long Version, IReadOnlyList<object> Events);

/// <summary>
/// The side effect of a command on groups (Wolverine applies it before committing): appends each group's events,
/// expecting the stream still to be at the version they were decided at. Streams without events are left alone.
/// </summary>
public sealed record AppendGroupEvents(IReadOnlyList<GroupEvents> Streams) : IMartenOp
{
    public AppendGroupEvents(GroupEvents stream) : this([stream])
    {
    }

    public IEnumerable<object> AllEvents => Streams.SelectMany(s => s.Events);

    public void Execute(IDocumentSession session)
    {
        foreach (var stream in Streams.Where(s => s.Events.Count > 0))
            // Marten's expected version is the stream's version after the append
            session.Events.Append(stream.GroupId, stream.Version + stream.Events.Count, [.. stream.Events]);
    }
}
