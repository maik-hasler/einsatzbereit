var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var database = postgres.AddDatabase("einsatzbereit");

var keycloakRealmPath = Path.GetFullPath(
    Path.Combine(builder.AppHostDirectory, "..", "..", "..", "..", "keycloak", "realms"));

var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.6.1")
    .WithEnvironment("KC_DB", "dev-file")
    .WithBindMount(keycloakRealmPath, "/opt/keycloak/data/import", isReadOnly: true)
    .WithArgs("start-dev", "--import-realm");

var keycloakEndpoint = keycloak.GetEndpoint("http");

var backend = builder.AddProject<Projects.Api>("backend")
    .WithReference(database)
    .WaitFor(database)
    .WaitFor(keycloak)
    .WithEnvironment("Authentication__Authority",
        ReferenceExpression.Create($"{keycloakEndpoint}/realms/einsatzbereit"))
    .WithEnvironment("Authentication__ValidIssuers__0",
        ReferenceExpression.Create($"{keycloakEndpoint}/realms/einsatzbereit"))
    .WithEnvironment("Keycloak__BaseUrl",
        ReferenceExpression.Create($"{keycloakEndpoint}"))
    .WithEnvironment("Cors__Origins__0", "http://localhost:4321");

builder.AddViteApp("frontend", "../../../../frontend")
    .WithReference(backend)
    .WaitFor(backend);

builder.Build().Run();