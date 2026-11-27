using Marten;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using Wolverine.Tracking;

namespace Backend.Tests.Database;

[Trait("Category", "Database")]
public class PersistenceTests(DatabaseBackendFactory factory) : IClassFixture<DatabaseBackendFactory>
{
    [DatabaseFact]
    public async Task Startup_CreatesMartenWolverineAndEfCoreSchemaObjects()
    {
        _ = factory.Services; // starts the host

        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();

        // Marten's event tables only appear once an event type is in use
        (await TableExistsAsync(connection, "marten", "mt_doc_envelope")).ShouldBeTrue();
        (await TableExistsAsync(connection, "wolverine", "wolverine_incoming_envelopes")).ShouldBeTrue();
        (await TableExistsAsync(connection, "public", "__EFMigrationsHistory")).ShouldBeTrue();
    }

    [DatabaseFact]
    public async Task DurableLocalMessage_IsHandledInAMartenTransaction()
    {
        var id = Guid.NewGuid();

        await factory.Services.SendMessageAndWaitAsync(new RecordProbe(id));

        await using var session = factory.Services.GetRequiredService<IDocumentStore>().QuerySession();
        (await session.LoadAsync<Probe>(id)).ShouldNotBeNull();
        var recorded = (await session.Events.FetchStreamAsync(id)).ShouldHaveSingleItem();
        recorded.Data.ShouldBeOfType<ProbeRecorded>();
        recorded.CorrelationId.ShouldNotBeNullOrEmpty();
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string schema, string table)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = $1 AND table_name = $2)",
            connection);
        command.Parameters.Add(new NpgsqlParameter { Value = schema });
        command.Parameters.Add(new NpgsqlParameter { Value = table });
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}

public record Probe(Guid Id);

public record RecordProbe(Guid Id);

public record ProbeRecorded(Guid Id);

/// <summary>Stores a <see cref="Probe"/> and starts its event stream; AutoApplyTransactions commits both together.</summary>
public static class RecordProbeHandler
{
    public static void Handle(RecordProbe message, IDocumentSession session)
    {
        session.Store(new Probe(message.Id));
        session.Events.StartStream(message.Id, new ProbeRecorded(message.Id));
    }
}
