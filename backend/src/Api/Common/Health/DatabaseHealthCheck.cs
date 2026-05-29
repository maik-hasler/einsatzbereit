using Application.Common.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Common.Health;

internal sealed class DatabaseHealthCheck(
	IApplicationDbContext dbContext)
	: IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var canConnect = await dbContext.CanConnectAsync(cancellationToken);

			return canConnect
				? HealthCheckResult.Healthy("Database is reachable.")
				: HealthCheckResult.Unhealthy("Database is not reachable.");
		}
		catch (Exception exception)
		{
			return HealthCheckResult.Unhealthy("Database is not reachable.", exception);
		}
	}
}
