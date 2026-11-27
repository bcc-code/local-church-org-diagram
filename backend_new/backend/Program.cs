var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/up", () => Results.Ok(new { Status = "OK" }));

app.Run();
