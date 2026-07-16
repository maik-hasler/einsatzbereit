---
type: decision-note
title: Reviving a long-stale branch needs a deliberate decision, not a merge
description: Issue #710 asks whether to revive a 39-commits-stale branch reworking error handling, domain events, and value objects across most of the backend.
resource: https://github.com/maik-hasler/einsatzbereit/issues/710
tags: [backend, architecture, technical-debt]
timestamp: 2026-07-16
status: draft
---

# Schema

A large, long-stale branch that touches nearly every handler in a codebase isn't safe to fast-forward-merge just because it "looks done" - incomplete pieces (e.g. new events with no handlers yet), staleness against `main`, and downstream breaking changes (e.g. a regenerated API client with fields that go from required to nullable) all need to be weighed before reviving it, not discovered after merging.

# Examples

`feature/ddd-improvements` replaces thrown exceptions with Result-style value objects (railway-oriented programming) mapped to `ProblemDetails` HTTP codes 400/404/409/403, and replaces a no-op event dispatcher with real in-process pub-sub - raising 9 new domain events that currently have zero `INotificationHandler<T>` implementations. It also introduces validated factories for strongly-typed ids, promotes `Address` to a shared value object with geolocation fields, and splits `VolunteerOpportunity`'s catch-all update method into intention-revealing, Result-returning alternatives.

Issue #710 names the blocking concerns directly: a single 3,100+ line commit touching nearly every command handler, 39 commits behind `main`, incomplete event handlers, and breaking changes in the regenerated API client. It suggests rebasing onto `main` and regenerating the three auto-generated artifacts, documenting the Result/exception convention, implementing the missing event handlers (especially achievement/streak logic), auditing frontend compatibility, and running the full test suite before merging.

# Citations

- `#710` https://github.com/maik-hasler/einsatzbereit/issues/710
