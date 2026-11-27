using Backend.Groups;
using Npgsql;

namespace Backend.LegacyImport;

/// <summary>A row of the Flask backend's <c>public.groups</c> table (see backend/schema.sql).</summary>
public record LegacyGroupRow(
    long Id,
    DateTimeOffset CreatedAt,
    string Name,
    long? ParentId,
    long? TenantId,
    int? SortOrder);

/// <summary>A row of the Flask backend's <c>public.group_membership</c> table (see backend/schema.sql).</summary>
public record LegacyMembershipRow(
    long Id,
    DateTimeOffset CreatedAt,
    long GroupId,
    Guid PersonUid,
    string? Title,
    string? Link,
    long? TenantId);

/// <summary>The import can't go ahead; nothing was written.</summary>
public class LegacyImportException(string message) : Exception(message);

/// <summary>Maps legacy rows to the *Imported events. Fails on rows without a tenant: events require one.</summary>
public static class LegacyMapping
{
    public static IReadOnlyList<GroupImported> ToEvents(IReadOnlyList<LegacyGroupRow> rows)
    {
        FailOnNullTenants("groups", rows.Where(r => r.TenantId is null).Select(r => r.Id));
        return rows.Select(r => new GroupImported(
                GroupIds.FromLegacyId(r.Id),
                r.Id,
                r.Name,
                r.ParentId is { } parentId ? GroupIds.FromLegacyId(parentId) : null,
                r.ParentId,
                r.TenantId!.Value,
                r.SortOrder,
                r.CreatedAt))
            .ToList();
    }

    public static IReadOnlyList<MembershipImported> ToEvents(IReadOnlyList<LegacyMembershipRow> rows)
    {
        FailOnNullTenants("group_membership", rows.Where(r => r.TenantId is null).Select(r => r.Id));
        return rows.Select(r => new MembershipImported(
                GroupIds.FromLegacyId(r.GroupId),
                r.Id,
                r.GroupId,
                r.PersonUid,
                r.Title,
                r.Link,
                r.TenantId!.Value,
                r.CreatedAt))
            .ToList();
    }

    private static void FailOnNullTenants(string table, IEnumerable<long> ids)
    {
        var missing = ids.ToList();
        if (missing.Count > 0)
            throw new LegacyImportException(
                $"{missing.Count} {table} row(s) have no tenant_id (id {string.Join(", ", missing)})");
    }
}

/// <summary>Reads the legacy tables in a read-only transaction.</summary>
public static class LegacyTables
{
    public static Task<List<LegacyGroupRow>> ReadGroupsAsync(NpgsqlDataSource source, CancellationToken ct) =>
        ReadAsync(source, "SELECT id, created_at, name, parent_id, tenant_id, sort_order FROM public.groups ORDER BY id",
            r => new LegacyGroupRow(
                r.GetInt64(0),
                r.GetFieldValue<DateTimeOffset>(1),
                r.GetString(2),
                r.IsDBNull(3) ? null : r.GetInt64(3),
                r.IsDBNull(4) ? null : r.GetInt64(4),
                r.IsDBNull(5) ? null : r.GetInt32(5)),
            ct);

    public static Task<List<LegacyMembershipRow>> ReadMembershipsAsync(NpgsqlDataSource source, CancellationToken ct) =>
        ReadAsync(source,
            "SELECT id, created_at, group_id, bcc_person_uid, title, link, tenant_id FROM public.group_membership ORDER BY id",
            r => new LegacyMembershipRow(
                r.GetInt64(0),
                r.GetFieldValue<DateTimeOffset>(1),
                r.GetInt64(2),
                r.GetGuid(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetInt64(6)),
            ct);

    private static async Task<List<T>> ReadAsync<T>(
        NpgsqlDataSource source, string sql, Func<NpgsqlDataReader, T> map, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<T>();
        while (await reader.ReadAsync(ct))
            rows.Add(map(reader));
        return rows;
    }
}
