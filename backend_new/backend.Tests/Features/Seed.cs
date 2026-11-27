using Backend.Groups;
using Backend.Tests.Database;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Tests.Features;

/// <summary>
/// Groups and memberships for database tests, as the legacy import's events. Each test seeds under a tenant of its own,
/// so tests sharing <see cref="DatabaseBackendFactory"/>'s database don't see each other's data.
/// </summary>
public static class Seed
{
    private static long _nextTenant = Random.Shared.NextInt64(1_000_000, 1_000_000_000_000);
    private static long _nextLegacyId = Random.Shared.NextInt64(1_000_000, 1_000_000_000_000);
    private static readonly DateTimeOffset Start = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static long NewTenant() => Interlocked.Increment(ref _nextTenant);

    /// <summary>A group; <paramref name="createdAfter"/> orders groups with equal sort orders.</summary>
    public static GroupImported Group(
        long tenantId, string name, GroupImported? parent = null, int? sortOrder = null, int createdAfter = 0)
    {
        var legacyId = Interlocked.Increment(ref _nextLegacyId);
        return new GroupImported(GroupIds.FromLegacyId(legacyId), legacyId, name, parent?.GroupId, parent?.LegacyId,
            tenantId, sortOrder, Start.AddMinutes(createdAfter));
    }

    public static MembershipImported Member(GroupImported group, Guid personUid, string? title = null, string? link = null) =>
        new(group.GroupId, Interlocked.Increment(ref _nextLegacyId), group.LegacyId, personUid, title, link,
            group.TenantId, Start);

    /// <summary>Starts each group's stream, then appends the memberships, as the import does.</summary>
    public static async Task StoreAsync(this DatabaseBackendFactory factory, params object[] events)
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();

        await using (var session = store.LightweightSession())
        {
            foreach (var group in events.OfType<GroupImported>())
                session.Events.StartStream(group.GroupId, group);
            await session.SaveChangesAsync();
        }

        await using (var session = store.LightweightSession())
        {
            foreach (var memberships in events.OfType<MembershipImported>().GroupBy(m => m.GroupId))
                session.Events.Append(memberships.Key, memberships.ToArray<object>());
            await session.SaveChangesAsync();
        }
    }
}
