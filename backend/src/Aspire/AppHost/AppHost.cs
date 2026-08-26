using System.Text.Json.Nodes;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
	.WithPgAdmin();

var isTestEnv = builder.Environment.EnvironmentName == "Testing";

if (!isTestEnv)
	postgres.WithDataVolume();

// Fixed host ports below are for local dev convenience (predictable URLs like
// localhost:8080/9000/1025) - nothing in IntegrationTests/VisualTests depends
// on a specific port, they all resolve endpoints dynamically via
// _app.GetEndpoint(...)/_app.CreateHttpClient(...). CI runs many of these test
// jobs on the same runner in sequence, and a fixed host port left bound by a
// container DCP couldn't clean up in time (or another job's leftover
// container) fails every subsequent bind attempt the same way, which turned
// one transient hiccup into an unrecoverable "port is already allocated"
// retry storm (#2204). Passing port: null here lets Docker pick a free
// ephemeral port instead, so isTestEnv runs can never collide on a fixed one.
var mailpit = builder.AddContainer("mailpit", "ghcr.io/axllent/mailpit", "v1.31.0")
	.WithHttpEndpoint(port: isTestEnv ? null : 1080, targetPort: 8025, name: "webui", isProxied: false)
	.WithEndpoint(port: isTestEnv ? null : 1025, targetPort: 1025, name: "smtp", scheme: "tcp", isProxied: false);

var minio = builder.AddContainer("minio", "quay.io/minio/minio", "RELEASE.2025-09-07T16-13-09Z.hotfix.7aa24e772")
	.WithArgs("server", "/data", "--console-address", ":9001")
	.WithEnvironment("MINIO_ROOT_USER", "minio")
	.WithEnvironment("MINIO_ROOT_PASSWORD", "minio123")
	.WithHttpEndpoint(port: isTestEnv ? null : 9000, targetPort: 9000, name: "api", isProxied: false)
	.WithHttpEndpoint(port: isTestEnv ? null : 9001, targetPort: 9001, name: "console", isProxied: false);

var minioApiEndpoint = minio.GetEndpoint("api");

var database = postgres.AddDatabase("einsatzbereit");

var keycloakRealmPath = Path.GetFullPath(
	Path.Combine(builder.AppHostDirectory, "..", "..", "..", "..", "keycloak", "realms"));

var keycloakThemePath = Path.GetFullPath(
	Path.Combine(builder.AppHostDirectory, "..", "..", "..", "..", "keycloak", "themes", "einsatzbereit"));

var localRealm = JsonNode.Parse(
	File.ReadAllText(Path.Combine(keycloakRealmPath, "einsatzbereit-realm.json")))!;
if (localRealm["clients"] is JsonArray realmClients)
{
	foreach (var client in realmClients)
	{
		if (client is not JsonObject clientObject)
			continue;

		var clientId = clientObject["clientId"]?.GetValue<string>();

		if (clientId == "frontend")
		{
			clientObject["webOrigins"] = new JsonArray("*");
			clientObject["redirectUris"] = new JsonArray("http://localhost:*");

			if (clientObject["attributes"] is JsonObject frontendAttributes)
				frontendAttributes["post.logout.redirect.uris"] = "http://localhost:*";
		}

		if (clientId == "frontend-test")
			clientObject["enabled"] = true;

		if (clientId == "backend")
			clientObject["secret"] = "backend-secret";
	}
}

if (localRealm["users"] is JsonArray realmUsers)
{
	foreach (var user in realmUsers)
	{
		if (user is not JsonObject userObject)
			continue;

		var username = userObject["username"]?.GetValue<string>();

		if (username is "vera" or "olaf" or "admin")
			userObject["enabled"] = true;
	}
}

localRealm["bruteForceProtected"] = false;

localRealm["accessTokenLifespan"] = 3600;

localRealm["smtpServer"] = new JsonObject
{
	["host"] = "mailpit",
	["port"] = "1025",
	["from"] = "noreply@einsatzbereit.local",
	["fromDisplayName"] = "Einsatzbereit",
	["ssl"] = "false",
	["starttls"] = "false",
	["auth"] = "false",
};

var keycloakRealmImportPath = Path.Combine(
	Path.GetTempPath(), "einsatzbereit-aspire-realm-import");
Directory.CreateDirectory(keycloakRealmImportPath);
File.WriteAllText(
	Path.Combine(keycloakRealmImportPath, "einsatzbereit-realm.json"),
	localRealm.ToJsonString());

