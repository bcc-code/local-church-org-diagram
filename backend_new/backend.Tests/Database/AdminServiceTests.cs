using Backend.Admins;
using Backend.CoreApi;
using Backend.Data;
using Backend.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Backend.Tests.Database;

/// <summary>
/// <see cref="AdminService"/> against a real Admins table, with <see cref="FakePersonDirectory"/> for the Core API.
/// The database is shared by the collection, so each test uses its own person UIDs and tenant IDs.
/// </summary>
[Trait("Category", "Database")]
[Collection(DatabaseCollection.Name)]
public class AdminServiceTests(DatabaseBackendFactory factory)
{
    private readonly FakePersonDirectory _persons = new();

    private static long NewTenant() => Random.Shared.NextInt64(1_000_000, long.MaxValue);

    private async Task<AdminResult> RunAsync(AdminRequest request)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = ActivatorUtilities.CreateInstance<AdminService>(scope.ServiceProvider, _persons);
        return await service.RunAsync(request, CancellationToken.None);
    }

    private async Task<AdminException> FailsAsync(AdminRequest request, string code)
    {
        var e = await Should.ThrowAsync<AdminException>(() => RunAsync(request));
        e.Code.ShouldBe(code);
        return e;
    }

    private async Task<List<Admin>> RowsAsync(Guid person)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Admins
            .Where(a => a.PersonUid == person).OrderBy(a => a.Id).ToListAsync();
    }

    private async Task<Guid> AddAsync(long? tenant, string name = "Kari Nordmann")
    {
        var person = Guid.NewGuid();
        (await RunAsync(new AddAdmin(person, tenant, name, DryRun: false))).Status.ShouldBe("added");
        return person;
    }

    private async Task StoreGroupAsync(long tenant)
    {
        await using var session = factory.Services.GetRequiredService<Marten.IDocumentStore>().LightweightSession();
        session.Store(new GroupView(Guid.NewGuid(), "Group", null, tenant, null, DateTimeOffset.UtcNow));
        await session.SaveChangesAsync();
    }

    [DatabaseFact]
    public async Task Add_LooksTheNameUp()
    {
        var person = Guid.NewGuid();
        var tenant = NewTenant();
        await StoreGroupAsync(tenant);
        _persons.Add(person, " Kari Nordmann ");

        var result = await RunAsync(new AddAdmin(person, tenant, null, DryRun: false));

        result.Status.ShouldBe("added");
        result.Warnings.ShouldBeEmpty();
        var row = result.Admins.ShouldHaveSingleItem();
        var stored = (await RowsAsync(person)).ShouldHaveSingleItem();
        row.ShouldBe(new AdminRow(stored.Id, person, "Kari Nordmann", AdminRow.Local, tenant, stored.CreatedAt));
        stored.CreatedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [DatabaseFact]
    public async Task Add_WithName_DoesntCallTheCoreApi()
    {
        _persons.NotConfigured = true;
        var person = Guid.NewGuid();

        var result = await RunAsync(new AddAdmin(person, null, "Ola Nordmann", DryRun: false));

        result.Status.ShouldBe("added");
        (await RowsAsync(person)).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            a => a.Name.ShouldBe("Ola Nordmann"),
            a => a.TenantId.ShouldBeNull());
    }

    [DatabaseFact]
    public async Task Add_Existing_IsUnchanged_AndKeepsTheStoredName()
    {
        var tenant = NewTenant();
        var person = await AddAsync(tenant, "Kari Nordmann");
        var stored = (await RowsAsync(person)).Single();

        var same = await RunAsync(new AddAdmin(person, tenant, null, DryRun: false));
        var renamed = await RunAsync(new AddAdmin(person, tenant, "Kari N", DryRun: false));

        same.Status.ShouldBe("unchanged");
        same.Warnings.ShouldBeEmpty();
        same.Admins.ShouldHaveSingleItem().Id.ShouldBe(stored.Id);
        renamed.Status.ShouldBe("unchanged");
        renamed.Warnings.ShouldHaveSingleItem().Code.ShouldBe(AdminWarning.NameNotUpdated);
        (await RowsAsync(person)).ShouldHaveSingleItem().Name.ShouldBe("Kari Nordmann");
        _persons.Calls.ShouldBe(0);
    }

    [DatabaseFact]
    public async Task Add_GlobalAndLocalRows_Coexist()
    {
        var person = Guid.NewGuid();
        var tenant = NewTenant();

        (await RunAsync(new AddAdmin(person, null, "Kari", DryRun: false))).Status.ShouldBe("added");
        (await RunAsync(new AddAdmin(person, tenant, "Kari", DryRun: false))).Status.ShouldBe("added");

        (await RowsAsync(person)).Select(a => a.TenantId).ShouldBe([null, tenant]);
    }

    [DatabaseFact]
    public async Task Add_DryRun_WritesNothing()
    {
        var person = Guid.NewGuid();
        _persons.Add(person, "Kari Nordmann");

        var result = await RunAsync(new AddAdmin(person, null, null, DryRun: true));

        result.Status.ShouldBe("would_add");
        result.Admins.ShouldHaveSingleItem()
            .ShouldBe(new AdminRow(null, person, "Kari Nordmann", AdminRow.Global, null, null));
        (await RowsAsync(person)).ShouldBeEmpty();
    }

    [DatabaseFact]
    public async Task Add_TenantWithoutGroups_Warns()
    {
        var tenant = NewTenant();

        var result = await RunAsync(new AddAdmin(Guid.NewGuid(), tenant, "Kari", DryRun: false));

        result.Status.ShouldBe("added");
        result.Warnings.ShouldHaveSingleItem().Code.ShouldBe(AdminWarning.TenantHasNoGroups);
    }

    [DatabaseFact]
    public async Task Add_UnknownPerson_Fails()
    {
        var person = Guid.NewGuid();

        await FailsAsync(new AddAdmin(person, null, null, DryRun: false), AdminErrors.PersonNotFound);
        (await RowsAsync(person)).ShouldBeEmpty();
    }

    [DatabaseFact]
    public async Task Add_PersonWithoutName_Fails()
    {
        var person = Guid.NewGuid();
        _persons.Add(person, "");

        await FailsAsync(new AddAdmin(person, null, null, DryRun: false), AdminErrors.PersonHasNoName);
    }

    [DatabaseFact]
    public async Task Add_WithoutCoreApi_FailsAndSuggestsName()
    {
        _persons.NotConfigured = true;

        var e = await FailsAsync(new AddAdmin(Guid.NewGuid(), null, null, DryRun: false), AdminErrors.CoreApiNotConfigured);
        e.Message.ShouldContain("--name");
    }

    [DatabaseFact]
    public async Task Add_CoreApiFailure_Fails()
    {
        _persons.Failure = new HttpRequestException("503");

        var e = await FailsAsync(new AddAdmin(Guid.NewGuid(), null, null, DryRun: false), AdminErrors.CoreApiError);
        e.Message.ShouldContain("503");
    }

    [DatabaseFact]
    public async Task List_FiltersByPersonTenantAndGlobal()
    {
        var tenant = NewTenant();
        var local = await AddAsync(tenant, "B");
        var otherTenant = NewTenant();
        var other = await AddAsync(otherTenant, "C");
        var person = Guid.NewGuid();
        await RunAsync(new AddAdmin(person, null, "A", DryRun: false));
        await RunAsync(new AddAdmin(person, tenant, "A", DryRun: false));

        async Task<List<(Guid, long?)>> List(Guid? uid, long? tenantId, bool global) =>
            [.. (await RunAsync(new ListAdmins(uid, tenantId, global))).Admins!.Select(a => (a.PersonUid, a.TenantId))];

        (await List(person, null, false)).ShouldBe([(person, null), (person, tenant)]);
        (await List(null, tenant, false)).ShouldBe([(person, tenant), (local, tenant)]); // by name
        (await List(person, null, true)).ShouldBe([(person, null)]);
        (await List(person, tenant, true)).ShouldBe([(person, null), (person, tenant)]);

        var all = await List(null, null, false);
        all.ShouldContain((other, otherTenant));
        all.SkipWhile(a => a.Item2 is null).ShouldAllBe(a => a.Item2 != null); // Global Admins first
    }

    [DatabaseFact]
    public async Task Remove_WithoutYes_RequiresConfirmation()
    {
        var tenant = NewTenant();
        var person = await AddAsync(tenant);

        var result = await RunAsync(new RemoveAdmin(person, new AdminScope(tenant), DryRun: false, Yes: false, false));

        result.Status.ShouldBe("error");
        result.Error!.Code.ShouldBe(AdminErrors.ConfirmationRequired);
        result.Admins.ShouldHaveSingleItem().TenantId.ShouldBe(tenant);
        (await RowsAsync(person)).ShouldHaveSingleItem();
    }

    [DatabaseFact]
    public async Task Remove_DryRun_RemovesNothing()
    {
        var tenant = NewTenant();
        var person = await AddAsync(tenant);

        var result = await RunAsync(new RemoveAdmin(person, new AdminScope(tenant), DryRun: true, Yes: false, false));

        result.Status.ShouldBe("would_remove");
        result.Admins.ShouldHaveSingleItem();
        (await RowsAsync(person)).ShouldHaveSingleItem();
    }

    [DatabaseFact]
    public async Task Remove_OnlyTheGivenScope_ThenNotFound()
    {
        var tenant = NewTenant();
        var person = await AddAsync(tenant);
        await RunAsync(new AddAdmin(person, NewTenant(), "Kari Nordmann", DryRun: false));
        var request = new RemoveAdmin(person, new AdminScope(tenant), DryRun: false, Yes: true, false);

        var removed = await RunAsync(request);
        var again = await RunAsync(request);

        removed.Status.ShouldBe("removed");
        removed.Admins.ShouldHaveSingleItem().TenantId.ShouldBe(tenant);
        (await RowsAsync(person)).ShouldHaveSingleItem().TenantId.ShouldNotBe(tenant);
        again.Status.ShouldBe("not_found");
        again.Admins.ShouldBeEmpty();
        again.Error.ShouldBeNull();
    }

    [DatabaseFact]
    public async Task Remove_All()
    {
        await AddAsync(null, "Another Global Admin");
        var person = await AddAsync(null);
        await RunAsync(new AddAdmin(person, NewTenant(), "Kari Nordmann", DryRun: false));

        var result = await RunAsync(new RemoveAdmin(person, null, DryRun: false, Yes: true, false));

        result.Status.ShouldBe("removed");
        result.Admins!.Count.ShouldBe(2);
        (await RowsAsync(person)).ShouldBeEmpty();
    }

    [DatabaseFact]
    public async Task Remove_TheOnlyGlobalAdmin_NeedsAllowLastGlobal()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Admins
                .Where(a => a.TenantId == null).ExecuteDeleteAsync();
        var person = await AddAsync(null);

        await FailsAsync(new RemoveAdmin(person, new AdminScope(null), false, Yes: true, AllowLastGlobal: false),
            AdminErrors.LastGlobalAdmin);
        await FailsAsync(new RemoveAdmin(person, null, DryRun: true, false, AllowLastGlobal: false),
            AdminErrors.LastGlobalAdmin);
        (await RowsAsync(person)).ShouldHaveSingleItem();

        var removed = await RunAsync(new RemoveAdmin(person, new AdminScope(null), false, Yes: true, AllowLastGlobal: true));

        removed.Status.ShouldBe("removed");
        (await RowsAsync(person)).ShouldBeEmpty();
    }

    [DatabaseFact]
    public async Task Search_ListsEachPersonsAdminRows()
    {
        var tenant = NewTenant();
        var admin = await AddAsync(tenant, "Kari Nordmann");
        var other = Guid.NewGuid();
        _persons.Add(admin, "Kari Nordmann");
        _persons.Add(other, "Kari Hansen");

        var result = await RunAsync(new SearchPersons("Kari", 10));

        result.Status.ShouldBe("ok");
        result.Persons!.Select(p => (p.Uid, p.DisplayName, string.Join(",", p.Admins.Select(a => a.TenantId)))).ShouldBe([
            (admin, "Kari Nordmann", tenant.ToString()),
            (other, "Kari Hansen", ""),
        ]);
        _persons.Searches.ShouldBe([("Kari", 10)]);
    }

    [DatabaseFact]
    public async Task Search_ByUid_LooksThePersonUp()
    {
        var person = Guid.NewGuid();
        _persons.Add(person, "Kari Nordmann");

        var result = await RunAsync(new SearchPersons(person.ToString(), 10));

        result.Persons.ShouldHaveSingleItem().Uid.ShouldBe(person);
        _persons.Searches.ShouldBeEmpty();
    }

    [DatabaseFact]
    public async Task Search_WithoutCoreApi_Fails()
    {
        _persons.NotConfigured = true;

        await FailsAsync(new SearchPersons("Kari", 10), AdminErrors.CoreApiNotConfigured);
    }
}

