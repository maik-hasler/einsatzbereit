using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application;
using Asp.Versioning;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices();

builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1);
        options.ReportApiVersions = true;
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'V";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
        // KC_HOSTNAME causes jwks_uri in the discovery document to use the external hostname
        // (e.g. localhost:8080), which is unreachable from inside Docker. Rewrite to the
        // internal Docker service name so JWKS can be fetched for token signature validation.
        options.BackchannelHttpHandler = new DockerRewriteHandler(
            from: builder.Configuration["Authentication:ExternalHost"],
            to: builder.Configuration["Authentication:InternalHost"]);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            RoleClaimType = "roles",
            // Accept tokens issued via the external hostname (browser) and internal hostname
            ValidIssuers = builder.Configuration.GetSection("Authentication:ValidIssuers").Get<string[]>(),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.EinsatzbereitAdminPolicy, policy =>
        policy.RequireClaim(AuthorizationPolicies.RealmClaim, AuthorizationPolicies.EinsatzbereitRealm)
            .RequireRole(AuthorizationPolicies.AdminRole))
    .AddDefaultPolicy(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy, policy =>
        policy.RequireClaim(AuthorizationPolicies.RealmClaim, AuthorizationPolicies.EinsatzbereitRealm)
            .RequireRole(AuthorizationPolicies.DefaultUser))
    .AddPolicy(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy, policy =>
        policy.RequireClaim(AuthorizationPolicies.RealmClaim, AuthorizationPolicies.EinsatzbereitRealm)
            .RequireRole(AuthorizationPolicies.OrganisatorRole));

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4321"])
            .AllowAnyHeader()
            .AllowAnyMethod()));

builder.Services.AddHealthChecks();

builder.Services.AddEndpoints();

builder.Services.AddOpenApi("v1", options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Einsatzbereit API",
            Version = "v1",
            Description = "API für die Einsatzbereit-Anwendung"
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var scope = app.Services.CreateScope();

    var initializer = scope.ServiceProvider.GetRequiredService<IApplicationDbContextInitializer>();
    
    await initializer.MigrateAsync();
    
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapEndpoints();

await app.RunAsync();

// Rewrites backchannel requests (OIDC metadata, JWKS) from an external hostname to an
// internal one. Necessary when KC_HOSTNAME differs from the Docker-internal service name.
internal sealed class DockerRewriteHandler(string? from, string? to) : HttpClientHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (from is not null && to is not null && request.RequestUri is not null)
        {
            var uri = request.RequestUri.ToString();
            if (uri.Contains(from, StringComparison.OrdinalIgnoreCase))
            {
                request.RequestUri = new Uri(uri.Replace(from, to, StringComparison.OrdinalIgnoreCase));
            }
        }
        return base.SendAsync(request, cancellationToken);
    }
}
