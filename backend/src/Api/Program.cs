using System.Diagnostics;
using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.ExceptionHandlers;
using Api.Common.Health;
using Api.Common.Middleware;
using Api.Common.Network;
using Api.Common.OutputCaching;
using Api.Common.RateLimiting;
using Application;
using Application.Common.Startup;
using Asp.Versioning;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

// OpenApiGenerateDocumentsOnBuild/NSwag regenerate the OpenAPI document on every
// build by launching this Main via Microsoft.Extensions.Hosting.HostFactoryResolver
// (dotnet-getdocument), which runs the whole app - including the config check below
// - for real, with no ASPNETCORE_ENVIRONMENT set. That design-time invocation is
// recognizable by the synthetic "--applicationName=" argument it passes, which a real
// run (dotnet run/dotnet Api.dll/the published container) never does.
var isDesignTimeToolInvocation = args.Any(a => a.StartsWith("--applicationName=", StringComparison.Ordinal));

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

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
			ValidateAudience = true,
			ValidAudience = builder.Configuration["Authentication:ValidAudience"],
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
	.AddCheck<KeycloakHealthCheck>("keycloak", tags: ["ready"])
	.AddCheck<StorageHealthCheck>("storage", tags: ["ready"]);

builder.Services.AddEndpoints();
builder.Services.AddRateLimitingPolicies(builder.Configuration);
builder.Services.AddOutputCachingPolicies(builder.Configuration);

// EnableForHttps is safe here: this API is a pure JSON/token (Bearer, not cookie) API,
// so there's no session secret reflected back into a compressible response body that a
// BREACH-style attack could exploit (#1391).
builder.Services.AddResponseCompression(options =>
{
	options.EnableForHttps = true;
	options.Providers.Add<BrotliCompressionProvider>();
	options.Providers.Add<GzipCompressionProvider>();
	options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Append("application/json");
});

var trustedNetworks = builder.Configuration.GetSection("TrustedNetworks").Get<TrustedNetworksOptions>()
	?? new TrustedNetworksOptions();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
	foreach (var cidr in trustedNetworks.Cidrs)
		options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr));
});

builder.Services.AddHttpLogging(logging =>
{
	logging.LoggingFields = HttpLoggingFields.RequestMethod
		| HttpLoggingFields.RequestPath
		| HttpLoggingFields.ResponseStatusCode
		| HttpLoggingFields.Duration;
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ResultFailureExceptionHandler>();
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
			Description = "API for the Einsatzbereit application"
		};

		// Inline the IFormFile binary schema instead of a $ref so NSwag emits
		// its FileParameter helper class in the generated clients.
		foreach (var pathItem in document.Paths.Values)
		{
			if (pathItem.Operations is null)
				continue;

			foreach (var operation in pathItem.Operations.Values)
			{
				if (operation.RequestBody?.Content is not { } content ||
					!content.TryGetValue("multipart/form-data", out var mediaType) ||
					mediaType.Schema?.Properties is not { } properties)
					continue;

				foreach (var propertyName in properties.Keys.ToList())
				{
					if (properties[propertyName] is OpenApiSchemaReference { Reference.Id: "IFormFile" })
						properties[propertyName] = new OpenApiSchema
						{
							Type = JsonSchemaType.String,
							Format = "binary"
						};
				}
			}
		}

		document.Components?.Schemas?.Remove("IFormFile");

		return Task.CompletedTask;
	});
});

var app = builder.Build();

// appsettings.json's ConnectionStrings/Keycloak:ClientSecret/Authentication:Authority
// defaults are dev-only fallbacks (see the comments in that file) that also ship in
// the production image - fail fast outside Development instead of silently running
// with a working Postgres superuser connection string or a plain-http authority if
// an environment's override is ever dropped or misspelled. Skipped for the
// design-time tool invocation (see isDesignTimeToolInvocation above), which never
// has any of this configured and never actually serves traffic either.
if (!isDesignTimeToolInvocation)
{
	var missingConfiguration = RequiredConfigurationValidator.FindMissing(
		app.Environment.IsDevelopment(),
		app.Configuration.GetConnectionString("einsatzbereit"),
		app.Configuration["Keycloak:ClientSecret"],
		app.Configuration["Authentication:Authority"],
		app.Configuration.GetSection("Cors:Origins").Get<string[]>());

	if (missingConfiguration.Count > 0)
		throw new InvalidOperationException(
			$"Missing required configuration outside Development: {string.Join(", ", missingConfiguration)}.");
}

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

	if (app.Configuration.GetValue<bool>("Database:SeedOnStartup"))
		await initializer.SeedAsync();
}

// Both anonymous and previously exempt from every rate limiting/caching policy -
// /health additionally ran a DB connect + an outbound Keycloak HTTP call on every
// single hit, so a trivial unauthenticated flood could exhaust the Npgsql pool and
// starve Keycloak (#1172). RequireRateLimiting caps the request rate itself;
// CacheOutput on /health also bounds how often the underlying dependency checks run
// at all, independent of how many distinct callers/IPs are behind a flood.
app.MapDefaultEndpoints(
	health => health
		.RequireRateLimiting(RateLimitingPolicies.Read)
		.CacheOutput(OutputCachingPolicies.HealthCheck),
	alive => alive.RequireRateLimiting(RateLimitingPolicies.Read));

// Must run before anything that reads Connection.RemoteIpAddress (HTTP logging,
// the rate limiter's anonymous partition key) - see TrustedNetworksOptions for why
// only these known networks are trusted to set X-Forwarded-For (#1332).
app.UseForwardedHeaders();

// Bridges RequestSizeLimitAttribute metadata (set per-endpoint via .WithMetadata()) to
// Kestrel's IHttpMaxRequestBodySizeFeature - see RequestSizeLimitMiddleware for why minimal
// API endpoints need this explicitly. Placed before anything that could read the request
// body; endpoint metadata is already resolved this early since UseRouting() is never called
// explicitly, so WebApplication runs routing at the very start of the pipeline (#1177).
app.UseMiddleware<RequestSizeLimitMiddleware>();

app.UseHttpLogging();
app.UseResponseCompression();

app.Use(async (context, next) =>
{
	context.Response.Headers["X-Content-Type-Options"] = "nosniff";
	context.Response.Headers["X-Frame-Options"] = "DENY";
	context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

	// Bearer tokens must never travel over a downgraded HTTP connection - skipped in
	// Development since local dev runs over plain HTTP (#1370).
	if (!app.Environment.IsDevelopment())
		context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

	context.Response.Headers["X-Trace-Id"] =
		Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
	await next();
});

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.Use(async (ctx, next) =>
{
	using (app.Logger.BeginScope(new Dictionary<string, object?>
	{
		["TraceId"] = Activity.Current?.TraceId.ToString() ?? ctx.TraceIdentifier,
		["UserId"] = ctx.User.FindFirst("sub")?.Value ?? "anonymous",
	}))
	{
		await next(ctx);
	}
});

app.UseMiddleware<LoginStreakMiddleware>();

// Placed after LoginStreakMiddleware, not before: a cache hit short-circuits the
// pipeline entirely (next() is never called), which would otherwise silently skip
// that day's login-streak update for every authenticated request that happens to
// land on a cached response (#1391).
app.UseOutputCache();

app.MapEndpoints();

await app.RunAsync();
