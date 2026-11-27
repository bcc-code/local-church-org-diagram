# backend_new

.NET 10 replacement for the Flask backend (issue #61). Runs alongside `backend/` until the migration is done.

```bash
cd backend_new
dotnet run --project backend    # http://localhost:5001 (Flask keeps :5000)
dotnet test
dotnet format                   # CI runs it with --verify-no-changes
```

- `backend/` – ASP.NET Core minimal API
  - `GET /up` – liveness, no dependencies
  - `GET /health` – readiness: `Healthy` (200) when the BCC Core API answers, `Unhealthy` (503) otherwise
  - `CoreApi/` – BCC Core API client (`BccCode.Core.Api.Client`): `IPersonDirectory` gets person details
    (UID, display name, profile picture) by UID or by free-text search
- `backend.Tests/` – xUnit + `WebApplicationFactory` tests; `CoreApiStub` stands in for login.bcc.no and the Core API

## Configuration

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
