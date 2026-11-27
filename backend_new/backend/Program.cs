using Backend.CoreApi;

var builder = WebApplication.CreateBuilder(args);

// BCC Core API (person details): one client with the app's own credentials, see CoreApiSettings
builder.Services.AddCoreApi(builder.Configuration);

var app = builder.Build();

// Liveness: no dependencies
app.MapGet("/up", () => Results.Ok(new { Status = "OK" }));
// Readiness: checks the BCC Core API
app.MapHealthChecks("/health");

app.Run();
