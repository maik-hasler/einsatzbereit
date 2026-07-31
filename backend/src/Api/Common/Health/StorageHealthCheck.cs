using Application.Common.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Common.Health;

internal sealed class StorageHealthCheck(
	IFileStorageService fileStorageService)
	: IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			await fileStorageService.PingAsync(cancellationToken);

			return HealthCheckResult.Healthy("Storage is reachable.");
		}
		catch (Exception exception)
		{
			return HealthCheckResult.Unhealthy("Storage is not reachable.", exception);
		}
	}
}