/// <summary>An in-memory <see cref="IPersonDirectory"/>: search matches display names containing the text.</summary>
public class FakePersonDirectory : IPersonDirectory
{
    private readonly List<PersonSummary> _persons = [];

    public bool NotConfigured { get; set; }

    public Exception? Failure { get; set; }

    public int Calls { get; private set; }

    public List<(string Text, int Limit)> Searches { get; } = [];

    public void Add(Guid uid, string displayName) => _persons.Add(new PersonSummary(uid, displayName.Trim(), null));

    public Task<IReadOnlyList<PersonSummary>> GetByUidsAsync(IEnumerable<Guid> uids, CancellationToken ct)
    {
        Call();
        var wanted = uids.ToHashSet();
        return Task.FromResult<IReadOnlyList<PersonSummary>>([.. _persons.Where(p => wanted.Contains(p.Uid))]);
    }

    public Task<IReadOnlyList<PersonSummary>> SearchAsync(string text, int limit, CancellationToken ct)
    {
        Call();
        Searches.Add((text, limit));
        return Task.FromResult<IReadOnlyList<PersonSummary>>(
            [.. _persons.Where(p => p.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase)).Take(limit)]);
    }

    private void Call()
    {
        Calls++;
        if (NotConfigured)
            throw new CoreApiNotConfiguredException();
        if (Failure is not null)
            throw Failure;
    }
}
