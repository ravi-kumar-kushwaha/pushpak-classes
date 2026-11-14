using Microsoft.Extensions.Diagnostics.HealthChecks;
using PushpakClasses.Application.Common.Interfaces;

namespace PushpakClasses.Api.HealthChecks;

public class RedisHealthCheck : IHealthCheck
{
    private readonly IRedisService _redisService;

    public RedisHealthCheck(IRedisService redisService)
    {
        _redisService = redisService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Perform active probe key existence check
            var probeResult = await _redisService.ExistsAsync("__health_probe__", cancellationToken);
            return HealthCheckResult.Healthy("Redis distributed cache connection is responsive.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Redis distributed cache connection check failed. L2 cache may be degraded.", ex);
        }
    }
}
