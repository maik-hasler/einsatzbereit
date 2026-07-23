# Backend - .NET 10 Clean Architecture API

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
├── Application/                Business logic only - no EF Core, no HTTP
│   ├── ServiceCollectionExtensions.cs   Reflection-based handler registration
│   ├── Common/
│   │   ├── Messaging/          ISender, ICommand<T>, IQuery<T>, IPipelineBehavior<T,R>, IPublisher, INotificationHandler<T>
│   │   ├── Persistence/        IApplicationDbContext, IUnitOfWork, IAggregateRepository<T,TId>
│   │   ├── Keycloak/           IKeycloakOrganizationService
│   │   ├── Pagination/         PagedList<T>
│   │   ├── Exceptions/         ResultFailureException, ResultExtensions (GetValueOrThrow/ThrowIfFailure)
│   │   └── PipelineBehaviors/  TransactionPipelineBehavior, PerformancePipelineBehavior
│   ├── Organizations/
│   └── VolunteerOpportunities/
│
├── Domain/                     Zero external dependencies
│   ├── Primitives/             AggregateRoot<TId>, Entity<TId>, DomainEvent, Result/Error/ErrorType, IValueObject, INotification
│   ├── Common/                 Address (shared value object, used by both Organization and VolunteerOpportunity)
│   ├── Organizations/          Organization (aggregate), OrganizationId (value object)
│   ├── VolunteerOpportunities/ VolunteerOpportunity (aggregate), Occurrence, ParticipationType, IPinGenerator
│   ├── Engagements/            Engagement (aggregate), EngagementStatus
│   └── Users/                  UserId (Keycloak user reference)
│
└── Infrastructure/             Implements Application interfaces
    ├── ServiceCollectionExtensions.cs   EF Core, Keycloak HTTP client, repositories
    ├── DomainEventDispatcher.cs         IDomainEventDispatcher -> IPublisher (see "Domain events" below)
    ├── Persistence/
    │   ├── ApplicationDbContext.cs       EF Core DbContext + IUnitOfWork
    │   ├── Configurations/               Fluent API entity mappings
    │   ├── Interceptors/                 AuditableEntityInterceptor (created_on/modified_on), DomainEventInterceptor
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
   └── Domain/Organizations/Organization.cs  - add method

2. Application
   └── Application/Organizations/{UseCase}/v1/
       ├── {UseCase}Command.cs / {UseCase}Query.cs
       └── {UseCase}CommandHandler.cs / {UseCase}QueryHandler.cs

3. Api
   └── Api/Organizations/{UseCase}/v1/
       ├── {UseCase}Request.cs    - request body record (omit if no body)
       └── {UseCase}Endpoint.cs  - implements IEndpoint, maps route, calls ISender

4. OpenAPI regenerates automatically on dotnet build (NSwag in Api.csproj)
5. Frontend api-client.ts regenerates with it - do not hand-edit
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
Auto-scanned from Application assembly - no manual DI registration needed.  
Add a class implementing `ICommandHandler<,>` or `IQueryHandler<,>` and it's picked up.

### Error handling (Result pattern)
Domain and Application logic signals failure with `Result`/`Result<T>` (`Domain/Primitives/Result.cs`), not exceptions. A domain method that can fail returns `Result` (or `Result<T>` when it also produces a value) built from `Error.Validation/NotFound/Conflict/Forbidden(code, description)`:
```csharp
public Result Confirm()
{
	if (Status != EngagementStatus.Pending)
		return Result.Failure(Error.Conflict("Engagement.NotPending", "Only pending engagements can be confirmed."));
	...
	return Result.Success();
}
```
Command/query handlers convert a `Result` to an exception at the Application boundary with `Application/Common/Exceptions/ResultExtensions.cs`:
- `result.ThrowIfFailure()` for a plain `Result`
- `result.GetValueOrThrow()` for a `Result<T>`, returning `T` on success

Both throw `ResultFailureException(Error)`, caught by `Api/Common/ExceptionHandlers/ResultFailureExceptionHandler.cs` and mapped to a `ProblemDetails` response (`Validation`->400, `NotFound`->404, `Conflict`->409, `Forbidden`->403) with `errorCode` + `traceId`. Only use this Application-boundary throw; don't invent a second convention for new endpoints, and don't thread `Result` all the way to the endpoint layer.

