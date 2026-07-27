using System.Text.Json.Nodes;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
	.WithPgAdmin();

// Skip persistent volume in test environments to avoid stale migration state
var isTestEnv = builder.Environment.EnvironmentName == "Testing";

if (!isTestEnv)
	postgres.WithDataVolume();

var mailpit = builder.AddContainer("mailpit", "ghcr.io/axllent/mailpit", "latest")
	.WithHttpEndpoint(port: 1080, targetPort: 8025, name: "webui", isProxied: false)
	.WithEndpoint(port: 1025, targetPort: 1025, name: "smtp", scheme: "tcp", isProxied: false);

var minio = builder.AddContainer("minio", "quay.io/minio/minio", "latest")
	.WithArgs("server", "/data", "--console-address", ":9001")
	.WithEnvironment("MINIO_ROOT_USER", "minio")
	.WithEnvironment("MINIO_ROOT_PASSWORD", "minio123")
	.WithHttpEndpoint(port: 9000, targetPort: 9000, name: "api", isProxied: false)
	.WithHttpEndpoint(port: 9001, targetPort: 9001, name: "console", isProxied: false);

var minioApiEndpoint = minio.GetEndpoint("api");

var database = postgres.AddDatabase("einsatzbereit");

var keycloakRealmPath = Path.GetFullPath(
	Path.Combine(builder.AppHostDirectory, "..", "..", "..", "..", "keycloak", "realms"));

var keycloakThemePath = Path.GetFullPath(
	Path.Combine(builder.AppHostDirectory, "..", "..", "..", "..", "keycloak", "themes", "einsatzbereit"));

// The committed realm keeps production security settings; some break the local
// Aspire + Playwright flow. Write a dev-only copy with two relaxations and import
// that. Production never runs this AppHost - it uses the baked image + committed realm.
//  - webOrigins: Aspire serves the frontend on a dynamic http://localhost:<port>
//    origin that no fixed webOrigins entry matches (Keycloak webOrigins are exact CORS
//    origins, not port wildcards), so the browser OIDC token exchange is CORS-blocked.
//    Allow all origins for the public frontend client.
//  - bruteForceProtected: the parallel VisualTests log in concurrently as the shared
//    seed users, which trips brute-force protection and gets rejected. Disable it.
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
			clientObject["webOrigins"] = new JsonArray("*");

		if (clientId == "backend")
			clientObject["secret"] = "backend-secret";
	}
}

localRealm["bruteForceProtected"] = false;

// Local dev never has a real SMTP relay - point straight at the Mailpit
// container by literal value instead of relying on Keycloak's "${VAR}"
// realm-import substitution (which the committed realm now uses for
// staging/production - see docker-compose.yml's keycloak service). Same
// literal-override approach as the "backend" client secret above.
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

var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.7.0")
	.WithEnvironment("KC_DB", "dev-file")
	.WithBindMount(keycloakRealmImportPath, "/opt/keycloak/data/import", isReadOnly: true)
	.WithBindMount(keycloakThemePath, "/opt/keycloak/themes/einsatzbereit", isReadOnly: true)
	.WithArgs("start-dev", "--import-realm")
	.WithHttpEndpoint(port: 8080, targetPort: 8080, isProxied: false);

var keycloakEndpoint = keycloak.GetEndpoint("http");

var mailpitSmtpEndpoint = mailpit.GetEndpoint("smtp");

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
	.WithEnvironment("RateLimiting__Read__AuthenticatedPermitLimit", "10000");

// Integration/Visual tests create opportunities with placeholder addresses
// ("Test Street", "Sample City", ...) that the real Nominatim API correctly
// reports as not found - since #975 that's now a hard validation error, so
// hitting the live public API here would both break those tests and violate
// Nominatim's usage policy against automated/bulk querying from CI. Point at
// an address nothing listens on instead: every geocode call fails fast as a
// connection error, which GeocodingHelper treats as a transient failure (save
// proceeds with null coordinates, same as before this override existed) -
// never as a confirmed not-found.
if (isTestEnv)
	backend.WithEnvironment("Geocoding__BaseUrl", "http://127.0.0.1:1");

var frontend = builder.AddViteApp("frontend", "../../../../frontend")
	.WithPnpm()
	.WithReference(backend)
	.WaitFor(backend)
	.WithEnvironment("VITE_API_URL", backend.GetEndpoint("http"))
	.WithEnvironment("VITE_KEYCLOAK_AUTHORITY_URL",
		ReferenceExpression.Create($"{keycloakEndpoint}/realms/einsatzbereit"))
	.WithEnvironment("STORAGE_PUBLIC_URL", ReferenceExpression.Create($"{minioApiEndpoint}"));

backend.WithEnvironment("Cors__Origins__0", frontend.GetEndpoint("http"));

builder.Build().Run();
