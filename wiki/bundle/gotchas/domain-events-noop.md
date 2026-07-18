---
type: "gotcha"
title: "Domain events are dispatched but currently go nowhere"
description: "The dispatcher is a no-op that silently drops every domain event, and wiring a real handler hits a transaction-timing footgun."
tags:
  - domain-events
  - clean-architecture
  - engagement
  - adr
timestamp: 2026-07-18
---

# The trap

Aggregates raise domain events with `AddEvent(...)` (for example `EngagementConfirmedDomainEvent` in `Domain/Engagements/`), and the plumbing looks complete: `DomainEventInterceptor` collects them and calls `IDomainEventDispatcher.DispatchAsync`, which loops and calls `IPublisher.Publish` on each. But `Publisher.Publish` resolves `INotificationHandler<T>` from the DI container and iterates the results, and no `INotificationHandler` is registered anywhere in `src`. The handler collection is always empty, so every raised event is silently dropped.

The dispatcher is a no-op in effect, not because its method body is empty but because nothing consumes the pipe. #710 phrases it as code that "builds the pipes but connects nothing." Grepping the entity layer for `AddEvent` calls will not reveal this - the events raise fine, they just have no destination. Do not assume raising an event triggers any side effect today.

# The second trap: transaction timing

Even once you decide to wire a handler, the dispatch point makes the obvious handler body wrong. `DomainEventInterceptor` fires from inside `DbContext.SaveChangesAsync`'s `SavedChangesAsync` callback: after the triggering command's DB write, but before `TransactionPipelineBehavior` commits the surrounding transaction.

That timing rules out two things a handler would naturally want to do:

- **Do not call `ISender.Send(...)`.** Dispatching a nested command puts `TransactionPipelineBehavior` on a `DbContext` that already has an open transaction; its `BeginTransactionAsync` call throws.
- **Do not write to the injected `IApplicationDbContext` expecting it to persist.** No further `SaveChangesAsync` runs before the outer transaction commits, so any entities you add or modify are silently dropped.

Wiring a handler that writes or sends therefore requires fixing the timing first: dispatch after the commit, or have the handler use a fresh scope and its own `DbContext`. Adding a naive write-or-send handler without addressing this produces changes that vanish or a runtime exception.

# The proper revival

Reviving domain events correctly is the parked `feature/ddd-improvements` branch tracked in #710: `Result` value objects, an in-process publisher, identity equality, and intention-revealing update methods. It touches roughly 185 files and was handed off as a decision for the repo owner rather than auto-implemented. Treat a domain-event handler request as blocked on that decision, not as a small wiring task.

# Related

- [backend-conventions](/reference/backend-conventions.md) - the transaction pipeline and Result pattern set the rules a handler must respect
- [adr-tdr-index](/reference/adr-tdr-index.md) - the DDD revival is tracked as a formal decision handoff
- [opportunity-deletion-cascade](/gotchas/opportunity-deletion-cascade.md) - both are engagement-lifecycle state changing away from the obvious call site

# Citations

- backend/AGENTS.md:126-138
- backend/src/Infrastructure/DomainEventDispatcher.cs
- #710
