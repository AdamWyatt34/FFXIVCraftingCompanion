using FFXIV.Domain.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FFXIV.Api.Health;

public class XivApiHealthCheck : IHealthCheck
{
    private readonly IXivApiClient _xivApiClient;

    public XivApiHealthCheck(IXivApiClient xivApiClient)
    {
        _xivApiClient = xivApiClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var (key, name) = await _xivApiClient.GetCurrentGameVersionAsync(cts.Token);
            return HealthCheckResult.Healthy($"XIVAPI reachable, game version: {name}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("XIVAPI is unreachable", ex);
        }
    }
}
