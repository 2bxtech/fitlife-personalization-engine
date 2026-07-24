using FitLife.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FitLife.Api.Health;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly FitLifeDbContext _context;

    public DatabaseHealthCheck(FitLifeDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database connection is healthy")
                : HealthCheckResult.Unhealthy("Database is not reachable");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Database connection failed",
                exception);
        }
    }
}
