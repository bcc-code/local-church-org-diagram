using BccCode.Core.Api.Client;
using BccCode.Linq.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Backend.CoreApi;

/// <summary>
/// Healthy when the app gets an access token and can read a person from the BCC Core API; unhealthy without credentials.
/// </summary>
public class CoreApiHealthCheck(ICoreApiClient coreApi, CoreApiSettings settings, ILogger<CoreApiHealthCheck> logger)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        if (!settings.IsConfigured)
            return HealthCheckResult.Unhealthy(new CoreApiNotConfiguredException().Message);

        try
        {
            await coreApi.Persons.Query(q => q.Fields = "uid").Take(1).ToListAsync(ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "BCC Core API health check failed");
            return HealthCheckResult.Unhealthy("BCC Core API connectivity issue", e);
        }
    }
}
