using System.Security.Claims;
using System.Threading.RateLimiting;
using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application;
using Asp.Versioning;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
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
			.AllowAnyMethod()));

builder.Services.AddEndpoints();

builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

	var readWindow = TimeSpan.FromSeconds(
		builder.Configuration.GetValue("RateLimiting:Read:WindowSeconds", 60));
	var readAuthLimit = builder.Configuration.GetValue("RateLimiting:Read:AuthenticatedPermitLimit", 200);
	var readAnonLimit = builder.Configuration.GetValue("RateLimiting:Read:AnonymousPermitLimit", 60);
	var writeWindow = TimeSpan.FromSeconds(
		builder.Configuration.GetValue("RateLimiting:Write:WindowSeconds", 60));
	var writeLimit = builder.Configuration.GetValue("RateLimiting:Write:PermitLimit", 100);

	options.AddPolicy(RateLimitingPolicies.Read, httpContext =>
	{
		if (httpContext.User.Identity?.IsAuthenticated == true)
		{
			var userId = httpContext.User.FindFirstValue("sub") ?? "unknown";
			return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = readAuthLimit,
				Window = readWindow,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = 0
			});
		}

		var clientIp = GetClientIp(httpContext);
		return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
		{
			PermitLimit = readAnonLimit,
			Window = readWindow,
			QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
			QueueLimit = 0
		});
	});

	options.AddPolicy(RateLimitingPolicies.Write, httpContext =>
	{
		var key = httpContext.User.FindFirstValue("sub") ?? GetClientIp(httpContext);
		return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
		{
			PermitLimit = writeLimit,
			Window = writeWindow,
			QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
			QueueLimit = 0
		});
	});

	static string GetClientIp(HttpContext ctx) =>
		ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
		?? ctx.Connection.RemoteIpAddress?.ToString()
		?? "unknown";
});

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

app.MapDefaultEndpoints();

app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapEndpoints();

await app.RunAsync();
