using System.Diagnostics;
using System.Reflection;
using Backend.CoreApi;
using Backend.Data;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.Resources;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.FluentValidation;
using Wolverine.Http;
using Wolverine.Http.FluentValidation;
using Wolverine.Http.Marten;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Build-time OpenAPI generation (`dotnet build`) runs this file and starts the host (without a web server).
// Nothing may touch Postgres then, so Wolverine persistence, schema setup and migrations are skipped below.
var isGeneratingOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

// Marten and Npgsql need a syntactically valid connection string to build, but never connect
if (isGeneratingOpenApi)
    builder.Configuration["ConnectionStrings:Default"] ??= "Host=localhost";

builder.Services.AddOpenApi();

// One NpgsqlDataSource (connection string "Default") shared by Marten and EF Core, with a "/health" check and tracing.
// Production connects through Supabase's session pooler (port 5432), not the transaction pooler (6543): Marten and
// Wolverine rely on session state (advisory locks). Commands are only traced inside another span (a request, a
// Wolverine message): Wolverine's durability agent polls the database every second.
builder.AddNpgsqlDataSource("Default", configureDataSourceBuilder: dataSource => dataSource.ConfigureTracing(tracing =>
    tracing
        .ConfigureCommandFilter(_ => Activity.Current is not null)
        .ConfigureBatchFilter(_ => Activity.Current is not null)));

// Marten and EF Core share one Postgres database in separate schemas: EF Core owns "public" (and its migrations),
// Marten owns "marten" and Wolverine's inbox/outbox tables live in "wolverine".
builder.Services.AddMarten(options =>
    {
        options.DatabaseSchemaName = "marten";

        // Production never creates schema objects on first use (CritterStackDefaults below), only for the types known
        // when AddResourceSetupOnStartup runs: register every document type, projection and snapshot here.

        // An aggregate fetched for writing (FetchForWriting, Wolverine's aggregate handler workflow) is kept in the
        // session, so its inline snapshot is updated without loading it again. Never mutate the fetched aggregate
        // yourself: only append events.
        options.Projections.UseIdentityMapForAggregates = true;

        // Stored with each event: the correlation and causation IDs and headers Wolverine propagates, so the
        // request or message that produced an event can be traced from the event store
        options.Events.MetadataConfig.CorrelationIdEnabled = true;
        options.Events.MetadataConfig.CausationIdEnabled = true;
        options.Events.MetadataConfig.HeadersEnabled = true;
    })
    .UseNpgsqlDataSource()
    .UseLightweightSessions()
    .IntegrateWithWolverine(integration =>
    {
        integration.MessageStorageSchemaName = "wolverine";
        // Committed Marten events are published to Wolverine through the outbox, so event handlers (reactions) run
        // in the background after the transaction commits
        integration.UseFastEventForwarding = true;
    });

// EF Core with Wolverine's transactional middleware and outbox: Wolverine's envelope tables are mapped into the model
// (excluded from migrations), so DbContext changes and outgoing messages commit in the same transaction.
builder.Services.AddDbContextWithWolverineIntegration<AppDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlDataSource>()));

builder.UseWolverine(options =>
{
    // Handlers that use a DbContext get EF Core transactions, handlers that use IDocumentSession get Marten ones
    options.Policies.AutoApplyTransactions();
    options.Policies.UseDurableLocalQueues();

    // Each handler of a message runs on its own, so one's Validate/Stop or failure doesn't hold back the others
    options.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;

    // Registers the application's FluentValidation validators and validates message handler inputs
    options.UseFluentValidation();

    // IAuthorizationService's implementation is internal, so generated endpoint code can't construct it
    options.CodeGeneration.AlwaysUseServiceLocationFor<IAuthorizationService>();

    // Production runs a single instance, so Wolverine needs no leader election or node agents: Solo mode owns every
    // stored message and recovers the inbox/outbox right away at startup. Two processes on one database in Solo mode
    // would both handle the same messages, so elsewhere (e.g. several local worktrees sharing the dev database) the
    // default Balanced mode stays.
    if (isGeneratingOpenApi)
        options.Durability.Mode = DurabilityMode.MediatorOnly;
    else if (builder.Environment.IsProduction())
        options.Durability.Mode = DurabilityMode.Solo;
});

// Production loads the Marten and Wolverine code that `codegen write` generated before publishing instead of compiling
// it on first use, and never checks or changes the database schema on first use of a type: AddResourceSetupOnStartup
// applies it once at startup. Everywhere else (Development, Testing, build-time OpenAPI and `codegen write` itself,
// which run as Production) code is generated at runtime and schema objects are created/updated on demand.
var isWritingCode = args.FirstOrDefault() == "codegen";
var usesPreGeneratedCode = builder.Environment.IsProduction() && !isGeneratingOpenApi && !isWritingCode;
builder.Services.CritterStackDefaults(defaults =>
{
    defaults.Production.GeneratedCodeMode = usesPreGeneratedCode ? TypeLoadMode.Static : TypeLoadMode.Dynamic;
    defaults.Production.ResourceAutoCreate = usesPreGeneratedCode ? AutoCreate.None : AutoCreate.CreateOrUpdate;
    defaults.Production.AssertAllPreGeneratedTypesExist = usesPreGeneratedCode;

    defaults.Development.GeneratedCodeMode = TypeLoadMode.Dynamic;
    defaults.Development.ResourceAutoCreate = AutoCreate.CreateOrUpdate;
});

if (isGeneratingOpenApi)
{
    // Must come after IntegrateWithWolverine(), which registers the message store
    builder.Services.DisableAllWolverineMessagePersistence();
    builder.Services.DisableAllExternalWolverineTransports();
}
else
{
    // Create/update Marten and Wolverine schema objects at startup, and apply EF Core migrations
    builder.Services.AddResourceSetupOnStartup();
    builder.Services.AddHostedService<MigrateOnStartup>();
}

builder.Services.AddWolverineHttp();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Wolverine", "Marten"))
    .WithMetrics(metrics => metrics.AddMeter("Wolverine:*", "Marten"));

// BCC Core API (person details): one client with the app's own credentials, see CoreApiSettings
builder.Services.AddCoreApi(builder.Configuration);

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Liveness: no dependencies
app.MapGet("/up", () => TypedResults.Ok(new UpStatus("OK")))
    .WithName("Up");
// Readiness: checks Postgres and the BCC Core API
app.MapHealthChecks("/health");

// Wolverine.Http endpoints. Invalid requests get a 400 with field-keyed ValidationProblemDetails from FluentValidation;
// Marten concurrency failures map to 409 Conflict instead of 500.
app.MapWolverineEndpoints(options =>
{
    options.RoutePrefix("api");
    options.UseFluentValidationProblemDetailMiddleware();
    options.MapMartenConcurrencyFailuresToConflict();
});

// JasperFx command line: `run` (the default) starts the app; `codegen write` generates the code production loads
return await app.RunJasperFxCommands(args);

record UpStatus(string Status);
