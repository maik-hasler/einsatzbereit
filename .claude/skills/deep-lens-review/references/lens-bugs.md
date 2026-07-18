# Lens: Bugs & correctness

Goal: find defects that produce wrong behavior for users or data — not
style, not design taste.

## Scope rule — one vertical slice

830 files cannot be bug-hunted in depth. Pick ONE feature slice
(Achievements, Engagements, Invitations, Notifications, Organizations,
Users, VolunteerOpportunities). Selection: user's choice if named,
otherwise the slice with the highest recent churn + fix density from
triage. State the chosen slice and the reason in "Scope & method".

## Method — trace the slice end to end

Read in this order, building a model of intended behavior as you go:

1. **Domain**: entity invariants, value objects, state transitions.
   Write down the invariants the code *implies* (e.g. "an engagement
   cannot be checked in twice").
2. **Application**: each command/query handler + validator of the slice.
   Compare validator rules against the domain invariants from step 1 —
   gaps between the two are prime bug territory.
3. **Infrastructure**: the slice's EF configuration and queries. Hunt:
   missing `Include` before navigation access, unbounded queries,
   tracking vs no-tracking misuse, transaction boundaries around
   multi-entity writes.
4. **Api**: the slice's endpoints. Hunt: authorization attribute present
   and correct for the operation's sensitivity, request→command mapping
   losses, status-code semantics, error responses leaking internals.
5. **Frontend**: pages/components consuming the slice via the generated
   client. Run `pnpm check` and `pnpm lint` first — compiler findings in
   the slice are free evidence. Then hunt: state updates after unmounted
   async calls, optimistic UI without rollback, error paths that swallow
   failures silently, de/en both handled.

## Cross-cutting checks for the slice

- **Time**: opportunities and engagements carry dates. Check timezone
  handling end to end (Postgres `timestamptz`? client `Date` parsing?
  "today" boundaries).
- **Concurrency**: double-submit on the slice's mutating endpoints
  (e.g. QR check-in twice, invitation accepted twice). What enforces
  idempotency — DB constraint, domain check, or nothing?
- **AuthZ object-level**: can user A act on user B's resource by ID
  swapping? Trace one mutating endpoint's ownership check explicitly.

## Verification bar

A bug finding must include a **repro narrative**: concrete input,
the code path taken (files:lines in order), and the wrong outcome.
"This could be a race" without the narrative is a Hypothesis at best.
Compiler/linter output counts as Confirmed.

## Traps

Validator gaps may be covered by DB constraints — check the EF
configuration before reporting a missing-validation bug. Frontend
"missing error handling" may be handled by a global boundary
(`ErrorBoundary.tsx`, toast bus) — trace the actual failure path.
