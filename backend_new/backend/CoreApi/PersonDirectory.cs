using BccCode.Core.Api.Client;
using BccCode.Linq.Client;
using CoreApiPerson = BccCode.Core.Api.Client.Model.Person;

namespace Backend.CoreApi;

/// <summary>A person's details from the BCC Core API.</summary>
/// <param name="ProfilePicture">Image URL; null when the person has none.</param>
public record PersonSummary(Guid Uid, string DisplayName, string? ProfilePicture);

/// <summary>Person details by UID or by name, from the BCC Core API (<see cref="CoreApiPersonDirectory"/>).</summary>
public interface IPersonDirectory
{
    /// <summary>
    /// The persons with these UIDs, in no particular order. UIDs the Core API doesn't know are left out, so callers
    /// show their own placeholder for them.
    /// </summary>
    Task<IReadOnlyList<PersonSummary>> GetByUidsAsync(IEnumerable<Guid> uids, CancellationToken ct);

    /// <summary>At most <paramref name="limit"/> persons matching <paramref name="text"/> (Core API free-text search).</summary>
    Task<IReadOnlyList<PersonSummary>> SearchAsync(string text, int limit, CancellationToken ct);
}

/// <summary>Looks persons up with the app's <see cref="ICoreApiClient"/>.</summary>
public class CoreApiPersonDirectory(ICoreApiClient coreApi, CoreApiSettings settings) : IPersonDirectory
{
    /// <summary>Only what <see cref="From"/> reads.</summary>
    public const string QueryFields = "uid,displayName,profilePicture";

    /// <summary>UIDs per request: they all go in the query string, which must stay short.</summary>
    public const int BatchSize = 100;

    public async Task<IReadOnlyList<PersonSummary>> GetByUidsAsync(IEnumerable<Guid> uids, CancellationToken ct)
    {
        var distinct = uids.Distinct().ToList();
        if (distinct.Count == 0)
            return [];
        EnsureConfigured();

        var persons = new List<PersonSummary>(distinct.Count);
        foreach (var chunk in distinct.Chunk(BatchSize))
        {
            // A List, not the array: C# 14 binds array.Contains to MemoryExtensions (spans), which BccCode.Linq can't
            // translate to the _in filter
            List<Guid> batch = [.. chunk];
            var found = await coreApi.Persons
                .Query(q => q.Fields = QueryFields)
                .Where(p => batch.Contains(p.Uid))
                .ToListAsync(ct);
            persons.AddRange(found.Select(From));
        }
        return persons;
    }

    public async Task<IReadOnlyList<PersonSummary>> SearchAsync(string text, int limit, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        EnsureConfigured();

        var found = await coreApi.Persons
            .Query(q =>
            {
                q.Fields = QueryFields;
                q.Search = text;
            })
            .Take(limit)
            .ToListAsync(ct);
        return [.. found.Select(From)];
    }

    public static PersonSummary From(CoreApiPerson person) => new(
        Uid: person.Uid,
        DisplayName: person.DisplayName?.Trim() ?? "",
        ProfilePicture: string.IsNullOrWhiteSpace(person.ProfilePicture) ? null : person.ProfilePicture);

    private void EnsureConfigured()
    {
        if (!settings.IsConfigured)
            throw new CoreApiNotConfiguredException();
    }
}
