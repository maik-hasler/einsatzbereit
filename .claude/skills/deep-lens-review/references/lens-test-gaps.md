# Lens: Test gaps

Goal: where the safety net is thin relative to risk — not a coverage-%
fetish. The repo cannot run its .NET tests in this sandbox; this lens
maps and reads tests, it does not execute them.

## Method

1. **Inventory the four backend test projects.** For
   Application.UnitTests: enumerate tested handlers vs all handlers per
   feature — this is mechanically countable; produce the table. For
   IntegrationTests: which endpoints/flows are exercised? For
   ArchitectureTests: which rules exist, and do they encode the layering
   the docs claim? VisualTests: which pages, which viewports.
2. **Risk-rank the uncovered.** Cross the gap map with: mutating
   endpoints, money/data-integrity adjacent logic (check-in,
   invitations, achievements awarding), auth-sensitive paths, and the
   churn/fix-density hotspots from triage. An untested pure formatter is
   Low; an untested state transition on Engagements is High.
3. **Assertion quality sample:** read ~10 existing tests across
   projects. Hunt: tests without meaningful assertions, tests asserting
   mocks were called rather than outcomes, copy-paste tests that test
   the same thing, test names lying about content.
4. **Frontend reality check:** there is NO unit test runner — the gate
   is tsc + eslint + VisualTests. Do not dumbly report "frontend has no
   tests". Instead: check whether any policy documents this choice
   (CLAUDE.md, CONTRIBUTING); then identify the spots where the strategy
   is weakest — pure logic in `src/lib/` (e.g. date/format/status
   mapping) is cheap to test and invisible to visual tests. Recommend
   the 3–5 highest-value first tests, with the risk each one retires.
5. **Test infra friction:** can a contributor run each suite locally
   with documented commands? Missing docs here feed contributor-dx —
   parking lot, unless the command is broken.

## Verification bar

Gap claims come from the enumeration table (Confirmed). Assertion-
quality findings quote the test (`path:line`) and state the outcome it
fails to pin down. Recommendations name concrete first test cases, not
"add more tests".
