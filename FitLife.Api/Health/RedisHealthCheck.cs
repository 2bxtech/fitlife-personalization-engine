using FitLife.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FitLife.Api.Health;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IRedisHealthProbe _redis;

    public RedisHealthCheck(IRedisHealthProbe redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _redis.PingAsync();
            return HealthCheckResult.Healthy("Redis connection is healthy");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis check failed", exception);
        }
    }
}
