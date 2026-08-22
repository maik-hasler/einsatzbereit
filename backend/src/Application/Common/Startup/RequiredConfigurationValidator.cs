namespace Application.Common.Startup;

// appsettings.json ships dev-friendly fallbacks (a working Postgres
// superuser connection string, a plain-http Keycloak authority) so local
// dev works with zero configuration. Those same fallbacks also ship inside
// the released Docker image - if an env var override is ever dropped or
// misspelled, the app would otherwise start up fine and connect
// with dev credentials/plain http instead of failing loudly. Only
// Development is allowed to fall back silently.
public static class RequiredConfigurationValidator
{
	public static IReadOnlyList<string> FindMissing(
		bool isDevelopment,
		string? connectionString,
		string? keycloakClientSecret,
		string? authenticationAuthority,
		string[]? corsOrigins)
	{
		if (isDevelopment)
			return [];

		var missing = new List<string>();

		if (string.IsNullOrWhiteSpace(connectionString))
			missing.Add("ConnectionStrings:einsatzbereit");

		if (string.IsNullOrWhiteSpace(keycloakClientSecret))
			missing.Add("Keycloak:ClientSecret");

		if (string.IsNullOrWhiteSpace(authenticationAuthority))
			missing.Add("Authentication:Authority");

		if (corsOrigins is not { Length: > 0 })
			missing.Add("Cors:Origins");

		return missing;
	}
}
