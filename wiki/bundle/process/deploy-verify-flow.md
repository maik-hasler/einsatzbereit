---
type: "process"
title: Deploy-and-verify is mandatory for every fix
description: A fix is not done until it has been observed working on live staging, following a fixed ordered flow.
tags: [deploy-verify, release, staging, playwright, self-review, "1.0", oldenburg, sandbox]
timestamp: 2026-07-18
---

# Deploy-and-verify is mandatory for every fix

Every bug fix and every feature ends the same way: cut a release candidate,
deploy it to live staging, and watch the changed behaviour actually work there.
Root `AGENTS.md` states it plainly - a fix that has not been observed working in
production is not done. This is not a suggestion you can trade away when the
change looks trivial.

# Why it exists

No human reviews the code before the PR opens. The routine implements, self-reviews,
and ships on its own, so the only backstop against a broken change reaching users is
direct observation on a real deployment. The live smoke run is that backstop. Since
the sandbox cannot run Docker (no Aspire, no Testcontainers, no local Playwright
stack), there is no way to see the change run locally either - live staging is the
only place it can be observed at all. The Oldenburg-first priority is what makes this
non-negotiable: the point is a working tool for real people, not a green checkmark.

# The ordered flow

The steps run in this order, and the order matters (the RC branch must come off
the feature branch so the fix is actually in the build):

1. `/self-review` the diff and fix everything it flags before opening the PR.
2. Confirm all CI checks on the PR are green.
3. Pick the next RC version: list existing tags and increment the RC counter
   (`v1.0.0-rc.8` becomes `v1.0.0-rc.9`).
4. Create `release/vX.Y.Z-rc.N` **from the feature branch**, add an empty
   `release: vX.Y.Z-rc.N` commit, and push the branch. Do not push a tag - the
   git proxy blocks tag pushes; `release-rc.yml` creates the tag from the branch.
5. Let `publish.yml` build images and run `deploy-staging`. Monitor the check
   runs on the release commit.
6. After `deploy-staging` succeeds, smoke-test the live site.
7. Add the same assertions as a committed TUnit test in
   `backend/tests/VisualTests/`.
8. Write a **Live verification** section in the PR describing pass/fail and what
   was observed.
9. Only now is the task complete.

# The health gate and smoke script

Step 6 has two hard gates. `curl -sf https://api.maik-hasler.de/health` must
return HTTP 200, and a manual Playwright script in `scripts/` that exercises the
changed behaviour against `https://einsatzbereit.maik-hasler.de` must exit 0
with every assertion green. The script imports `scripts/lib/live-browser.mjs`
(`launchLiveBrowser()`, `loginKeycloak()`) rather than launching its own browser -
the helper carries the egress-proxy TLS workaround that plain `chromium.launch()`
lacks.

# Two artifacts, not one

The manual script and the TUnit test are both required because they do different
jobs. The `scripts/` Playwright script is throwaway proof that the change works
on live staging right now. The C# TUnit test in `backend/tests/VisualTests/` is
the reviewable, committed record that runs against the local Aspire stack in CI
on every future change. Skipping the TUnit test leaves nothing durable behind;
skipping the live script means the fix was never observed in production. One does
not substitute for the other.

# The trap: green does not mean done

The easy failure here is stopping too early. A PR with a passing self-review,
green CI, and a clean merge looks finished and is not - under this policy none of
that counts as verification. CI runs the suite, but "observed working on live
staging" is a separate, later step. Treat the merged-and-green state as the
midpoint of the flow, not the end of it.

# Related

- [release-workflow](/process/release-workflow.md) - steps 3-5 (RC version, release branch, deploy) are the release mechanics this flow drives
- [live-playwright-scripts](/process/live-playwright-scripts.md) - step 6's live smoke script must use the shared helper
- [sandbox-limitations](/gotchas/sandbox-limitations.md) - the reason local verification is impossible and live staging is the only option
- [project-vision](/project/project-vision.md) - the ship-for-Oldenburg-first priority is what makes 'not done until observed live' non-negotiable
- [claude-check-setup](/decisions/claude-check-setup.md) - step 1 is /self-review, which fans out to the check agents
- [field-review-routine](/decisions/field-review-routine.md) - `field-review` only ever files issues, never implements; whoever picks one up (human or a separately-invoked session) follows this same mandatory flow to close it out

# Citations

- AGENTS.md (root, "Mandatory: Deploy and verify every bug fix / feature")
- AGENTS.md:120
- AGENTS.md:136
- AGENTS.md:151