Reserve a raw `throw` (not wrapped in a `Result`) for truly exceptional/programmer-error cases that aren't part of a use case's expected failure modes.

### Domain events
Aggregates raise events via `AddEvent(...)` (see `EngagementConfirmedDomainEvent` etc. in `Domain/Engagements/`). `Infrastructure/Persistence/Interceptors/DomainEventInterceptor.cs` collects an aggregate's events and calls `IDomainEventDispatcher.DispatchAsync` from inside `DbContext.SaveChangesAsync`'s `SavedChangesAsync` callback - i.e. **after the DB write for the triggering command but before `TransactionPipelineBehavior` commits the transaction**.

This timing means an `INotificationHandler<T>` for a domain event **cannot safely**:
- call `ISender.Send(...)` to dispatch a nested command - `TransactionPipelineBehavior` would call `BeginTransactionAsync` again on a `DbContext` that already has an open transaction, and EF Core throws.
- add/modify entities on the injected `IApplicationDbContext` expecting them to persist - no further `SaveChangesAsync` call happens before the outer transaction commits, so those changes are silently dropped.

As of this writing no domain event has a registered `INotificationHandler` - the pipe (`IPublisher`/`Publisher`) exists but nothing consumes it yet (see #710). Wiring one up requires first resolving the timing issue above (e.g. dispatching after commit, or handlers writing through a fresh scope/DbContext) - don't add a handler that writes to the database or calls `ISender` without addressing it first.

### Pipeline behaviors (run in this order)
1. `TransactionPipelineBehavior` - wraps commands in a DB transaction
2. `PerformancePipelineBehavior` - logs slow requests

### Authorization policies
| Policy constant | Role |
|---|---|
| `EinsatzbereitAdminPolicy` | `admin` |
| `EinsatzbereitDefaultUserPolicy` | `user` |
| `EinsatzbereitOrganisatorPolicy` | `organisator` |

## Organization domain model

`Organization` aggregate fields: `Id`, `Name`, `Description?`, `ContactEmail?`, `ContactPhone?`, `Website?`, `Address?` (`Domain.Common.Address`), `CreatedOn`, `ModifiedOn`.

`IKeycloakOrganizationService` methods: `CreateOrganizationAsync`, `AddMemberAsync`, `RemoveMemberAsync`, `AssignOrganizerRoleAsync`, `GetMembersAsync`, `SearchUsersAsync`, `DeleteOrganizationAsync`. Which organizations a user organizes is answered from the local `organization_membership` table (`IApplicationDbContext.GetOrganizerOrganizationsAsync`), not Keycloak.

## Implemented endpoints (Organizations)

| Method | Route | Auth | Handler |
|---|---|---|---|
| GET | `/v1/organizations` | DefaultUser | `GetOrganizations` |
| GET | `/v1/organizations/directory` | Anonymous | `GetPublicOrganizations` |
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
- Test handlers in isolation - no DB, no HTTP

### Integration tests (`IntegrationTests`)
- `IntegrationTestFixture` (IAsyncLifetime): spins up Postgres + Keycloak containers
- `Respawn` resets database state between tests (not between runs)
- `ApiClient.cs` is NSwag-generated - **do not hand-edit**
- Get auth tokens via `GetAccessTokenAsync(username, password)` in fixture

### Architecture tests (`ArchitectureTests`)
- `ArchitectureLayerTests.cs` - layer dependency rules
- `EndpointConventionTests.cs` - endpoint naming/structure rules
- `MessagingConventionTests.cs` - handler/command/query naming rules
- Run these if you rename namespaces or move files between layers

### Run all tests
```bash
dotnet test  # from backend/
```

## NuGet Packages (key ones)

All versions centrally managed in `Directory.Packages.props`.

| Package | Used in |
|---|---|
| `Asp.Versioning.Http` | Api - URL-segment versioning |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Api - JWT validation |
| `NSwag.MSBuild` | Api - generates OpenAPI spec + TS client on build |
| `EFCore.NamingConventions` | Infrastructure - snake_case |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | Infrastructure - Postgres provider |
| `Testcontainers.PostgreSql` + `.Keycloak` | IntegrationTests |
| `Respawn` | IntegrationTests - DB reset |
| `NetArchTest.Rules` | ArchitectureTests |
| `NSubstitute` | Application.UnitTests |
| `AwesomeAssertions` | All test projects |
