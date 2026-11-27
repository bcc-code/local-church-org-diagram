using System.Net;
using Backend.Tests.Auth;
using Backend.Tests.CoreApi;
using Backend.Tests.Database;
using Backend.Tests.Features;
using Shouldly;

namespace Backend.Tests.Api;

/// <summary>
/// The exact JSON of each /api read endpoint, as the Flask backend (backend/api.py) answers it and the frontend
/// (frontend/src/types) reads it: property names, nesting and JSON types. Group IDs are GUID strings where Flask had
/// integers; the frontend only compares them.
/// </summary>
[Trait("Category", "Database")]
[Collection(DatabaseCollection.Name)]
public class FlaskContractTests(DatabaseBackendFactory factory)
{
    [DatabaseFact]
    public async Task Tree()
    {
        var tenant = Seed.NewTenant();
        var root = Seed.Group(tenant, "Root", sortOrder: 0);
        var child = Seed.Group(tenant, "Child", root);
        await factory.StoreAsync(root, child, Seed.Member(child, Guid.NewGuid()));

        var response = await factory.CreateClient(TestUser.Member(tenant)).GetAsync("/api/tree");

        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        (await response.ReadJsonAsync()).ShouldBeJson(new object[]
        {
            new { group_id = root.GroupId, label = "Root", parent_group_id = (Guid?)null, member_count = 0, sort_order = 0 },
            new { group_id = child.GroupId, label = "Child", parent_group_id = root.GroupId, member_count = 1, sort_order = (int?)null },
        });
    }

    [DatabaseFact]
    public async Task GroupMembers()
    {
        var tenant = Seed.NewTenant();
        var group = Seed.Group(tenant, "Styre");
        var known = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        await factory.StoreAsync(group, Seed.Member(group, known, "Leder", "https://x"), Seed.Member(group, unknown));
        factory.CoreApi.Persons = _ => [CoreApiStub.Person(known, "Kari", "https://pic")];

        var response = await factory.CreateClient(TestUser.Member(tenant)).GetAsync($"/api/persons?group_id={group.GroupId}");

        (await response.ReadJsonAsync()).ShouldBeJson(new object[]
        {
            new { person_uid = known, name = "Kari", profile_picture = "https://pic", title = "Leder", link = "https://x" },
            new { person_uid = unknown, name = "?", profile_picture = (string?)null, title = (string?)null, link = (string?)null },
        });
    }

    [DatabaseFact]
    public async Task PersonGroups()
    {
        var tenant = Seed.NewTenant();
        var person = Guid.NewGuid();
        var group = Seed.Group(tenant, "Styre");
        await factory.StoreAsync(group, Seed.Member(group, person));

        var response = await factory.CreateClient(TestUser.Member(tenant)).GetAsync($"/api/persons/{person}/groups");

        (await response.ReadJsonAsync()).ShouldBeJson(new
        {
            groups = new[] { new { group_id = group.GroupId, name = "Styre", member_count = 1 } },
        });
    }

    [DatabaseFact]
    public async Task PersonGroups_WithoutMemberships() =>
        (await (await factory.CreateClient(TestUser.Member(Seed.NewTenant()))
                .GetAsync($"/api/persons/{Guid.NewGuid()}/groups")).ReadJsonAsync())
        .ShouldBeJson(new { groups = Array.Empty<object>() });

    [DatabaseFact]
    public async Task TitlesAndTitleHolders()
    {
        var tenant = Seed.NewTenant();
        var person = Guid.NewGuid();
        var group = Seed.Group(tenant, "Styre");
        await factory.StoreAsync(group, Seed.Member(group, person, "Leder"));
        factory.CoreApi.Persons = _ => [CoreApiStub.Person(person, "Kari")];
        var client = factory.CreateClient(TestUser.Member(tenant));

        (await (await client.GetAsync("/api/titles")).ReadJsonAsync()).ShouldBeJson(new[] { "Leder" });
        (await (await client.GetAsync("/api/titles/Leder/persons")).ReadJsonAsync()).ShouldBeJson(new[]
        {
            new { person_uid = person, name = "Kari", profile_picture = (string?)null },
        });
    }

    [DatabaseFact]
    public async Task PersonSearch()
    {
        var person = Guid.NewGuid();
        factory.CoreApi.Persons = _ => [CoreApiStub.Person(person, "Hans Hansen", "https://pic")];

        var response = await factory.CreateClient(TestUser.Member(Seed.NewTenant())).GetAsync("/api/persons/search?q=hans");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).ShouldBeJson(new[]
        {
            new { person_uid = person, name = "Hans Hansen", profile_picture = "https://pic" },
        });
    }
}
