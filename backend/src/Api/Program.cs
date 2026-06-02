using System.Diagnostics;
using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.ExceptionHandlers;
using Api.Common.Health;
using Api.Common.Middleware;
using Api.Common.RateLimiting;
using Application;
using Asp.Versioning;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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
		options.MapInboundClaims = false;
		options.Authority = builder.Configuration["Authentication:Authority"];
		options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateAudience = false,
			RoleClaimType = "roles",
			ValidIssuers = builder.Configuration
				.GetSection("Authentication:ValidIssuers").Get<string[]>(),
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
			.WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")));

builder.Services.AddHttpClient(KeycloakHealthCheck.HttpClientName, client =>
	client.Timeout = TimeSpan.FromSeconds(5));

builder.Services.AddHealthChecks()
	.AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
	.AddCheck<KeycloakHealthCheck>("keycloak", tags: ["ready"]);

builder.Services.AddEndpoints();
builder.Services.AddRateLimitingPolicies(builder.Configuration);

builder.Services.AddHttpLogging(logging =>
{
	logging.LoggingFields = HttpLoggingFields.RequestMethod
		| HttpLoggingFields.RequestPath
		| HttpLoggingFields.ResponseStatusCode
		| HttpLoggingFields.Duration;
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();

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
	await initializer.SeedAsync();

	app.MapOpenApi();
}
else if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
	var scope = app.Services.CreateScope();

	var initializer = scope.ServiceProvider.GetRequiredService<IApplicationDbContextInitializer>();

	await initializer.MigrateAsync();
}

app.MapDefaultEndpoints();

app.UseHttpLogging();

app.Use(async (context, next) =>
{
	context.Response.Headers["X-Content-Type-Options"] = "nosniff";
	context.Response.Headers["X-Frame-Options"] = "DENY";
	context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
	context.Response.Headers["X-Trace-Id"] =
		Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
	await next();
});

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseMiddleware<LoginStreakMiddleware>();

app.MapEndpoints();

await app.RunAsync();
