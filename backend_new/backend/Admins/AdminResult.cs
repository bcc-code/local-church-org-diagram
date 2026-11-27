using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Data;

namespace Backend.Admins;

/// <summary>
/// What an <c>admins</c> command did, printed as one JSON document with <c>--json</c> (<see cref="AdminOutput"/>).
/// <see cref="Status"/> is <c>ok</c> (list, search), <c>added</c>/<c>unchanged</c>/<c>would_add</c> (add),
/// <c>removed</c>/<c>not_found</c>/<c>would_remove</c> (remove) or <c>error</c>, with <see cref="Error"/> set.
/// </summary>
public record AdminResult(string Status)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AdminRow>? Admins { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<PersonRow>? Persons { get; init; }

    public IReadOnlyList<AdminWarning> Warnings { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AdminError? Error { get; init; }

    public static AdminResult Failed(string code, string message) => new("error") { Error = new(code, message) };
}

/// <param name="Id">Null for a row a dry run would add.</param>
/// <param name="Scope"><c>global</c> or <c>local</c>.</param>
/// <param name="TenantId">The tenant (BCC church ID) of a Local Admin; null for a Global Admin.</param>
/// <param name="CreatedAt">Null for a row a dry run would add.</param>
public record AdminRow(int? Id, Guid PersonUid, string Name, string Scope, long? TenantId, DateTimeOffset? CreatedAt)
{
    public const string Global = "global";
    public const string Local = "local";

    public static AdminRow From(Admin admin, bool stored = true) => new(
        stored ? admin.Id : null,
        admin.PersonUid,
        admin.Name,
        admin.TenantId is null ? Global : Local,
        admin.TenantId,
        stored ? admin.CreatedAt : null);

    public string Describe() =>
        $"{(TenantId is { } tenant ? $"Local Admin of tenant {tenant}" : "Global Admin")}: {Name} ({PersonUid})";
}

/// <summary>A person found in the Core API, with their admin rows.</summary>
public record PersonRow(Guid Uid, string DisplayName, IReadOnlyList<AdminRow> Admins);

public record AdminWarning(string Code, string Message)
{
    public const string TenantHasNoGroups = "tenant_has_no_groups";
    public const string NameNotUpdated = "name_not_updated";
}

public record AdminError(string Code, string Message);

/// <summary>Prints an <see cref="AdminResult"/>: JSON, or lines and tables for people.</summary>
public static class AdminOutput
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // Printed, not embedded in HTML: keep ' and ` readable
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// With <paramref name="json"/>, the result as JSON on <paramref name="stdout"/>; otherwise lines for people.
    /// Warnings and errors always go to <paramref name="stderr"/> as well.
    /// </summary>
    public static void Write(AdminResult result, bool json, TextWriter stdout, TextWriter stderr)
    {
        if (json)
            stdout.WriteLine(JsonSerializer.Serialize(result, Json));
        else
            WriteText(result, stdout);

        foreach (var warning in result.Warnings)
            stderr.WriteLine($"Warning ({warning.Code}): {warning.Message}");
        if (result.Error is { } error)
            stderr.WriteLine($"Error ({error.Code}): {error.Message}");
    }

    private static void WriteText(AdminResult result, TextWriter stdout)
    {
        var admins = result.Admins ?? [];
        switch (result.Status)
        {
            case "added":
            case "unchanged":
            case "would_add":
            case "removed":
            case "would_remove":
                var verb = result.Status switch
                {
                    "added" => "Added",
                    "unchanged" => "Already there, unchanged",
                    "would_add" => "Dry run, would add",
                    "removed" => "Removed",
                    _ => "Dry run, would remove",
                };
                foreach (var admin in admins)
                    stdout.WriteLine($"{verb}: {admin.Describe()}");
                break;
            case "not_found":
                stdout.WriteLine("No matching admin rows, nothing removed.");
                break;
            case "error":
                // confirmation_required lists what would be removed
                foreach (var admin in admins)
                    stdout.WriteLine($"Would remove: {admin.Describe()}");
                break;
            default:
                if (result.Persons is { } persons)
                    WritePersons(persons, stdout);
                else
                    WriteAdmins(admins, stdout);
                break;
        }
    }

    private static void WriteAdmins(IReadOnlyList<AdminRow> admins, TextWriter stdout)
    {
        if (admins.Count == 0)
        {
            stdout.WriteLine("No admins.");
            return;
        }

        WriteTable(stdout, ["ID", "SCOPE", "TENANT", "PERSON UID", "NAME", "CREATED"], admins.Select(a => new[]
        {
            a.Id?.ToString() ?? "", a.Scope, a.TenantId?.ToString() ?? "", a.PersonUid.ToString(), a.Name,
            a.CreatedAt?.ToString("yyyy-MM-dd") ?? "",
        }));
    }

    private static void WritePersons(IReadOnlyList<PersonRow> persons, TextWriter stdout)
    {
        if (persons.Count == 0)
        {
            stdout.WriteLine("No persons found.");
            return;
        }

        WriteTable(stdout, ["PERSON UID", "NAME", "ADMIN"], persons.Select(p => new[]
        {
            p.Uid.ToString(), p.DisplayName,
            string.Join(", ", p.Admins.Select(a => a.TenantId is { } tenant ? $"tenant {tenant}" : "global")),
        }));
    }

    private static void WriteTable(TextWriter stdout, string[] header, IEnumerable<string[]> rows)
    {
        List<string[]> lines = [header, .. rows];
        var widths = header.Select((_, i) => lines.Max(l => l[i].Length)).ToArray();
        foreach (var line in lines)
            stdout.WriteLine(string.Join("  ", line.Select((cell, i) => cell.PadRight(widths[i]))).TrimEnd());
    }
}
