using Backend.CoreApi;
using Backend.Data;
using Backend.Groups;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Backend.Admins;

/// <summary>Runs <see cref="AdminsCommand"/>'s requests against the Admins table and the BCC Core API.</summary>
public class AdminService(AppDbContext db, IPersonDirectory persons, Marten.IQuerySession marten)
{
    /// <exception cref="AdminException">Any expected failure, with its <see cref="AdminErrors"/> code.</exception>
    public async Task<AdminResult> RunAsync(AdminRequest request, CancellationToken ct)
    {
        // The host isn't started, so MigrateOnStartup doesn't run, and the commands never migrate a database themselves
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0)
            throw new AdminException(AdminErrors.MigrationsPending,
                $"The database lacks migrations {string.Join(", ", pending)}: start the app once (it migrates at " +
                "startup) or run `dotnet ef database update`");

        return request switch
        {
            AddAdmin add => await AddAsync(add, ct),
            ListAdmins list => await ListAsync(list, ct),
            RemoveAdmin remove => await RemoveAsync(remove, ct),
            SearchPersons search => await SearchAsync(search, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };
    }

    /// <summary>
    /// Adds the row, or leaves an existing one as it is (its name too): <c>unchanged</c>. The name comes from the Core
    /// API unless given.
    /// </summary>
    private async Task<AdminResult> AddAsync(AddAdmin request, CancellationToken ct)
    {
        if (await FindAsync(request.PersonUid, request.TenantId, ct) is { } existing)
            return Unchanged(existing, request.Name);

        var warnings = new List<AdminWarning>();
        if (request.TenantId is { } tenant && !await TenantHasGroupsAsync(tenant, ct))
            warnings.Add(new(AdminWarning.TenantHasNoGroups,
                $"No group has tenant {tenant}: check that it's the right BCC church ID"));

        var admin = new Admin
        {
            PersonUid = request.PersonUid,
            Name = request.Name ?? await LookUpNameAsync(request.PersonUid, ct),
            TenantId = request.TenantId,
        };
        if (request.DryRun)
            return new("would_add") { Admins = [AdminRow.From(admin, stored: false)], Warnings = warnings };

        db.Admins.Add(admin);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Added concurrently
            db.ChangeTracker.Clear();
            return Unchanged((await FindAsync(request.PersonUid, request.TenantId, ct))!, request.Name);
        }

