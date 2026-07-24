# Log

Chronological, newest-first record of changes to this bundle. Use ISO 8601
dates and a bold action-word prefix, e.g. `**Added**`, `**Updated**`,
`**Fixed**`, `**Superseded**`:

```
## YYYY-MM-DD

- **Added** - <one-line description of what changed and why>
```

## 2026-07-24

- **Added** - `decisions/scripts-folder-removed.md`: root `scripts/` (106 tracked files, re-verified against `origin/main` rather than the issue's now-stale 98) and root `package.json`/`package-lock.json` deleted per #791 discussion, expanded beyond the issue's 16 orphaned scripts to the whole persisted-script convention. Live-verification scripts are now scratch-only, never committed.
- **Updated** - `process/deploy-verify-flow.md` and `process/live-playwright-scripts.md`: smoke script section and the shared-helper page rewritten for the scratch-directory approach (no more `scripts/lib/live-browser.mjs`); `process/index.md` description updated to match. `gotchas/sandbox-limitations.md`'s Related line reworded ("browser helper" -> "launch args") to match.

## 2026-07-18

- **Added** - first roundup: 17 concept pages harvested from the repo, `docs/`, `.claude/` config, git history, and the GitHub tracker, grouped under `process/`, `gotchas/`, `reference/`, `decisions/`, `ci/`. Every falsifiable claim was adversarially verified against the working tree and public issues/PRs; 8 refuted claims were corrected (Testcontainers -> Aspire/DCP, the OwnershipGuard path, the full Keycloak `passwordPolicy`, the organizations-are-both-Keycloak-and-DB correction, React Router v8, PR #716 merged not open, and two others). Graph is fully bidirectional; `backend-conventions` is the hub.
- **Updated** - rewrote `project-vision.md` and `pre-launch-testing-event.md` as synthesis rather than 1:1 copies of the loose notes, and moved both under `project/`. Wired them into the new graph (vision -> deploy-verify + autonomous-routines; testing-event -> persona-simulation via autonomous-routines + the a11y gap in frontend-conventions).
- **Added** - per-section `index.md` files and a rewritten root `index.md` routing table.
