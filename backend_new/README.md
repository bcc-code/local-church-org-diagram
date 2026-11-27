# backend_new

.NET 10 replacement for the Flask backend (issue #61). Runs alongside `backend/` until the migration is done.

```bash
cd backend_new
dotnet tool restore             # aspire, dotnet-ef
aspire run                      # Postgres + backend + Vite frontend, dashboard at http://localhost:15000
dotnet run --project backend    # backend only, http://localhost:5001 (Flask keeps :5000); needs a database
dotnet test
dotnet format                   # CI runs it with --verify-no-changes
```

- `apphost/` – Aspire AppHost: Postgres 17 in the persistent container `org-diagram-postgres` (host port 5433, volume
  `org-diagram_postgres_data`, shared by all worktrees), the backend, and the Vite dev server on :5173 with `/api` and
  `/login` proxied to the backend (`API_PROXY_TARGET`; plain `pnpm dev` still proxies to Flask on :5000)
- `servicedefaults/` – OpenTelemetry, service discovery, HTTP resilience, `/alive` (Development)
- `backend/` – ASP.NET Core
  - `GET /up` – liveness, no dependencies
  - `GET /health` – readiness: `Healthy` (200) when Postgres and the BCC Core API answer, `Unhealthy` (503) otherwise
  - `CoreApi/` – BCC Core API client (`BccCode.Core.Api.Client`): `IPersonDirectory` gets person details
    (UID, display name, profile picture) by UID or by free-text search
  - `Data/` – EF Core `AppDbContext` and its migrations
  - `Groups/` – group events and projections (`GroupView`, `MembershipView`), see [Groups and memberships](#groups-and-memberships)
  - `LegacyImport/` – CLI commands importing the Flask backend's tables
- `backend.Tests/` – xUnit + `WebApplicationFactory` tests; `CoreApiStub` stands in for login.bcc.no and the Core API

## Persistence and messaging

Marten (event store and documents), EF Core and Wolverine share one Postgres database and one `NpgsqlDataSource`
(connection string `Default`, Aspire.Npgsql), in separate schemas:

- `public` – EF Core (`AppDbContext`), managed by migrations, applied at startup (`MigrateOnStartup`). Add one with
  `dotnet ef migrations add <Name> --project backend -o Data/Migrations` (needs `ConnectionStrings__Default` set, e.g.
  `Host=localhost`; it doesn't connect).
- `marten` – Marten documents and events, created/updated at startup (`AddResourceSetupOnStartup`), no migrations.
  Production never creates schema objects on first use, so register every document type, projection and snapshot in
  `AddMarten`.
- `wolverine` – Wolverine inbox/outbox and node tables (`IntegrateWithWolverine`).

Wolverine applies transactions automatically (`AutoApplyTransactions`: Marten for handlers using `IDocumentSession`, EF
Core for `AppDbContext`), local queues are durable, and committed Marten events are forwarded to Wolverine handlers.
HTTP endpoints are Wolverine.Http under `/api`, with FluentValidation problem details and Marten concurrency failures
as 409.

Production loads Marten/Wolverine code generated before publishing and refuses to start without it:

```bash
ASPNETCORE_ENVIRONMENT=Production ConnectionStrings__Default=Host=localhost \
  dotnet run --project backend --no-launch-profile -p:OpenApiGenerateDocumentsOnBuild=false -- codegen write
```

writes the gitignored `backend/Internal/Generated/` (CI runs it). Production also runs Wolverine in `Solo` durability
mode (a single instance); everywhere else it's `Balanced`, since local worktrees share one database.

### Database tests

Tests in `backend.Tests/Database/` (`Category=Database`) boot the app against a real Postgres: they create the database
`org_diagram_test` on the server `TEST_DATABASE_URL` points to (localhost or a `*test*` database only) and are skipped
without it. CI provides a postgres service.

```bash
docker run -d --rm --name org-pg -e POSTGRES_PASSWORD=pw -p 5432:5432 postgres:17
TEST_DATABASE_URL=postgresql://postgres:pw@localhost:5432/postgres dotnet test
```

## Groups and memberships

Each group is an event stream (`Groups/`), and its memberships are events on that stream. Stream IDs are UUIDv5s
derived from the legacy `groups.id` (`GroupIds`). Inline projections reproduce the Flask backend's tables:

- `GroupView` (`groups`): name, parent, tenant, sort order, created at
- `MembershipView` (`group_membership`), one per group and person: person UID, title, link, tenant, created at

Legacy IDs are only on the events (`LegacyId`, …), not on the views.

### Importing the Flask backend's tables

`GroupImported` and `MembershipImported` come only from a one-off import of `public.groups` and
`public.group_membership` (`LegacyImport/`). Each command runs in a single transaction and prints the rows read, the
events appended and the view counts. Rows without a `tenant_id` fail the import, and nothing is written. Import groups
first: memberships are appended to their group's stream.

```bash
dotnet run --project backend -- import-groups      [--source <connection string>] [--dry-run]
dotnet run --project backend -- import-memberships [--source <connection string>] [--dry-run]
dotnet run --project backend -- import-reset --yes   # deletes ALL Marten events and documents
```

- Target: `ConnectionStrings:Default`. The commands create or update the Marten schema themselves.
- Source: the target database by default, or `--source` (an Npgsql connection string, not a `postgresql://` URI). It
  is only read, in a read-only transaction.
- `--dry-run` imports, reports, then rolls back. A memberships dry run needs the groups imported.
- The commands don't start the app (no web server, no Wolverine) and, in Production, don't need `codegen write`.
- The commands don't check for an earlier import: run each once, or `import-reset --yes` and start over.

Against production (Supabase session pooler, port 5432):

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__Default="Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=...;SSL Mode=Require"
dotnet run --project backend --no-launch-profile -- import-groups --dry-run
dotnet run --project backend --no-launch-profile -- import-groups
dotnet run --project backend --no-launch-profile -- import-memberships --dry-run
dotnet run --project backend --no-launch-profile -- import-memberships
```

## API client

`dotnet build` writes the OpenAPI document to `openapi/backend.json` (gitignored). `pnpm generate:api` in `frontend/`
builds the backend and regenerates the NSwag TypeScript client `frontend/src/api/generated/client.ts` (committed, never
edit by hand); CI fails when it's stale. An endpoint's operation ID (`.WithName("X")`, or `OperationId` on the Wolverine
attribute) becomes the client method name. Return named types (`TypedResults.Ok(record)`), or the client gets
`Promise<void>`.

## Configuration

### Database

`ConnectionStrings:Default`, an Npgsql connection string. `aspire run` sets it for the local container. Production uses
Supabase's **session pooler** (port 5432), not the transaction pooler (6543) the Flask backend uses: Marten and Wolverine
rely on session state (advisory locks).

```bash
dotnet user-secrets --project backend set ConnectionStrings:Default "Host=localhost;Port=5433;Database=org_diagram;Username=org_diagram;Password=org_diagram"
```

### BCC Core API

One OAuth client (client credentials), granted `persons.name#read` and `persons.profile_picture#read`. The SDK gets,
caches and renews the access token.

| Setting                 | Fallback                  | Default |
| ----------------------- | ------------------------- | ------- |
| `CoreApi:Environment`   |                           | `prod`  |
| `CoreApi:ClientId`      | `BCC_OAUTH_CLIENT_ID`     |         |
| `CoreApi:ClientSecret`  | `BCC_OAUTH_CLIENT_SECRET` |         |

The fallbacks are the Flask backend's variables, so both backends can run from the same environment. As environment
variables, use `CoreApi__ClientId` and `CoreApi__ClientSecret`. Locally, user secrets work too:

```bash
dotnet user-secrets --project backend set CoreApi:ClientId ...
dotnet user-secrets --project backend set CoreApi:ClientSecret ...
```

Without credentials the app still starts, Core API calls throw `CoreApiNotConfiguredException` and `/health` is
unhealthy.