        return new("added") { Admins = [AdminRow.From(admin)], Warnings = warnings };
    }

    private static AdminResult Unchanged(Admin existing, string? requestedName) => new("unchanged")
    {
        Admins = [AdminRow.From(existing)],
        Warnings = requestedName is not null && requestedName != existing.Name
            ? [new(AdminWarning.NameNotUpdated, $"Kept the stored name '{existing.Name}', not '{requestedName}'")]
            : [],
    };

    private async Task<AdminResult> ListAsync(ListAdmins request, CancellationToken ct)
    {
        var query = db.Admins.AsNoTracking();
        if (request.PersonUid is { } uid)
            query = query.Where(a => a.PersonUid == uid);
        if (request.TenantId is not null || request.Global)
        {
            var tenant = request.TenantId;
            var global = request.Global;
            query = query.Where(a => (global && a.TenantId == null) || (tenant != null && a.TenantId == tenant));
        }

        var admins = await Ordered(query).ToListAsync(ct);
        return new("ok") { Admins = [.. admins.Select(a => AdminRow.From(a))] };
    }

    /// <summary>
    /// Removes the person's row (or all of them) with <see cref="RemoveAdmin.Yes"/>; without it (and without
    /// <see cref="RemoveAdmin.DryRun"/>) fails with <c>confirmation_required</c>, listing what would be removed.
    /// </summary>
    private async Task<AdminResult> RemoveAsync(RemoveAdmin request, CancellationToken ct)
    {
        var query = db.Admins.AsNoTracking().Where(a => a.PersonUid == request.PersonUid);
        if (request.Scope is { TenantId: var tenant })
            query = tenant is null ? query.Where(a => a.TenantId == null) : query.Where(a => a.TenantId == tenant);
        var admins = await Ordered(query).ToListAsync(ct);
        if (admins.Count == 0)
            return new("not_found") { Admins = [] };

        var rows = admins.Select(a => AdminRow.From(a)).ToList();
        if (!request.AllowLastGlobal && admins.Any(a => a.TenantId == null) &&
            await db.Admins.CountAsync(a => a.TenantId == null, ct) == 1)
            throw new AdminException(AdminErrors.LastGlobalAdmin,
                $"{admins[0].Name} is the only Global Admin: add another one first, or pass --allow-last-global");

        if (request.DryRun)
            return new("would_remove") { Admins = rows };
        if (!request.Yes)
            return new("error")
            {
                Admins = rows,
                Error = new(AdminErrors.ConfirmationRequired,
                    $"Would remove {rows.Count} admin row(s): run again with --yes to confirm"),
            };

        var ids = admins.Select(a => a.Id).ToList();
        await db.Admins.Where(a => ids.Contains(a.Id)).ExecuteDeleteAsync(ct);
        return new("removed") { Admins = rows };
    }

    private async Task<AdminResult> SearchAsync(SearchPersons request, CancellationToken ct)
    {
        var found = await CallCoreApiAsync(hint: null, () => Guid.TryParse(request.Text, out var uid)
            ? persons.GetByUidsAsync([uid], ct)
            : persons.SearchAsync(request.Text, request.Limit, ct));

        var uids = found.Select(p => p.Uid).ToList();
        var admins = await Ordered(db.Admins.AsNoTracking().Where(a => uids.Contains(a.PersonUid))).ToListAsync(ct);
        return new("ok")
        {
            Persons =
            [
                .. found.Select(p => new PersonRow(p.Uid, p.DisplayName,
                    [.. admins.Where(a => a.PersonUid == p.Uid).Select(a => AdminRow.From(a))])),
            ],
        };
    }

    private async Task<string> LookUpNameAsync(Guid uid, CancellationToken ct)
    {
        var found = await CallCoreApiAsync(
            "pass --name to add the admin without looking the person up", () => persons.GetByUidsAsync([uid], ct));
        var person = found.SingleOrDefault(p => p.Uid == uid)
                     ?? throw new AdminException(AdminErrors.PersonNotFound,
                         $"The Core API has no person with UID {uid}: run `admins search <name>` to find the UID");
        if (string.IsNullOrWhiteSpace(person.DisplayName))
            throw new AdminException(AdminErrors.PersonHasNoName,
                $"The Core API has no display name for {uid}: pass --name");
        return AdminRequest.ValidName(person.DisplayName);
    }

    /// <param name="hint">How to do without the Core API, if there's a way.</param>
    private static async Task<T> CallCoreApiAsync<T>(string? hint, Func<Task<T>> call)
    {
        var orHint = hint is null ? "" : $", or {hint}";
        try
        {
            return await call();
        }
        catch (CoreApiNotConfiguredException)
        {
            throw new AdminException(AdminErrors.CoreApiNotConfigured,
                $"Core API credentials aren't set: set CoreApi:ClientId and CoreApi:ClientSecret{orHint}");
        }
        catch (Exception e) when (e is not (OperationCanceledException or AdminException))
        {
            throw new AdminException(AdminErrors.CoreApiError, $"The Core API call failed ({e.Message}){orHint}");
        }
    }

    private Task<bool> TenantHasGroupsAsync(long tenant, CancellationToken ct) =>
        Marten.QueryableExtensions.AnyAsync(marten.Query<GroupView>(), g => g.TenantId == tenant, ct);

    private Task<Admin?> FindAsync(Guid uid, long? tenant, CancellationToken ct)
    {
        var query = db.Admins.AsNoTracking().Where(a => a.PersonUid == uid);
        query = tenant is null ? query.Where(a => a.TenantId == null) : query.Where(a => a.TenantId == tenant);
        return query.SingleOrDefaultAsync(ct);
    }

    /// <summary>Global Admins first, then by tenant and name.</summary>
    private static IQueryable<Admin> Ordered(IQueryable<Admin> query) => query
        .OrderBy(a => a.TenantId == null ? 0 : 1)
        .ThenBy(a => a.TenantId)
        .ThenBy(a => a.Name)
        .ThenBy(a => a.Id);
}
