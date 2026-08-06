---
type: "decision-note"
title: "Frontend component-level tests are not adopted - VisualTests covers that layer instead"
description: "Vitest stays scoped to src/lib/ pure functions; component/page rendering and interaction behavior is tested exclusively through the Playwright suite in backend/tests/VisualTests/."
tags:
  - testing
  - frontend
  - vitest
  - playwright
  - visualtests
timestamp: 2026-08-06
---

# What was decided

Component- and page-level frontend behavior is not tested with Vitest plus a
DOM-rendering library. `frontend/package.json` has no `@testing-library/react`
dependency, no `*.test.tsx` file exists anywhere in `src/`, and
`vitest.config.ts`'s coverage `include` is hard-scoped to `src/lib/**/*.ts` -
none of this is missing tooling, it is the intended boundary.
`src/lib/*.test.ts` keeps covering pure logic, colocated next to the module it
tests. Coverage of how a component or page actually renders and behaves is
carried entirely by the Playwright suite in `backend/tests/VisualTests/`.

# Why

Issue #1682, a 1.0-readiness test-coverage audit, flagged that commit
`c3f9ada1` shipped roughly 528 lines of stateful selection and
partial-failure-rendering logic in `EngagementManagementPage.tsx` (bulk
confirm/cancel, #1044) with no test at any level, and asked to "decide
explicitly whether component-level frontend tests are wanted, and record the
decision." The decision already existed implicitly - `frontend/AGENTS.md`'s
Unit Tests section states "Component/page-level behavior is covered by the
Playwright suite in `backend/tests/VisualTests/` instead - see root
`AGENTS.md`" - but had never been written down as a deliberate trade-off, so
a contributor who only notices the absence of `@testing-library/react` could
read it as an oversight rather than a choice.

The trade-off, made explicit here: a Vitest-plus-Testing-Library component
test would give a faster, more isolated feedback loop for pure
rendering/interaction logic than an E2E test ever can. This repo chooses one
E2E suite (Playwright driving a real browser against the full Aspire stack,
already around 50 test classes) as the single source of UI-behavior coverage
instead of splitting it across two frameworks with two different
rendering/mocking models. The issue names the resulting cost directly: "a
change to a component's rendering logic has no fast local signal" -
VisualTests is CI-only, it needs Aspire/DCP container orchestration a web/
cloud Claude Code session cannot provide (see sandbox-limitations), so a
regression surfaces on the PR's CI run rather than while the change is being
written.

# What this does not change

`src/lib/*.test.ts` continues testing pure functions with Vitest; that
boundary is unaffected. `AccessibilityTests.cs` in VisualTests still needs a
matching case for any new page, unrelated to this decision and already
enforced by the `a11y-check` agent. This does not rule out revisiting the
split later if VisualTests' runtime becomes a bigger bottleneck than it is
now - `backend/AGENTS.md` already calls it out as the "Largest and slowest
suite (~50 test classes)" among the four backend test projects.

# Related

- [frontend-conventions](/reference/frontend-conventions.md) - the frontend
  stack and directory layout this testing boundary applies to
- [backend-conventions](/reference/backend-conventions.md) - VisualTests'
  place among the four TUnit test projects
- [sandbox-limitations](/gotchas/sandbox-limitations.md) - why VisualTests
  never gives the fast local signal a component test would

# Citations

- #1682
- frontend/AGENTS.md ("Unit Tests" section)
- frontend/vitest.config.ts
- frontend/package.json
- backend/AGENTS.md ("Visual tests" section)
- frontend/src/pages/EngagementManagementPage.tsx@c3f9ada1
