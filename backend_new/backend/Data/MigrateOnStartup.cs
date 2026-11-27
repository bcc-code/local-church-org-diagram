using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

/// <summary>Applies pending EF Core migrations before the server starts (production runs a single instance).</summary>
public class MigrateOnStartup(IServiceProvider services) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
