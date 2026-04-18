# Backend — .NET 10 Clean Architecture API

## Layer Overview

```
Api → Application → Domain
          ↓
    Infrastructure → Domain
```

Enforced by `tests/ArchitectureTests/`. Breaking layer deps = failing CI.

## Directory Structure

```
src/
├── Api/                        Endpoints, auth config, program entry
│   ├── Program.cs              DI wiring, JWT auth, CORS, OpenAPI, migrations on startup
│   ├── Common/
│   │   ├── Authentication/AuthorizationPolicies.cs   Policy name constants + role constants
│   │   └── Endpoints/IEndpoint.cs + EndpointExtensions.cs   Auto-discovery pattern
│   ├── Organizations/          Feature folders: {Feature}/{UseCase}/v1/
│   └── VolunteerOpportunities/
│
├── Application/                Business logic only — no EF Core, no HTTP
│   ├── ServiceCollectionExtensions.cs   Reflection-based handler registration
│   ├── Common/
│   │   ├── Messaging/          ISender, ICommand<T>, IQuery<T>, IPipelineBehavior<T,R>
│   │   ├── Persistence/        IApplicationDbContext, IUnitOfWork, IAggregateRepository<T,TId>
│   │   ├── Keycloak/           IKeycloakOrganizationService
│   │   ├── Pagination/         PagedList<T>
│   │   └── PipelineBehaviors/  TransactionPipelineBehavior, PerformancePipelineBehavior
│   ├── Organizations/
│   └── VolunteerOpportunities/
│
├── Domain/                     Zero external dependencies
│   ├── Primitives/             AggregateRoot<TId>, Entity<TId>, DomainEvent, DomainException
│   ├── Common/                 Address (shared value object)
│   ├── Organizations/          Organization (aggregate), OrganizationId (value object)
│   ├── VolunteerOpportunities/ VolunteerOpportunity (aggregate), Address, Occurrence, ParticipationType
│   ├── Engagements/            Engagement (aggregate), EngagementStatus
│   └── Users/                  UserId (Keycloak user reference)
│
└── Infrastructure/             Implements Application interfaces
    ├── ServiceCollectionExtensions.cs   EF Core, Keycloak HTTP client, repositories
    ├── Persistence/
    │   ├── ApplicationDbContext.cs       EF Core DbContext + IUnitOfWork
    │   ├── Configurations/               Fluent API entity mappings
    │   ├── Interceptors/                 AuditableEntityInterceptor (created_on/modified_on)
    │   ├── Repositories/                 AggregateRepository<T,TId>, read repositories
    │   └── Migrations/                   EF Core migrations
    └── Keycloak/                         KeycloakOrganizationService (HttpClient wrapper)

tests/
├── Application.UnitTests/      Handler tests, NSubstitute mocks, no DB
├── IntegrationTests/           Testcontainers (Postgres 18 + Keycloak 26), Respawn
└── ArchitectureTests/          NetArchTest layer rules + naming conventions
```

## Adding a Feature (canonical pattern)

```
1. Domain (if domain logic involved)
   └── Domain/Organizations/Organization.cs  — add method

2. Application
   └── Application/Organizations/{UseCase}/v1/
       ├── {UseCase}Command.cs / {UseCase}Query.cs
       └── {UseCase}CommandHandler.cs / {UseCase}QueryHandler.cs

3. Api
   └── Api/Organizations/{UseCase}/v1/
       ├── {UseCase}Request.cs    — request body record (omit if no body)
       └── {UseCase}Endpoint.cs  — implements IEndpoint, maps route, calls ISender

4. OpenAPI regenerates automatically on dotnet build (NSwag in Api.csproj)
5. Frontend api-client.ts regenerates with it — do not hand-edit
```

Reference implementations (newest first): `Organizations/RemoveMember/`, `Organizations/GetOrganizationDetails/`, `Organizations/AddMember/`.

## Key Patterns

### IEndpoint auto-discovery
```csharp
// Any class implementing IEndpoint is auto-registered via EndpointExtensions.cs
public class MyEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/v{version:apiVersion}/...", handler)
           .RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
           .WithTags("TagName");
}
```

