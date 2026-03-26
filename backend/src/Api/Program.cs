using System.Security.Claims;
using Api.Authentication;
using Api.Data;
using Api.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            RoleClaimType = "roles"
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.EinsatzbereitAdminPolicy, policy =>
        policy.RequireClaim(AuthorizationPolicies.RealmClaim, AuthorizationPolicies.EinsatzbereitRealm)
            .RequireRole(AuthorizationPolicies.AdminRole))
    .AddDefaultPolicy(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy, policy =>
        policy.RequireClaim(AuthorizationPolicies.RealmClaim, AuthorizationPolicies.EinsatzbereitRealm)
            .RequireRole(AuthorizationPolicies.DefaultUser));

builder.Services.AddDbContext<EinsatzbereitDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4321"])
            .AllowAnyHeader()
            .AllowAnyMethod()));


builder.Services.AddOpenApi("v1", options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new()
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
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<EinsatzbereitDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/me", (ClaimsPrincipal user) => new { Name = user.FindFirstValue("preferred_username") })
    .RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy);

app.MapGet("/admin", () => new { Message = "Admin access granted" })
    .RequireAuthorization(AuthorizationPolicies.EinsatzbereitAdminPolicy);

app.MapBedarfeEndpoints();

await app.RunAsync();
