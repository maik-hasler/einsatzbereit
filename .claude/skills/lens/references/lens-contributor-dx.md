# Lens: Contributor accessibility

Goal: how hard is it for an outside contributor to arrive, get running,
find work, and land a first PR. Method: simulate that journey against
the repo as it is - every friction point is a candidate finding.

## Method - walk the journey

1. **Arrival (first 5 minutes):** does the README say what this is, who
   it's for, and that contributions are welcome - above the fold?
   Is the project's state honest (active? pre-1.0? looking for help?)?
   License clear? German project name + English docs: is the language
   expectation for issues/PRs stated anywhere?
2. **Setup (first hour):** follow the Local Development section
   literally, as someone with none of the prerequisites. Count the
   installs (.NET 10 + Docker + pnpm), note undocumented assumptions
   (pnpm version? Docker resources? Windows/WSL viable?), missing
   troubleshooting for the predictable failures (ports taken, first
   Keycloak import slow). You cannot execute the stack here - audit the
   instructions' completeness and internal consistency instead, and
   mark run-only steps unverified.
3. **Finding work:** open issues via GitHub API - are there any labeled
   `good first issue`/`help wanted`? Are issue templates (bug, chore,
   user story) actually usable by outsiders or internal-jargon-laden?
   Is there a roadmap or project board reference?
4. **Making the change:** CONTRIBUTING - branch/commit conventions
   stated and consistent with what CI enforces (pr-title.yml)? Are the
   local quality gates listed with copy-paste commands (backend tests,
   pnpm check/lint, i18n check)? Does a contributor learn about the
   `.claude/` tooling and whether they're expected to use it?
5. **Review & landing:** response expectations, CI feedback a PR author
   sees (which checks, how fast - reuse triage timing data), whether a
   first-time contributor can tell what "done" means.
6. **Comprehension:** is there a human-oriented architecture overview
   (docs/Architecture) that a newcomer can read in 15 minutes, and does
   it match reality? (Deep quality/drift analysis belongs to the docs
   lens - here the question is existence and entry-level readability.)

## Verification bar

Findings are specific frictions with the step they block and the
smallest fix ("README line NN assumes X; one sentence fixes it") -
not "docs could be better". Where the journey depends on maintainer
behavior (response times), report observable signals (issue/PR ages
from the API) without psychoanalyzing the maintainer.

## Traps

This is a solo project - recommendations must fit a one-person budget.
Ten process files nobody maintains are worse than three honest ones;
prefer removing friction over adding ceremony.
