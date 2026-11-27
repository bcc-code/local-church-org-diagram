using Backend.Groups;
using JasperFx.CommandLine;
using Marten.Exceptions;
using Marten;
using SessionOptions = Marten.Services.SessionOptions;
using Npgsql;

namespace Backend.LegacyImport;

// One-off import of the Flask backend's tables into the event store, run from the command line (JasperFx commands,
// see RunJasperFxCommands in Program.cs). The host is built but not started: no web server, no Wolverine.
//
//   dotnet run --project backend -- import-groups [--source <connection string>] [--dry-run]
//   dotnet run --project backend -- import-memberships [--source <connection string>] [--dry-run]
//   dotnet run --project backend -- import-reset --yes

public class ImportInput : NetCoreInput
{
    [Description("Npgsql connection string of the database with the legacy tables (default: ConnectionStrings:Default)")]
    public string? SourceFlag { get; set; }

    [Description("Import and report, then roll back")]
    public bool DryRunFlag { get; set; }
}

public class ResetInput : NetCoreInput
{
    [Description("Confirms deleting all Marten events and documents")]
    public bool YesFlag { get; set; }
}

[Description("Imports public.groups: starts one stream per group with GroupImported", Name = ImportGroupsCommand.Name)]
public class ImportGroupsCommand : JasperFxAsyncCommand<ImportInput>
{
    public const string Name = "import-groups";

    public override Task<bool> Execute(ImportInput input) =>
        LegacyImporter.RunAsync(input, async (source, session, ct) =>
        {
            var rows = await LegacyTables.ReadGroupsAsync(source, ct);
            foreach (var e in LegacyMapping.ToEvents(rows))
                session.Events.StartStream(e.GroupId, e);
            return rows.Count;
        });
}

[Description(
    "Imports public.group_membership: appends MembershipImported to each group's stream (run import-groups first)",
    Name = ImportMembershipsCommand.Name)]
public class ImportMembershipsCommand : JasperFxAsyncCommand<ImportInput>
{
    public const string Name = "import-memberships";

    public override Task<bool> Execute(ImportInput input) =>
        LegacyImporter.RunAsync(input, async (source, session, ct) =>
        {
            var rows = await LegacyTables.ReadMembershipsAsync(source, ct);
            foreach (var group in LegacyMapping.ToEvents(rows).GroupBy(e => e.GroupId))
            {
                try
                {
                    await session.Events.AppendOptimistic(group.Key, ct, group.ToArray<object>());
                }
                catch (NonExistentStreamException)
                {
                    throw new LegacyImportException(
                        $"Group {group.First().LegacyGroupId} has no stream: run {ImportGroupsCommand.Name} first");
                }
            }

            return rows.Count;
        });
}

[Description("Deletes all Marten events and documents, to run the import again", Name = ImportResetCommand.Name)]
public class ImportResetCommand : JasperFxAsyncCommand<ResetInput>
{
    public const string Name = "import-reset";

    public override async Task<bool> Execute(ResetInput input)
    {
        if (!input.YesFlag)
        {
            Console.Error.WriteLine("This deletes all Marten events and documents. Run again with --yes to confirm.");
            return false;
        }

        using var host = input.BuildHost();
        var store = host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllEventDataAsync();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        Console.WriteLine("Deleted all Marten events and documents.");
        return true;
    }
}

public static class LegacyImporter
{
    /// <summary>Commands that run the import, which Program.cs lets generate code at runtime in Production.</summary>
    public static readonly IReadOnlySet<string> CommandNames =
        new HashSet<string> { ImportGroupsCommand.Name, ImportMembershipsCommand.Name, ImportResetCommand.Name };

    /// <summary>
    /// Runs <paramref name="import"/> (reads the source, appends events; returns the rows read) in one target
    /// transaction with the inline projections, reports, then commits, or rolls back for a dry run.
    /// </summary>
    public static async Task<bool> RunAsync(
        ImportInput input, Func<NpgsqlDataSource, IDocumentSession, CancellationToken, Task<int>> import)
    {
        var ct = CancellationToken.None;
        using var host = input.BuildHost();
        var store = host.Services.GetRequiredService<IDocumentStore>();
        var target = host.Services.GetRequiredService<NpgsqlDataSource>();

        // The host isn't started, so AddResourceSetupOnStartup doesn't run: create/update the Marten schema here
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

        await using var ownSource = input.SourceFlag is { } source ? NpgsqlDataSource.Create(source) : null;

        await using var connection = await target.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var session = store.LightweightSession(SessionOptions.ForTransaction(transaction));

        int rows;
        try
        {
            rows = await import(ownSource ?? target, session, ct);
        }
        catch (LegacyImportException e)
        {
            Console.Error.WriteLine($"Nothing imported: {e.Message}");
            return false;
        }

        var events = session.PendingChanges.Streams().Sum(s => s.Events.Count);
        await session.SaveChangesAsync(ct);

        Console.WriteLine($"Source rows read:  {rows}");
        Console.WriteLine($"Events appended:   {events}");
        Console.WriteLine($"GroupView:         {await session.Query<GroupView>().CountAsync(ct)}");
        Console.WriteLine($"MembershipView:    {await session.Query<MembershipView>().CountAsync(ct)}");

        if (input.DryRunFlag)
        {
            await transaction.RollbackAsync(ct);
            Console.WriteLine("Dry run: rolled back, nothing imported.");
        }
        else
        {
            await transaction.CommitAsync(ct);
            Console.WriteLine("Committed.");
        }

        return true;
    }
}
