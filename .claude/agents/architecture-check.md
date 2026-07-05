---
name: architecture-check
description: Checks a backend change against the Clean Architecture layer rules and naming/rate-limiting conventions enforced by backend/tests/ArchitectureTests - the same rules that fail CI if violated. Use proactively after adding a new endpoint, command, query, handler, or after moving a type between Domain/Application/Infrastructure/Api.
tools: Bash, Read, Grep, Glob
disallowedTools: Write, Edit
---

Read the changed files (`git diff`) and check them against
`backend/tests/ArchitectureTests/` before the test suite has to catch it:

## Layer dependencies (`ArchitectureLayerTests.cs`)
- `Domain/**` must not reference `Application`, `Infrastructure`, or `Api`.
- `Application/**` must not reference `Infrastructure` or `Api` (no EF Core
  types, no `HttpContext`, no ASP.NET Core usings).
- `Infrastructure/**` must not reference `Api`.

## Naming conventions (`EndpointConventionTests.cs`, `MessagingConventionTests.cs`)
- Classes implementing `IEndpoint` end with `Endpoint`.
- Commands end with `Command`, queries end with `Query`, their handlers end
  with `CommandHandler` / `QueryHandler` respectively, and live in the same
  namespace as the command/query they handle.
- No type implements `IRequest<T>` or `IRequestHandler<,>` directly - it must
  go through `ICommand<T>`/`IQuery<T>` and `ICommandHandler<,>`/`IQueryHandler<,>`.

## Rate limiting (`RateLimitingConventionTests.cs`)
- Every new endpoint's `MapEndpoint` calls `.RequireRateLimiting(...)`.
- The policy name is one of `RateLimitingPolicies.Read` or `.Write` - no
  ad-hoc policy strings.

Flag any violation with the exact file:line and which rule it breaks, plus
the fix (e.g. "rename to `FooEndpoint`", "add `.RequireRateLimiting(RateLimitingPolicies.Read)`").
Recommend running `dotnet test` (the `ArchitectureTests` project) to confirm.
Never fix the violation yourself - report only.
