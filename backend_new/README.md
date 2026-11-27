# backend_new

.NET 10 replacement for the Flask backend (issue #61). Runs alongside `backend/` until the migration is done.

```bash
cd backend_new
dotnet run --project backend    # http://localhost:5001 (Flask keeps :5000)
dotnet test
dotnet format                   # CI runs it with --verify-no-changes
```

- `backend/` – ASP.NET Core minimal API (`GET /up` health check)
- `backend.Tests/` – xUnit + `WebApplicationFactory` integration tests
