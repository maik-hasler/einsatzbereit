# Lens: CI health & performance

Goal: every workflow in `.github/workflows/` - do they gate the right
things (health), and do they waste time or minutes (performance). This
lens reads every workflow line, which is feasible and is the point;
size the job first with `ls .github/workflows | wc -l` and
`cat .github/workflows/*.yml | wc -l` rather than trusting a number
written here.

## Method - health

1. **Gate completeness:** enumerate the dimensions from the workflows
   themselves rather than from a list here - `frontend-checks.yml`
   alone carries `pnpm audit`, Vitest and a row of bespoke
   `frontend/scripts/check-*.js` gates, and there are also mutation
   tests, a Keycloak realm-import check and CodeQL. For each: which
   workflow enforces it, on which trigger? Build the matrix. Holes =
   things a PR can break silently. Cross-check against what runs
   *before* CI in `.claude/hooks/pre-stop-verify.sh`, since a gate
   mirrored there fails earlier and cheaper.
2. **Trigger & path-filter correctness:** do `paths:` filters match the
   actual layout? A filter like `backend/**` that misses the realm JSON
   lets relevant changes skip CI.
   Conversely: workflows running on changes they cannot be affected by.
3. **Cross-workflow duplication:** same steps re-implemented in several
   workflows (checkout+setup blocks are fine; duplicated *logic* like
   version derivation or build scripting is drift risk - publish.yml is
   by far the largest and the prime suspect; assess whether it should
   decompose into reusable workflows or composite actions).
4. **Failure semantics:** `continue-on-error` masking real failures,
   jobs whose failure blocks nothing, missing `concurrency` groups
   (superseded pushes still burning minutes).
5. **Action hygiene:** pinning strategy (tag vs SHA - consistency
   matters more than dogma; note what Renovate manages), deprecated
   actions/runners.

## Method - performance

6. **Actual timings, not guesses:** unauthenticated GitHub API -
   `GET /repos/maik-hasler/einsatzbereit/actions/runs?per_page=50` gives
   run durations and conclusions; per-run `/jobs` gives step timing for
   the slowest. Identify: slowest workflow, slowest recurring step,
   failure rate (flaky?).
7. **Caching:** NuGet, pnpm store, Docker layers - cached where it
   pays? Keyed correctly (lockfile-based), or keys that never hit?
8. **Wasted work:** full builds where path filters would skip, matrix
   entries nobody consumes, artifacts uploaded and never downloaded,
   scheduled runs on a repo with low activity.

## Verification bar

Health findings cite workflow file + line. Performance findings cite
measured durations from the API where available; a caching suggestion
without a measured or clearly reasoned cost is a Hypothesis. Estimate
saved minutes/month where you can - the user decides with numbers.

## Traps

publish.yml length is a smell, not a finding - the finding is the
concrete duplication/coupling inside it. Some redundancy is deliberate
defense in depth (lint locally + in CI). Free-tier minutes on public
repos are unlimited for standard runners - "wasted minutes" arguments
are about feedback latency, not cost.
