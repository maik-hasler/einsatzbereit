var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
	.WithDataVolume()
	.WithPgAdmin();

var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "latest")
	.WithHttpEndpoint(port: 1080, targetPort: 8025, name: "webui", isProxied: false)
	.WithEndpoint(port: 1025, targetPort: 1025, name: "smtp", scheme: "tcp", isProxied: false);

var database = postgres.AddDatabase("einsatzbereit");

var keycloakRealmPath = Path.GetFullPath(
	Path.Combine(builder.AppHostDirectory, "..", "..", "..", "..", "keycloak", "realms"));

var keycloakThemePath = Path.GetFullPath(
	Path.Combine(builder.AppHostDirectory, "..", "..", "..", "..", "keycloak", "themes", "einsatzbereit"));

var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.6.1")
	.WithEnvironment("KC_DB", "dev-file")
	.WithBindMount(keycloakRealmPath, "/opt/keycloak/data/import", isReadOnly: true)
	.WithBindMount(keycloakThemePath, "/opt/keycloak/themes/einsatzbereit", isReadOnly: true)
	.WithArgs("start-dev", "--import-realm")
	.WithHttpEndpoint(port: 8080, targetPort: 8080, isProxied: false);

var keycloakEndpoint = keycloak.GetEndpoint("http");

var mailpitSmtpEndpoint = mailpit.GetEndpoint("smtp");

var backend = builder.AddProject<Projects.Api>("backend")
	.WithReference(database)
	.WaitFor(database)
	.WaitFor(keycloak)
	.WaitFor(mailpit)
	.WithEnvironment("Authentication__Authority",
		ReferenceExpression.Create($"{keycloakEndpoint}/realms/einsatzbereit"))
	.WithEnvironment("Authentication__ValidIssuers__0",
		ReferenceExpression.Create($"{keycloakEndpoint}/realms/einsatzbereit"))
	.WithEnvironment("Keycloak__BaseUrl",
		ReferenceExpression.Create($"{keycloakEndpoint}"))
	.WithEnvironment("Smtp__Host", mailpitSmtpEndpoint.Property(EndpointProperty.Host))
	.WithEnvironment("Smtp__Port", mailpitSmtpEndpoint.Property(EndpointProperty.Port));

var frontend = builder.AddViteApp("frontend", "../../../../frontend")
	.WithPnpm()
	.WithReference(backend)
	.WaitFor(backend)
	.WithEnvironment("VITE_API_URL", backend.GetEndpoint("http"))
	.WithEnvironment("VITE_KEYCLOAK_AUTHORITY_URL",
		ReferenceExpression.Create($"{keycloakEndpoint}/realms/einsatzbereit"));

backend.WithEnvironment("Cors__Origins__0", frontend.GetEndpoint("http"));

builder.Build().Run();
