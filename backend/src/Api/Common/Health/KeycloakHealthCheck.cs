using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Common.Health;

internal sealed class KeycloakHealthCheck(
	IHttpClientFactory httpClientFactory,
	IConfiguration configuration)
	: IHealthCheck
{
	public const string HttpClientName = "keycloak-health";

	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		var baseUrl = configuration["Keycloak:BaseUrl"];
		var realm = configuration["Keycloak:Realm"];

		if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(realm))
		{
			return HealthCheckResult.Unhealthy("Keycloak configuration is missing.");
		}

		var discoveryUrl =
			$"{baseUrl.TrimEnd('/')}/realms/{realm}/.well-known/openid-configuration";

		try
		{
			var client = httpClientFactory.CreateClient(HttpClientName);
			using var response = await client.GetAsync(discoveryUrl, cancellationToken);

			return response.IsSuccessStatusCode
				? HealthCheckResult.Healthy("Keycloak is reachable.")
				: HealthCheckResult.Unhealthy(
					$"Keycloak returned status code {(int)response.StatusCode}.");
		}
		catch (Exception exception)
		{
			return HealthCheckResult.Unhealthy("Keycloak is not reachable.", exception);
		}
	}
}
