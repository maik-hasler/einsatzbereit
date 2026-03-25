using System.Security.Claims;
using Api.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/me", (ClaimsPrincipal user) => new { Name = user.FindFirstValue("preferred_username") })
    .RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy);

app.MapGet("/admin", () => new { Message = "Admin access granted" })
    .RequireAuthorization(AuthorizationPolicies.EinsatzbereitAdminPolicy);

await app.RunAsync();