using Microsoft.Extensions.Diagnostics.HealthChecks;
using PushpakClasses.Infrastructure.Persistence;

namespace PushpakClasses.Api.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly PushpakClassesDbContext _context;

    public DatabaseHealthCheck(PushpakClassesDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                return HealthCheckResult.Healthy("SQL Server database connection is responsive.");
            }
            return HealthCheckResult.Unhealthy("Cannot connect to SQL Server database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server health check encountered an exception.", ex);
        }
    }
}
