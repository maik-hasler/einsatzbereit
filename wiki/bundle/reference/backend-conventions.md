---
type: "reference"
title: "Backend conventions the architecture tests enforce"
description: "Clean Architecture layers, the Result-at-the-boundary rule, auto-discovery naming, per-endpoint rate limiting, and the test stack - most of it CI-enforced by NetArchTest."
tags:
  - clean-architecture
  - rate-limiting
  - authorization
  - ci
  - nswag
  - adr
  - sandbox
timestamp: 2026-07-18
---

# Backend conventions the architecture tests enforce

Most of what follows is not style advice. It is checked by the `ArchitectureTests` project and fails CI when broken. Treat this page as the list of things that turn a green build red.

## Layer directions are compiled-in, not documented

Clean Architecture here means exactly two allowed dependency directions: `Api -> Application -> Domain`, and `Infrastructure -> Domain`. `ArchitectureLayerTests.cs` encodes this as six `NetArchTest` assertions, each phrased as a "should not depend on" rule:

- Domain must not reference Application, Infrastructure, or the presentation (Api) layer - it has zero external dependencies.
- Application must not reference Infrastructure or Api - so no EF Core, no `HttpClient`, no ASP.NET types leak into business logic.
- Infrastructure must not reference Api.

The practical trap: Application talks to persistence only through interfaces it owns (`IApplicationDbContext`, `IUnitOfWork`, `IAggregateRepository<T,TKey>`), and Infrastructure implements them. Reach for `DbContext` or an HTTP type inside a handler and the Application-layer test goes red, not just a reviewer's eyebrow. Run `ArchitectureTests` after any namespace rename or file move between layers.

## Result at the Application boundary, exceptions past it

Domain and Application logic signal expected failure with `Result` / `Result<T>`, built from `Error.Validation`, `Error.NotFound`, `Error.Conflict`, or `Error.Forbidden(code, description)`. A `Result` never reaches the endpoint. It is converted to an exception exactly once, at the Application boundary, using `ResultExtensions`:

- `result.ThrowIfFailure()` for a plain `Result`.
- `result.GetValueOrThrow()` for a `Result<T>`, returning `T` on success.

Both throw `ResultFailureException(Error)`, which `ResultFailureExceptionHandler` maps to `ProblemDetails` (`Validation` -> 400, `NotFound` -> 404, `Conflict` -> 409, `Forbidden` -> 403) with `errorCode` and `traceId`. Do not thread `Result` all the way to the endpoint, and do not invent a second error-mapping convention for a new endpoint - there is one. Reserve a raw `throw` for genuine programmer errors that are not a use case's expected failure mode.

## No manual DI: naming is the wiring

Nothing in this layer is hand-registered in a DI container. Auto-discovery does it, so the naming rules are load-bearing, and `MessagingConventionTests` enforces them:

- Any class implementing `IEndpoint` is found and mapped automatically. Add the class; do not register it.
- Command/query handlers are reflection-scanned from the Application assembly. A command type must end in `Command`, a query in `Query`, and their handlers in `CommandHandler` / `QueryHandler`.
- A handler must live in the **same namespace** as the command or query it handles, or the pairing test fails.
- Implement `ICommand<T>` / `IQuery<T>` and `ICommandHandler<,>` / `IQueryHandler<,>` - never the raw `IRequest<>` / `IRequestHandler<,>`. Two dedicated tests reject direct `IRequest` implementations.

Feature folders are `{Layer}/{Domain}/{Feature}/v1/`. Commands, queries, and DTOs are C# records. Routes use `/v{version:apiVersion}/...`.

## Every endpoint must opt into exactly one rate-limit policy

This rule is enforced by `RateLimitingConventionTests` but is not written down in `AGENTS.md`, so it is easy to miss. Two tests apply:

- Every `RouteEndpoint` must carry a rate-limiting policy - i.e. every `IEndpoint` must call `.RequireRateLimiting(...)`. A missing call fails `AllEndpoints_ShouldHaveRateLimitingPolicyApplied`.
- The policy name must be exactly `RateLimitingPolicies.Read` (`"rate-limit-read"`) or `RateLimitingPolicies.Write` (`"rate-limit-write"`). Anything else fails `AllEndpoints_ShouldUseOnlyKnownRateLimitingPolicies`.

Behavior from `RateLimitingExtensions.cs` plus the `RateLimitingOptions` defaults:

- **Read**: authenticated requests partition by the `sub` claim at 200 permits / 60s; anonymous requests partition by client IP (`X-Forwarded-For` first hop, else `RemoteIpAddress`) at 60 permits / 60s.
- **Write**: partitions by `sub` when present, otherwise client IP, at 100 permits / 60s.
- All partitions use `QueueLimit = 0`, so an over-limit request is rejected immediately with HTTP 429 rather than queued.

Local and test runs are effectively unthrottled: the Aspire `AppHost` sets `RateLimiting__Write__PermitLimit=10000` and `RateLimiting__Read__AuthenticatedPermitLimit=10000` as environment overrides, so you will not hit limits driving the app by hand. The 200/60/100 defaults come from `appsettings.json` and apply in staging/production.

## Test stack

All four test projects use TUnit with AwesomeAssertions:

- `Application.UnitTests` - handlers in isolation, NSubstitute mocks, no DB, no HTTP.
- `ArchitectureTests` - the `NetArchTest` layer, endpoint, messaging, and rate-limiting convention checks above.
- `IntegrationTests` - boots the entire Aspire application via `DistributedApplicationTestingBuilder.CreateAsync<AppHost>()`, waits for the `keycloak` and `backend` resources to reach `Running`, then uses Respawn to reset `public` (ignoring `__EFMigrationsHistory`) between tests. This needs real DCP orchestration for Postgres 18 and Keycloak 26, which the web sandbox cannot provide - run it in CI on a real runner, not locally in a cloud session. `VisualTests` (Aspire + Playwright) has the same requirement.

Respawn resets state between tests within a shared session, not between runs. The fixture also revokes the realm-level `organisator` role from non-baseline users between tests, because creating an organization grants that global role and it survives an organization-only reset - a leak that would otherwise break later tests assuming, say, that `vera` is not an organizer.

## Async all the way

Never `.Result` or `.Wait()`. Await instead.

# Related

- [sandbox-limitations](/gotchas/sandbox-limitations.md) - IntegrationTests/VisualTests need DCP orchestration the sandbox cannot provide
- [ci-traps](/ci/ci-traps.md) - the ArchitectureTests convention checks are a frequent CI failure
- [nswag-generated-clients](/gotchas/nswag-generated-clients.md) - changing the API shape here is what regenerates the clients
- [ef-migrations](/process/ef-migrations.md) - the persistence layer and its migration workflow sit under these layer rules
- [adr-tdr-index](/reference/adr-tdr-index.md) - rate limiting is TDR-1 and the layering choices trace to the ADRs
- [keycloak-realm-config](/reference/keycloak-realm-config.md) - auth policies depend on the realm claim and backend audience in the token
- [domain-events-noop](/gotchas/domain-events-noop.md) - the transaction pipeline behavior governs domain-event handler timing
- [frontend-component-tests-not-adopted](/decisions/frontend-component-tests-not-adopted.md) - the frontend's mirror-image decision: VisualTests, not a frontend unit-test layer, covers component/page behavior

# Citations

- backend/AGENTS.md
- backend/tests/ArchitectureTests/ArchitectureLayerTests.cs
- backend/tests/ArchitectureTests/RateLimitingConventionTests.cs
- backend/src/Api/Common/RateLimiting/RateLimitingExtensions.cs
- backend/tests/IntegrationTests/IntegrationTestFixture.cs
