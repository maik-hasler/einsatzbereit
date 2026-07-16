---
type: ci-failure
title: ArchitectureTests enforces Clean Architecture layering, naming, and rate-limiting as a CI-blocking test project
description: A new endpoint, command, query, or moved type can build and work locally and still fail CI on layer-dependency, naming-convention, or missing-rate-limiting grounds.
tags: [ci, architecture, backend]
timestamp: 2026-07-16
---

# Schema

Structural conventions (layering, naming, cross-cutting requirements like rate limiting) don't show up as compile errors or obvious runtime bugs. A change that builds and works locally can still fail CI if a dedicated architecture-test project enforces those rules as executable tests - the rule set is otherwise only discoverable by reading the test project's source directly.

# Examples

`backend/tests/ArchitectureTests/ArchitectureLayerTests.cs` enforces that Domain must not reference Application/Infrastructure/Api, Application must not reference Infrastructure/Api (no EF Core types, no `HttpContext`, no ASP.NET Core usings), and Infrastructure must not reference Api. `EndpointConventionTests.cs` and `MessagingConventionTests.cs` enforce that `IEndpoint` implementers end in `Endpoint`; commands/queries end in `Command`/`Query` with handlers ending in `CommandHandler`/`QueryHandler` in the same namespace; and that no type implements `IRequest<T>`/`IRequestHandler<,>` directly - it must go through this codebase's own `ICommand<T>`/`IQuery<T>` and `ICommandHandler<,>`/`IQueryHandler<,>`. `RateLimitingConventionTests.cs` enforces that every `MapEndpoint` call includes `.RequireRateLimiting(...)` using only `RateLimitingPolicies.Read` or `.Write`, never an ad-hoc policy string.

`dotnet.yml`'s build-and-test job runs `dotnet run --project tests/ArchitectureTests --no-build` as one of its sequential test steps that must pass.

# Citations

- `.claude/agents/architecture-check.md`
- `backend/tests/ArchitectureTests/ArchitectureLayerTests.cs`
- `backend/tests/ArchitectureTests/EndpointConventionTests.cs`
- `backend/tests/ArchitectureTests/MessagingConventionTests.cs`
- `backend/tests/ArchitectureTests/RateLimitingConventionTests.cs`