### CQRS dispatch
```csharp
// In endpoint handler:
var result = await sender.SendAsync(new MyCommand(...), cancellationToken);
```

### Handler registration
Auto-scanned from Application assembly — no manual DI registration needed.  
Add a class implementing `ICommandHandler<,>` or `IQueryHandler<,>` and it's picked up.

### Pipeline behaviors (run in this order)
1. `TransactionPipelineBehavior` — wraps commands in a DB transaction
2. `PerformancePipelineBehavior` — logs slow requests

### Authorization policies
| Policy constant | Role |
|---|---|
| `EinsatzbereitAdminPolicy` | `admin` |
| `EinsatzbereitDefaultUserPolicy` | `user` |
| `EinsatzbereitOrganisatorPolicy` | `organisator` |

## Organization domain model

`Organization` aggregate fields: `Id`, `Name`, `Description?`, `ContactEmail?`, `ContactPhone?`, `Website?`, `Address?` (`Domain.Common.Address`), `CreatedOn`, `ModifiedOn`.

`IKeycloakOrganizationService` methods: `CreateOrganizationAsync`, `AddMemberAsync`, `RemoveMemberAsync`, `AssignOrganizerRoleAsync`, `GetUserOrganizationsAsync`, `GetMembersAsync`.

## Implemented endpoints (Organizations)

| Method | Route | Auth | Handler |
|---|---|---|---|
| GET | `/v1/organizations` | DefaultUser | `GetOrganizations` |
| POST | `/v1/organizations` | DefaultUser | `CreateOrganization` |
| GET | `/v1/organizations/{id}` | Organisator | `GetOrganizationDetails` |
| PUT | `/v1/organizations/{id}` | Organisator | `UpdateOrganization` |
| POST | `/v1/organizations/{id}/members` | DefaultUser | `AddMember` |
| DELETE | `/v1/organizations/{id}/members/{userId}` | Organisator | `RemoveMember` |

## Database

- PostgreSQL 18, EF Core 9, `UseSnakeCaseNamingConvention()`
- Migrations in `Infrastructure/Persistence/Migrations/`
- Add migration: `dotnet ef migrations add <Name> -p src/Infrastructure -s src/Api`
- Apply migrations: runs automatically on startup in Development; `dotnet ef database update` otherwise
- `AuditableEntityInterceptor` auto-populates `created_on` / `modified_on`

## Testing

### Unit tests (`Application.UnitTests`)
- Mock with NSubstitute, assert with AwesomeAssertions
- Test handlers in isolation — no DB, no HTTP

### Integration tests (`IntegrationTests`)
- `IntegrationTestFixture` (IAsyncLifetime): spins up Postgres + Keycloak containers
- `Respawn` resets database state between tests (not between runs)
- `ApiClient.cs` is NSwag-generated — **do not hand-edit**
- Get auth tokens via `GetAccessTokenAsync(username, password)` in fixture

### Architecture tests (`ArchitectureTests`)
- `ArchitectureLayerTests.cs` — layer dependency rules
- `EndpointConventionTests.cs` — endpoint naming/structure rules
- `MessagingConventionTests.cs` — handler/command/query naming rules
- Run these if you rename namespaces or move files between layers

### Run all tests
```bash
dotnet test  # from backend/
```

## NuGet Packages (key ones)

All versions centrally managed in `Directory.Packages.props`.

| Package | Used in |
|---|---|
| `Asp.Versioning.Http` | Api — URL-segment versioning |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Api — JWT validation |
| `NSwag.MSBuild` | Api — generates OpenAPI spec + TS client on build |
| `EFCore.NamingConventions` | Infrastructure — snake_case |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | Infrastructure — Postgres provider |
| `Testcontainers.PostgreSql` + `.Keycloak` | IntegrationTests |
| `Respawn` | IntegrationTests — DB reset |
| `NetArchTest.Rules` | ArchitectureTests |
| `NSubstitute` | Application.UnitTests |
| `AwesomeAssertions` | All test projects |
