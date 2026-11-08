using Microsoft.Extensions.Diagnostics.HealthChecks;
using StockService.Infrastructure.Persistence;

namespace StockService.Health;

public sealed class DatabaseHealthCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StockDbContext>();
        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("StockDb disponível.")
            : HealthCheckResult.Unhealthy("StockDb indisponível.");
    }
}
