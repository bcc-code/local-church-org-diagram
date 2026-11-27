using JasperFx.CommandLine;

namespace Backend.Admins;

// Manages the Admins table from the command line (JasperFx command, see RunJasperFxCommands in Program.cs), built for
// agents running it on someone's behalf: non-interactive, --json output, --dry-run, idempotent add/remove, exit code 0
// on success (no-ops included) and 1 on any error. The host is built but not started: no web server, no Wolverine.
//
//   dotnet run --project backend -- admins search <name or UID> [--limit <n>] [--json]
//   dotnet run --project backend -- admins list [<person UID>] [--tenant <id>] [--global] [--json]
//   dotnet run --project backend -- admins add <person UID> (--tenant <id> | --global) [--name <name>] [--dry-run] [--json]
//   dotnet run --project backend -- admins remove <person UID> (--tenant <id> | --global | --all) [--yes] [--dry-run]
//                                                 [--allow-last-global] [--json]

public class AdminsInput : NetCoreInput
{
    [Description("add, list, remove or search")]
    public string Action { get; set; } = "";

    [Description("add, remove: the person's BCC UID. list: only this person's rows. search: a name (free text) or a UID")]
    public string? Person { get; set; }

    [Description("add, remove: Local Admin of this tenant (BCC church ID). list: Local Admins of this tenant")]
    public long? TenantFlag { get; set; }

    [Description("add, remove: Global Admin (every tenant). list: Global Admins")]
    public bool GlobalFlag { get; set; }

    [Description("remove: every admin row of the person")]
    [FlagAlias("all", true)]
    public bool AllFlag { get; set; }

    [Description("add: the name stored with the row (default: the person's display name from the Core API)")]
    public string? NameFlag { get; set; }

    [Description("search: at most this many persons (default 10, at most 100)")]
    public int? LimitFlag { get; set; }

    [Description("add, remove: report what would change, change nothing")]
    public bool DryRunFlag { get; set; }

    [Description("remove: confirms the removal")]
    [FlagAlias("yes", true)]
    public bool YesFlag { get; set; }

    [Description("remove: allows removing the only Global Admin")]
    [FlagAlias("allow-last-global", true)]
    public bool AllowLastGlobalFlag { get; set; }

    [Description("Prints one JSON document on stdout")]
    public bool JsonFlag { get; set; }
}

[Description("Manages admins (add, list, remove) and searches persons in the BCC Core API", Name = Name)]
public class AdminsCommand : JasperFxAsyncCommand<AdminsInput>
{
    public const string Name = "admins";

    /// <summary>Where results go: stdout, which <see cref="ReserveStdout"/> keeps for them alone.</summary>
    private static TextWriter _results = Console.Out;

    /// <summary>
    /// Keeps stdout for the command's result and sends everything else written to <see cref="Console.Out"/> (JasperFx's
    /// command discovery, libraries) to stderr, so <c>--json</c> output parses. Call before RunJasperFxCommands.
    /// </summary>
    public static void ReserveStdout()
    {
        _results = Console.Out;
        Console.SetOut(Console.Error);
    }

    public AdminsCommand()
    {
        Usage("List all admins").Arguments(x => x.Action);
        Usage("Add or remove a person's admin rows, list them, or search persons").Arguments(x => x.Action, x => x.Person!);
    }

    public override async Task<bool> Execute(AdminsInput input)
    {
        AdminResult result;
        try
        {
            var request = AdminRequest.From(input);
            using var host = input.BuildHost();
            await using var scope = host.Services.CreateAsyncScope();
            var admins = ActivatorUtilities.CreateInstance<AdminService>(scope.ServiceProvider);
            result = await admins.RunAsync(request, CancellationToken.None);
        }
        catch (AdminException e)
        {
            result = AdminResult.Failed(e.Code, e.Message);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            result = AdminResult.Failed("unexpected", e.Message);
        }

        AdminOutput.Write(result, input.JsonFlag, _results, Console.Error);
        return result.Error is null;
    }
}