var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.7.2")
	.WithEnvironment("KC_DB", "dev-file")
	.WithBindMount(keycloakRealmImportPath, "/opt/keycloak/data/import", isReadOnly: true)
	.WithBindMount(keycloakThemePath, "/opt/keycloak/themes/einsatzbereit", isReadOnly: true)
	.WithArgs("start-dev", "--import-realm")
	.WithHttpEndpoint(port: isTestEnv ? null : 8080, targetPort: 8080, isProxied: false)

	.WithHttpHealthCheck("/realms/einsatzbereit/.well-known/openid-configuration");

var keycloakEndpoint = keycloak.GetEndpoint("http");

var mailpitSmtpEndpoint = mailpit.GetEndpoint("smtp");

var anonymousReadPermitLimit = builder.Configuration["RateLimiting:Read:AnonymousPermitLimit"] ?? "10000";

var backend = builder.AddProject<Projects.Api>("backend")
	.WithReference(database)
	.WaitFor(database)
	.WaitFor(keycloak)
	.WaitFor(minio)
	.WithEnvironment("Authentication__Authority",
		ReferenceExpression.Create($"{keycloakEndpoint}/realms/einsatzbereit"))
	.WithEnvironment("Authentication__ValidIssuers__0",
		ReferenceExpression.Create($"{keycloakEndpoint}/realms/einsatzbereit"))
	.WithEnvironment("Keycloak__BaseUrl",
		ReferenceExpression.Create($"{keycloakEndpoint}"))
	.WithEnvironment("Keycloak__ClientSecret", "backend-secret")
	.WithEnvironment("Smtp__Host", mailpitSmtpEndpoint.Property(EndpointProperty.Host))
	.WithEnvironment("Smtp__Port", mailpitSmtpEndpoint.Property(EndpointProperty.Port))
	.WithEnvironment("Storage__Endpoint", ReferenceExpression.Create($"{minioApiEndpoint}"))
	.WithEnvironment("Storage__AccessKey", "minio")
	.WithEnvironment("Storage__SecretKey", "minio123")
	.WithEnvironment("Storage__BucketName", "einsatzbereit")
	.WithEnvironment("RateLimiting__Write__PermitLimit", "10000")
	.WithEnvironment("RateLimiting__Read__AuthenticatedPermitLimit", "10000")

	.WithEnvironment("RateLimiting__Read__AnonymousPermitLimit", anonymousReadPermitLimit);

if (isTestEnv)
{
	// Every existing integration/visual test runs the backend as ASPNETCORE_ENVIRONMENT=
	// Development (the default below), which skips RequiredConfigurationValidator entirely
	// and always runs migrate+seed - the non-Development branch of Program.cs has never
	// been exercised by a test (#2204). ProductionEnvironmentFixture opts a single, separate
	// test class into "Production" here without touching that default for the other ~70.
	backend.WithEnvironment("ASPNETCORE_ENVIRONMENT",
		builder.Configuration["Testing:BackendAspNetCoreEnvironment"] ?? "Development");

	backend.WithEnvironment("Geocoding__UseFakeService", "true");
}

// Outside Development, Program.cs only migrates when this is true (and never seeds) - see
// the isTestEnv block above. Unconditional (not isTestEnv-gated) for the same reason
// RateLimiting:Read:AnonymousPermitLimit above is: a config passthrough, not a test hook,
// and a no-op in real Development usage since Program.cs never reads it there.
if (builder.Configuration["Database:MigrateOnStartup"] is { } migrateOnStartup)
	backend.WithEnvironment("Database__MigrateOnStartup", migrateOnStartup);

// Same passthrough shape as Database:MigrateOnStartup above, for the same reason - see
// Program.cs's comment on RequireHttpsMetadata for why ProductionEnvironmentFixture is
// the only caller that ever sets this.
if (builder.Configuration["Authentication:RequireHttpsMetadata"] is { } requireHttpsMetadata)
	backend.WithEnvironment("Authentication__RequireHttpsMetadata", requireHttpsMetadata);

var frontend = builder.AddViteApp("frontend", "../../../../frontend")
	.WithPnpm()
	.WithReference(backend)
	.WaitFor(backend)
	.WithEnvironment("VITE_API_URL", backend.GetEndpoint("http"))
	.WithEnvironment("VITE_KEYCLOAK_AUTHORITY_URL",
		ReferenceExpression.Create($"{keycloakEndpoint}/realms/einsatzbereit"))
	.WithEnvironment("STORAGE_PUBLIC_URL", ReferenceExpression.Create($"{minioApiEndpoint}"))

	.WithEnvironment("VITE_TOAST_LIFETIME_MS", isTestEnv ? "0" : "5000");

backend.WithEnvironment("Cors__Origins__0", frontend.GetEndpoint("http"));

builder.Build().Run();
