# Gotchas

Traps and non-obvious constraints, each learned from fixing a real bug or hitting a real wall.

- [Authorization traps that only fire on a fresh deploy or import](auth-fresh-deploy-traps.md) - A new per-org auth table without a backfill locks out existing organizers, and plaintext realm passwords crash a fresh Keycloak import.
- [Deleting an opportunity orphans its engagements](opportunity-deletion-cascade.md) - Hard-deleting a VolunteerOpportunity only cancels its engagements; reads that assume a live opportunity silently drop or strand a volunteer's history.
- [Domain events are dispatched but currently go nowhere](domain-events-noop.md) - The dispatcher is a no-op that silently drops every domain event, and wiring a real handler hits a transaction-timing footgun.
- [NSwag-generated clients are hook-protected, never hand-edit](nswag-generated-clients.md) - Three generated files regenerate from the backend build and are blocked by a PreToolUse hook; change the API shape at the source instead.
- [What the Claude Code web sandbox cannot do](sandbox-limitations.md) - No reliable Docker, git proxy blocks main and tag pushes, and gh/mcp GitHub tools are unavailable - use WebFetch for the tracker.
