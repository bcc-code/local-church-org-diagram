namespace Backend.Admins;

/// <summary>A validated <c>admins</c> command line (<see cref="From"/>).</summary>
public abstract record AdminRequest
{
    public const int MaxNameLength = 200;
    public const int DefaultSearchLimit = 10;
    public const int MaxSearchLimit = 100;

    /// <summary>
    /// Validates <paramref name="input"/>: the person argument, the scope flags and that every flag given applies to
    /// the action, so a mistyped command fails instead of doing something else.
    /// </summary>
    /// <exception cref="AdminException"><c>invalid_input</c></exception>
    public static AdminRequest From(AdminsInput input)
    {
        var action = input.Action.Trim().ToLowerInvariant();
        var given = new Dictionary<string, bool>
        {
            ["--tenant"] = input.TenantFlag is not null,
            ["--global"] = input.GlobalFlag,
            ["--all"] = input.AllFlag,
            ["--name"] = input.NameFlag is not null,
            ["--limit"] = input.LimitFlag is not null,
            ["--dry-run"] = input.DryRunFlag,
            ["--yes"] = input.YesFlag,
            ["--allow-last-global"] = input.AllowLastGlobalFlag,
        };
        string[] allowed = action switch
        {
            "add" => ["--tenant", "--global", "--name", "--dry-run"],
            "list" => ["--tenant", "--global"],
            "remove" => ["--tenant", "--global", "--all", "--dry-run", "--yes", "--allow-last-global"],
            "search" => ["--limit"],
            _ => throw Invalid($"Unknown action '{input.Action}': use add, list, remove or search"),
        };
        var notAllowed = given.Where(f => f.Value && !allowed.Contains(f.Key)).Select(f => f.Key).ToList();
        if (notAllowed.Count > 0)
            throw Invalid($"{string.Join(", ", notAllowed)} can't be used with {action}");

        if (input.TenantFlag is <= 0)
            throw Invalid($"--tenant must be a positive BCC church ID, not {input.TenantFlag}");

        switch (action)
        {
            case "add":
                return new AddAdmin(
                    PersonUid(input, action),
                    Scope(input, "--tenant <id> or --global"),
                    input.NameFlag is null ? null : ValidName(input.NameFlag),
                    input.DryRunFlag);

            case "list":
                return new ListAdmins(
                    input.Person is null ? null : PersonUid(input, action), input.TenantFlag, input.GlobalFlag);

            case "remove":
                var uid = PersonUid(input, action);
                if (input.AllFlag && (input.TenantFlag is not null || input.GlobalFlag))
                    throw Invalid("Give one of --tenant <id>, --global or --all");
                return new RemoveAdmin(
                    uid,
                    input.AllFlag ? null : new AdminScope(Scope(input, "--tenant <id>, --global or --all")),
                    input.DryRunFlag,
                    input.YesFlag,
                    input.AllowLastGlobalFlag);

            default:
                var text = input.Person?.Trim();
                if (string.IsNullOrEmpty(text))
                    throw Invalid("search needs a name or a UID to search for");
                var limit = input.LimitFlag ?? DefaultSearchLimit;
                if (limit is < 1 or > MaxSearchLimit)
                    throw Invalid($"--limit must be between 1 and {MaxSearchLimit}");
                return new SearchPersons(text, limit);
        }
    }

    /// <summary>The trimmed name, 1 to <see cref="MaxNameLength"/> characters (the Admins table's constraints).</summary>
    public static string ValidName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            throw Invalid("The name can't be empty");
        if (trimmed.Length > MaxNameLength)
            throw Invalid($"The name can't be longer than {MaxNameLength} characters");
        return trimmed;
    }

    private static Guid PersonUid(AdminsInput input, string action)
    {
        if (input.Person is null)
            throw Invalid($"{action} needs the person's UID: run `admins search <name>` to find it");
        if (!Guid.TryParse(input.Person.Trim(), out var uid) || uid == Guid.Empty)
            throw Invalid($"'{input.Person}' isn't a person UID: run `admins search <name>` to find it");
        return uid;
    }

    /// <summary>The tenant ID of a Local Admin, null for a Global Admin.</summary>
    private static long? Scope(AdminsInput input, string expected)
    {
        if ((input.TenantFlag is null) == !input.GlobalFlag)
            throw Invalid($"Give one of {expected}");
        return input.TenantFlag;
    }

    private static AdminException Invalid(string message) => new(AdminErrors.InvalidInput, message);
}

/// <param name="TenantId">Null: Global Admin.</param>
public record AddAdmin(Guid PersonUid, long? TenantId, string? Name, bool DryRun) : AdminRequest;

/// <summary>Every admin, or only the rows matching all the filters given (tenant and global: either).</summary>
public record ListAdmins(Guid? PersonUid, long? TenantId, bool Global) : AdminRequest;

/// <param name="Scope">The row to remove; null: all of the person's rows.</param>
public record RemoveAdmin(Guid PersonUid, AdminScope? Scope, bool DryRun, bool Yes, bool AllowLastGlobal)
    : AdminRequest;

/// <summary>Searches by name, or looks one person up when <paramref name="Text"/> is a UID.</summary>
public record SearchPersons(string Text, int Limit) : AdminRequest;

/// <param name="TenantId">Null: Global Admin.</param>
public record AdminScope(long? TenantId);

/// <summary>A failure reported to the caller with a stable <see cref="Code"/> (see <see cref="AdminErrors"/>).</summary>
public class AdminException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class AdminErrors
{
    public const string InvalidInput = "invalid_input";
    public const string PersonNotFound = "person_not_found";
    public const string PersonHasNoName = "person_has_no_name";
    public const string CoreApiNotConfigured = "core_api_not_configured";
    public const string CoreApiError = "core_api_error";
    public const string MigrationsPending = "migrations_pending";
    public const string LastGlobalAdmin = "last_global_admin";
    public const string ConfirmationRequired = "confirmation_required";
}
