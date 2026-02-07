using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace FFXIV.Api.Health;

public class RedisHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;

    public RedisHealthCheck(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var multiplexer = _serviceProvider.GetService<IConnectionMultiplexer>();
        if (multiplexer is null)
            return HealthCheckResult.Healthy("Redis disabled, using in-memory cache");

        try
        {
            var db = multiplexer.GetDatabase();
            var latency = await db.PingAsync();
            return HealthCheckResult.Healthy($"Redis connected, latency: {latency.TotalMilliseconds:F1}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis is unreachable", ex);
        }
    }
}
