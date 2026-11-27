var builder = DistributedApplication.CreateBuilder(args);

var postgresUser = builder.AddParameter("postgres-user", "org_diagram");
var postgresPassword = builder.AddParameter("postgres-password", "org_diagram", secret: true);

// Host port 5433, so it runs next to other projects' Postgres on 5432 (e.g. local-church-shop)
var postgres = builder.AddPostgres("postgres", postgresUser, postgresPassword, port: 5433)
    .WithImageTag("17-alpine")
    .WithDataVolume("org-diagram_postgres_data")
    // A fixed name, so every worktree's AppHost reuses the same container instead of creating
    // its own (named after a hash of the AppHost path) that can't bind port 5433
    .WithContainerName("org-diagram-postgres")
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("org-diagram", databaseName: "org_diagram");

// Health check on /up (liveness): /health also checks the BCC Core API, which needs credentials
var backend = builder.AddProject<Projects.backend>("backend", launchProfileName: "http")
    .WithReference(database, connectionName: "Default")
    .WaitFor(database)
    .WithHttpHealthCheck("/up");

// Vite proxies /api (and /login) to API_PROXY_TARGET; without Aspire it defaults to the Flask backend.
// Port 5173 is pinned so the URL stays the same as with `pnpm dev`.
builder.AddViteApp("frontend", "../../frontend")
    .WithPnpm()
    .WithEndpoint("http", endpoint => endpoint.Port = 5173)
    .WithEnvironment("API_PROXY_TARGET", backend.GetEndpoint("http"))
    .WaitFor(backend);

builder.Build().Run();
