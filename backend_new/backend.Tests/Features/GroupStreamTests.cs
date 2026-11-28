using System.Net;
using System.Net.Http.Json;
using Backend.Api;
using Backend.Groups;
using Backend.Tests.Api;
using Backend.Tests.Auth;
using Backend.Tests.Database;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Http;

namespace Backend.Tests.Features;

public record StaleGroupAppend(Guid GroupId);

/// <summary>Test-only: appends to a group's stream as decided one version ago, as a command that lost a race would.</summary>
public static class StaleGroupAppendEndpoint
{
    [WolverinePost("/test/groups/stale-append")]
    public static async Task<(ApiSuccess, AppendGroupEvents)> Post(
        StaleGroupAppend command, IQuerySession session, CancellationToken ct)
    {
        var group = (await GroupStream.LoadAsync(session, command.GroupId, ct))!;
        var stale = group with { Version = group.Version - 1 };
        var changed = new GroupSortOrderChanged(group.Id, 9, Guid.NewGuid(), DateTimeOffset.UtcNow);
        return (ApiSuccess.Instance, new AppendGroupEvents(stale.Append(changed)));
    }
}

[Trait("Category", "Database")]
[Collection(DatabaseCollection.Name)]
public class GroupStreamDatabaseTests(DatabaseBackendFactory factory)
{
    [DatabaseFact]
    public async Task LoadsTheGroupAtItsVersion()
    {
        var group = Seed.Group(Seed.NewTenant(), "Styre");
        await factory.StoreAsync(group, Seed.Member(group, Guid.NewGuid()), Seed.Member(group, Guid.NewGuid()));
        await using var session = factory.Services.GetRequiredService<IDocumentStore>().QuerySession();

        var stream = (await GroupStream.LoadAsync(session, group.GroupId, CancellationToken.None))!;

        stream.Version.ShouldBe(3);
        stream.Group.MemberCount.ShouldBe(2);
        (await GroupStream.LoadAsync(session, Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
    }

    // A command decided on a version the stream has moved past changes nothing: 409, in the /api error shape
    [DatabaseFact]
    public async Task AStaleAppend_IsAConflict()
    {
        var group = Seed.Group(Seed.NewTenant(), "Styre", sortOrder: 1);
        await factory.StoreAsync(group, Seed.Member(group, Guid.NewGuid()));
        var client = factory.CreateClient(TestUser.Global());

        var response = await client.PostAsJsonAsync("/api/test/groups/stale-append", new { group_id = group.GroupId });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadJsonAsync()).ShouldBeJson(new { error = ApiConventions.ConcurrentChange });
        await using var session = factory.Services.GetRequiredService<IDocumentStore>().QuerySession();
        (await session.LoadAsync<GroupView>(group.GroupId))!.SortOrder.ShouldBe(1);
    }
}
