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
// Aspire + Playwright flow. Write a dev-only copy with three relaxations and import
// that. Production never runs this AppHost - it uses the baked image + committed realm.
//  - webOrigins: Aspire serves the frontend on a dynamic http://localhost:<port>
//    origin that no fixed webOrigins entry matches (Keycloak webOrigins are exact CORS
//    origins, not port wildcards), so the browser OIDC token exchange is CORS-blocked.
//    Allow all origins for the public frontend client.
//  - redirectUris / post.logout.redirect.uris: same dynamic-port problem as webOrigins
//    above. The committed realm only lists the production callback (#1190 removed the
//    "http://localhost:*" wildcard from the deployed realm to keep it out of
//    production) - add it back here, local dev only.
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
		{
			clientObject["webOrigins"] = new JsonArray("*");
			clientObject["redirectUris"] = new JsonArray("http://localhost:*");

			if (clientObject["attributes"] is JsonObject frontendAttributes)
				frontendAttributes["post.logout.redirect.uris"] = "http://localhost:*";
		}

		if (clientId == "backend")
			clientObject["secret"] = "backend-secret";
	}
}

localRealm["bruteForceProtected"] = false;

// FastSignInAsync (VisualTests/AuthHelper.cs) mints via the frontend-test
// client but seeds the token under the frontend client's storage key, so its
// refresh token is invalid for the client oidc-client-ts believes it holds.
// AuthHelper drops that refresh token rather than seed an unusable one, so a
// long-running test would otherwise hit the realm's default 300s access
// token lifetime with no way to renew - raise it well past this suite's
// longest test instead of racing individual test durations against it.
localRealm["accessTokenLifespan"] = 3600;

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
	.WithHttpEndpoint(port: 8080, targetPort: 8080, isProxied: false)
	// Without a health check, Aspire's WaitFor(keycloak) below only waits for the
	// container process to reach "Running" - not for --import-realm to actually
	// finish inside it. backend's own startup (Program.cs) races Keycloak's admin
	// API right after that with its Keycloak-dependent seed calls
	// (ApplicationDbContextInitializer.SeedOrg1Async/SeedOrg2Async), which fail if
	// the realm isn't servable yet - silently, since SeedAsync only logs the
	// exception rather than rethrowing. On a slower CI runner, realm import can
	// plausibly take longer than the gap between "container started" and
	// backend's first admin API call, losing this race deterministically
	// rather than intermittently. Wait for the same endpoint
	// AspireFixture.WaitForRealmReadyAsync already polls from the test side, so
	// backend is gated on real readiness, not just process liveness.
	.WithHttpHealthCheck("/realms/einsatzbereit/.well-known/openid-configuration");

var keycloakEndpoint = keycloak.GetEndpoint("http");

var mailpitSmtpEndpoint = mailpit.GetEndpoint("smtp");

// Defaults to the VisualTests-safe bump (see the WithEnvironment call below) -
// IntegrationTestFixture.cs passes "--RateLimiting:Read:AnonymousPermitLimit=60"
// to restore the real production default for its own dedicated rate-limit test.
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
	// VisualTests' AllowAnonymous endpoints (home page, opportunity detail,
	// Leaflet map tiles - none of which can carry a bearer token) otherwise
	// share the production default of 60 anonymous reads per 60s with no
	// queueing, across every concurrent test hitting the same shared backend.
	// A 429 there renders as an empty list, which looks like an unrelated
	// locator timeout in whatever test happened to exhaust the bucket.
	// IntegrationTests boots this same AppHost (IntegrationTestFixture.cs) and
	// overrides this back down to the real production default via the
	// RateLimiting:Read:AnonymousPermitLimit command-line arg - its
	// RateLimitingTests.cs deliberately exercises that default's 429 behavior,
	// which this VisualTests-only bump would otherwise silently defeat.
	.WithEnvironment("RateLimiting__Read__AnonymousPermitLimit", anonymousReadPermitLimit);

if (isTestEnv)
{
	// isTestEnv only reflects the AppHost's OWN environment (set via
	// DistributedApplicationTestingBuilder.CreateAsync<AppHost>(["--environment",
	// "Testing"]) in IntegrationTestFixture/AspireFixture) - it says nothing
	// about the backend project resource's own environment, which Aspire may or
	// may not inherit from the apphost process. Force it explicitly so the
	// Program.cs IsDevelopment() migrate/seed logic keeps behaving exactly as it
	// always has for these test runs, regardless of that inheritance question.
	backend.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

	// Integration/Visual tests create opportunities with placeholder addresses
	// ("Test Street", "Sample City", ...) that the real Nominatim API correctly
	// reports as not found - since #975 that's now a hard validation error, so
	// hitting the live public API here would both break those tests and violate
	// Nominatim's usage policy against automated/bulk querying from CI. Swap in
	// a fake IGeocodingService instead of pointing at an unreachable address -
	// no network call at all, so no dependence on HTTP client resilience/retry
	// timing either.
	backend.WithEnvironment("Geocoding__UseFakeService", "true");
}

// dev mode (not a production build) is deliberate here: Aspire's AddViteApp
// gives HMR for local iteration, and a separate prod-build path just for
// VisualTests would mean maintaining two different ways of running the same
// frontend. React.StrictMode's double-invocation of effects (main.tsx) is
// already accounted for rather than fought - useSharedOrgFetch dedupes
// concurrent requests per key, and every effect it and OrgDashboardPage fire
// twice under StrictMode is an idempotent GET.
var frontend = builder.AddViteApp("frontend", "../../../../frontend")
	.WithPnpm()
	.WithReference(backend)
	.WaitFor(backend)
	.WithEnvironment("VITE_API_URL", backend.GetEndpoint("http"))
	.WithEnvironment("VITE_KEYCLOAK_AUTHORITY_URL",
		ReferenceExpression.Create($"{keycloakEndpoint}/realms/einsatzbereit"))
	.WithEnvironment("STORAGE_PUBLIC_URL", ReferenceExpression.Create($"{minioApiEndpoint}"))
	// Toasts otherwise auto-dismiss after 5s (ToastContext.tsx) in every test
	// env too, forcing assertion windows to race that timer instead of just
	// waiting for render. 0 = never auto-dismiss for test builds; production
	// keeps the runtimeConfig default (5000) via the VITE_TOAST_LIFETIME_MS
	// fallback in runtimeConfig.ts.
	.WithEnvironment("VITE_TOAST_LIFETIME_MS", isTestEnv ? "0" : "5000");

backend.WithEnvironment("Cors__Origins__0", frontend.GetEndpoint("http"));

builder.Build().Run();
