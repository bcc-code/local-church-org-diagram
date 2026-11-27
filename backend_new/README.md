# backend_new

.NET 10 replacement for the Flask backend (issue #61), deployed to production (see [Deployment](#deployment)). The Flask
backend in `backend/` is no longer deployed and stays until the migration is done.

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
  - `Auth/` – session cookie and authorization policies (`ChurchMember`), BCC sign-on claims; see [Authentication](#authentication)
  - `Features/<Feature>/` – the API, one folder per feature: its `Endpoints` class, response records and pure logic; see
    [API](#api)
  - `CoreApi/` – BCC Core API client (`BccCode.Core.Api.Client`): `IPersonDirectory` gets person details
    (UID, display name, profile picture) by UID or by free-text search
  - `Auth/` – BCC sign-on (cookie session), claims, roles from the `Admins` table and policies, see
    [Authentication and authorization](#authentication-and-authorization)
  - `Data/` – EF Core `AppDbContext` (`Admins`, Data Protection keys) and its migrations
  - `Groups/` – group events and projections (`GroupView`, `MembershipView`), see [Groups and memberships](#groups-and-memberships)
  - `LegacyImport/` – CLI commands importing the Flask backend's tables
  - `Admins/` – the `admins` CLI command managing the Admins table, see [Admins](#admins)
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

Database tests (`Category=Database`, `[DatabaseFact]`, `[Collection(DatabaseCollection.Name)]`) boot the app against a
real Postgres, sharing one host and running one at a time: they create the database `org_diagram_test` on the server
`TEST_DATABASE_URL` points to (localhost or a `*test*` database only) and are skipped without it. CI provides a
postgres service.

```bash
docker run -d --rm --name org-pg -e POSTGRES_PASSWORD=pw -p 5432:5432 postgres:17
TEST_DATABASE_URL=postgresql://postgres:pw@localhost:5432/postgres dotnet test
```

## Groups and memberships

Each group is an event stream (`Groups/`), and its memberships are events on that stream. Stream IDs are UUIDv5s
derived from the legacy `groups.id` (`GroupIds`). Inline projections reproduce the Flask backend's tables:

- `GroupView` (`groups`): name, parent, tenant, sort order, created at, and the group's member count
- `MembershipView` (`group_membership`), one per group and person: person UID, title, link, tenant, created at

Legacy IDs are only on the events (`LegacyId`, …), not on the views.

### Rebuilding projections

An inline projection only applies to events appended after it changes. After changing one (e.g. a new field on
`GroupView`), rebuild it from the event store, here in Development against the Aspire database:

```bash
dotnet run --project backend -- projections rebuild -p GroupView
```

Against production, set `ASPNETCORE_ENVIRONMENT` and `ConnectionStrings__Default` as for the import below and add
`--no-launch-profile`; like the import, it runs from a checkout without `codegen write`.

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

## Authentication and authorization

Users sign in with BCC sign-on (OpenID Connect, code flow) and keep a cookie session (30 days, sliding), as with the
Flask backend: the SPA needs no tokens. It uses the Flask backend's OIDC client and its registered callback `/authorize`.

- `GET /login?returnUrl=/path` – signs in, then returns to the path (only paths on this site, otherwise `/`)
- `GET /authorize` – the callback. One that fails (cancelled, stale, missing cookies) answers 400 with a link to sign in
  again, not a redirect, so it can't loop with the SPA's 401 → `/login`
- `GET /logout` – ends the session here (not at BCC sign-on), then goes to `/`
- `/api` answers 401 (signed out) or 403 instead of redirecting

Claims keep BCC sign-on's names (`Auth/BccClaims`): `GetPersonUid()` (`…/claims/personUid`, what memberships and
`Admins` refer to), `GetPersonId()`, `GetChurchId()` (the user's tenant) and `GetHasMembership()`.

Tenants are BCC church IDs (`TenantId` on groups and memberships). Roles come from the EF Core `Admins` table, read on
every request (`RoleClaimsTransformation`), so changes apply on the next request:

| `Admins` row                   | Role          | Claims added                                            |
| ------------------------------ | ------------- | ------------------------------------------------------- |
| `TenantId` null                | Global Admin  | `role=GlobalAdmin`: every tenant                        |
| `TenantId` set (one per tenant) | Local Admin  | `role=LocalAdmin`, `admin_tenant=<id>`: that tenant, whatever the person's own church |

A row has a required `Name` (who it is for) and is unique per person and tenant (one global row per person).

Policies (`Auth/AuthPolicies`). Every policy, the default one included, requires a signed-in BCC member (person UID,
`hasMembership` and a `churchId`: a person without a church is refused like one without membership). Wolverine.Http endpoints require the default policy unless they say otherwise
(`RequireAuthorizeOnAll`; `[AllowAnonymous]`, `[Authorize(Policy = …)]`).

- default (`[Authorize]`, every `/api` endpoint) – a signed-in member
- `GlobalAdmin` – Global Admins
- `TenantReader` – against a tenant (`ITenantScoped`, e.g. `new Tenant(id)`): its admins and its church's members
- `TenantAdmin` – against a tenant: Global Admins and its Local Admins. For changes

An endpoint scoped to a tenant checks it in `Before`:

```csharp
public static Task<ProblemDetails> Before(long tenantId, ClaimsPrincipal user, IAuthorizationService authorization) =>
    authorization.AuthorizeTenantAsync(user, tenantId, AuthPolicies.TenantReader); // 403 problem when denied
```

Endpoints use the user's own church (`user.GetChurchId()`) as the tenant for now; a tenant parameter for admins of other
tenants comes later.

The Data Protection key ring (session cookies, sign-in state) is stored in Postgres (`DataProtectionKeys`), so sessions
survive restarts and deploys. Behind a TLS-terminating proxy, the request scheme must be forwarded
(`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`), or the callback URL sent to BCC sign-on is `http://`.

There is no local dev login yet: Development signs in with BCC sign-on too, so it needs the client's credentials.
Through Vite (`:5173`), the callback URL is `http://localhost:5173/authorize`, which the client must allow.

### Adding admins

With the `admins` command, see [Admins](#admins).

### Tests

`BackendFactory` signs requests in as a `TestUser` (`backend.Tests/Auth/TestAuth.cs`) carried in a header, roles
included, so endpoint tests need neither BCC sign-on nor the `Admins` table; requests without it are signed out:

```csharp
var client = factory.CreateClient(TestUser.Member(churchId: 51));     // also NonMember, LocalAdmin(51), Global()
```

Policy and handler unit tests use `TestUser.Principal()`. `Database/AdminsTests` cover the claims transformation and the
table's constraints.

## Admins

The EF Core `Admins` table (`Data/Admin.cs`) holds one row per admin assignment: a person (BCC person UID) is a
**Global Admin** (`TenantId` null, every tenant) or a **Local Admin** of a tenant (a BCC church ID, the groups'
`tenant_id`), and can have both. The `admins` command manages it. It's built for agents running it on someone's behalf
(see the `manage-admins` skill in `.agents/skills/`): it never prompts, `--json` prints one JSON document on stdout, add
and remove are idempotent, and it exits 0 on success (no-ops included) and 1 on any error.

```bash
dotnet run --project backend -- admins search <name or UID> [--limit <n>] [--json]
dotnet run --project backend -- admins list [<person UID>] [--tenant <id>] [--global] [--json]
dotnet run --project backend -- admins add <person UID> (--tenant <id> | --global) [--name <name>] [--dry-run] [--json]
dotnet run --project backend -- admins remove <person UID> (--tenant <id> | --global | --all) [--yes] [--dry-run] \
                                              [--allow-last-global] [--json]
```

- `search` – persons in the BCC Core API matching a name (free text, at most `--limit`, default 10), or the person with
  that UID, each with their admin rows. Resolve a name to a UID with it before `add`.
- `list` – admin rows, Global Admins first, then by tenant and name. With a UID, only that person's rows. `--tenant` and
  `--global` each select their rows; both together select both: everyone who can administer that tenant.
- `add` – adds the row, named after the person's Core API display name unless `--name` is given (then the Core API isn't
  called). An existing row is left as it is, name included (`unchanged`). Warns (`tenant_has_no_groups`) when no group
  has the tenant, which usually means a wrong church ID.
- `remove` – removes the row of one scope, or every row of the person (`--all`). Without `--yes` it removes nothing and
  fails with `confirmation_required`, listing the rows it would remove. It refuses to remove the only Global Admin
  (`last_global_admin`) without `--allow-last-global`. A row that isn't there is `not_found`, exit 0.
- `--dry-run` (add, remove) reports what would change and changes nothing.
- A flag that doesn't apply to the action (e.g. `add --yes`) is an error, not ignored.

With `--json`, stdout carries only the result; logs, warnings and errors also go to stderr:

```json
{
  "status": "added",
  "admins": [
    {
      "id": 7,
      "personUid": "1f0c…",
      "name": "Kari Nordmann",
      "scope": "local",
      "tenantId": 4512,
      "createdAt": "2026-11-27T12:00:00+00:00"
    }
  ],
  "warnings": [{ "code": "tenant_has_no_groups", "message": "…" }]
}
```

- `status`: `ok` (list, search), `added` / `unchanged` / `would_add` (add), `removed` / `not_found` / `would_remove`
  (remove), or `error`.
- `admins` (list, add, remove) – `scope` is `global` (`tenantId` null) or `local`; a dry run's new row has `id` and
  `createdAt` null. `persons` (search) – `uid`, `displayName` and `admins`.
- `error` (with `status: error`) – `code` is one of `invalid_input`, `person_not_found`, `person_has_no_name`,
  `core_api_not_configured`, `core_api_error`, `migrations_pending`, `last_global_admin`, `confirmation_required`,
  `unexpected`, and `message` says what to do.

Like the import commands, `admins` doesn't start the app and, in Production, doesn't need `codegen write`. It needs
`ConnectionStrings:Default`, and the Core API credentials for `search` and for `add` without `--name`. It never migrates
the database: with migrations pending (the app applies them at startup) it fails with `migrations_pending`.

Against production (Supabase session pooler, port 5432):

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__Default="Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=...;SSL Mode=Require"
export CoreApi__ClientId=... CoreApi__ClientSecret=...
dotnet run --project backend --no-launch-profile -- admins search "Kari Nordmann" --json
dotnet run --project backend --no-launch-profile -- admins add <UID> --tenant <church ID> --dry-run --json
dotnet run --project backend --no-launch-profile -- admins add <UID> --tenant <church ID> --json
```

## API

Wolverine.Http endpoints under `/api`, one folder per feature in `backend/Features/`. They read the projections and need a
signed-in member (the default policy). All but the search are scoped to the user's church (`GetChurchId()`) and check
it with `TenantReader` in `Before` (see [Authentication and authorization](#authentication-and-authorization)). Groups
and persons are identified by Guid (group stream ID, person UID).

| Feature                   | Endpoint                               | Operation ID       | Reads                                   |
| ------------------------- | -------------------------------------- | ------------------ | --------------------------------------- |
| `MembershipTree`          | `GET /api/tree`                        | `ListGroups`       | `GroupView`                             |
| `GroupMembershipListing`  | `GET /api/persons?group_id=`           | `ListGroupMembers` | `MembershipView`, Core API              |
| `TitleSearch`             | `GET /api/titles`                      | `ListTitles`       | `MembershipView`                        |
| `TitleSearch`             | `GET /api/titles/{title}/persons`      | `ListTitleHolders` | `MembershipView`, Core API              |
| `PersonSearch`            | `GET /api/persons/search?q=`           | `SearchPersons`    | Core API (any church)                   |
| `PersonMembershipListing` | `GET /api/persons/{personUid}/groups`  | `ListPersonGroups` | `MembershipView`, `GroupView`           |

They are drop-in replacements for the Flask backend's GET endpoints (`backend/api.py`), so the frontend works against
either: the same routes, data and ordering, and the same JSON (`Api/ApiConventions`):

- snake_case property names, from one global naming policy (`ConfigureHttpJsonOptions`), which the OpenAPI document and
  the generated client follow too. Name record properties so the policy gives Flask's names (`GroupNode.GroupId` →
  `group_id`, `Label` → `label`), rather than `[JsonPropertyName]` on each.
- errors as `{"error": "..."}` (`ApiError`): a problem an endpoint returns (`ProblemDetails` from `Before`, FluentValidation
  failures: the first message) is written in that shape by an `IProblemDetailsWriter` for `/api`, and the authorization
  middleware answers 401 (`Authentication required`, never a redirect) and 403 with it. `ApiErrorsOpenApi` documents it.
- a group of another church, or an unknown one, has no members (`[]`), as Flask's tenant-filtered query.

Group IDs are Guid strings where Flask had integers: the frontend only compares them and sends them back.
`backend.Tests/Api/FlaskContractTests` pins the exact JSON of each endpoint.

The pages `GET /` and `GET /{tenantId}/admin` (`Api/AppPages`) serve the SPA's `index.html` from the web root to
signed-in members, as Flask's `index()`: signed-out users are redirected to `/login?returnUrl=<path>`, and
`/{tenantId}/admin` answers 403 `Unauthorized` unless `tenantId` is the user's church.
Demo mode (`DEMO_MODE`) isn't ported.

Tests: one file per feature, `backend.Tests/Features/<Feature>Tests.cs` (namespace `Backend.Tests.Features`), with unit
tests of the pure methods and database tests calling the endpoints over HTTP (see [Database tests](#database-tests)),
seeded with `Seed` under a tenant of their own.

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

### BCC sign-on

The OpenID Connect client users sign in with (scopes `openid profile email church`), shared with the Flask backend.

| Setting                  | Fallback                 | Default                |
| ------------------------ | ------------------------ | ---------------------- |
| `BccSignOn:Authority`    |                          | `https://login.bcc.no` |
| `BccSignOn:ClientId`     | `BCC_OIDC_CLIENT_ID`     |                        |
| `BccSignOn:ClientSecret` | `BCC_OIDC_CLIENT_SECRET` |                        |

```bash
dotnet user-secrets --project backend set BccSignOn:ClientId ...
dotnet user-secrets --project backend set BccSignOn:ClientSecret ...
```

## Deployment

`.github/workflows/cd.yml` deploys on every push to `main` (or manually) to the Azure Web App `org-diagram` (resource
group `org-diagram_group`, Linux, B1, a single instance), which serves https://org.hkonline.dk:

1. `backend-new.yml` (CI, then `codegen write` and `dotnet publish`, framework-dependent) and `frontend.yml` (CI and
   `pnpm build`) run with `publish: true` and upload the artifacts `backend-new` and `frontend`
2. the deploy job puts the SPA into the published backend's `wwwroot` and zip-deploys it with the publish profile
   (`AZURE_WEBAPP_PUBLISH_PROFILE` secret, `AZURE_WEBAPP_NAME` variable). App Service runs it as it is
   (`SCM_DO_BUILD_DURING_DEPLOYMENT=false`), with `dotnet backend.dll`
3. it waits for `GET /up` to answer 200: startup applies EF Core migrations and Marten/Wolverine schema changes first

The app serves the SPA's assets from `wwwroot` (public, as Flask's static folder) and its pages through `AppPages`.

Configuration is in the Web App's app settings, not in the repository (`appsettings.Production.json` only sets log
levels):

| App setting                                       | Value                                                                    |
| ------------------------------------------------- | ------------------------------------------------------------------------ |
| `ASPNETCORE_ENVIRONMENT`                          | `Production`                                                             |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED`             | `true`: App Service terminates TLS, and the sign-in callback must be https |
| `ConnectionStrings__Default`                      | Supabase's session pooler (port 5432), see [Database](#database)         |
| `BCC_OAUTH_CLIENT_ID`, `BCC_OAUTH_CLIENT_SECRET`  | the Core API client, see [BCC Core API](#bcc-core-api)                   |
| `BCC_OIDC_CLIENT_ID`, `BCC_OIDC_CLIENT_SECRET`    | the sign-on client, see [BCC sign-on](#bcc-sign-on)                      |
| `SCM_DO_BUILD_DURING_DEPLOYMENT`                  | `false`                                                                  |

Site settings: runtime `DOTNETCORE|10.0`, startup command `dotnet backend.dll`, health check path `/up` (`/health`
also checks the Core API, whose outage shouldn't get the instance replaced), Always On (Wolverine's durability agent
runs in the background).

Production runs exactly one instance: Wolverine's `Solo` mode relies on it. Never scale the app out, and never run a
second app (or a local `dotnet run` as Production) against its database. The one-off commands (legacy import,
`projections rebuild`, `admins`) don't start Wolverine, so they're safe to run from a checkout.
