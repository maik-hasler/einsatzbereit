# Log

Chronological, newest-first record of changes to this bundle. Use ISO 8601
dates and a bold action-word prefix, e.g. `**Added**`, `**Updated**`,
`**Fixed**`, `**Superseded**`:

```
## YYYY-MM-DD

- **Added** - <one-line description of what changed and why>
```

## 2026-07-22 (2)

- **Superseded** - `decisions/autonomous-routines.md` -> `decisions/field-review-routine.md`: `issue-triage`, `persona-simulation`, and `deep-lens-review` merged into one skill, `.claude/skills/field-review/`, later the same day as the entry below. Twelve lenses (the old nine static ones plus personas, accessibility, design & content, and a new code/comment-complexity lens), one per run, issues-only output, no automated implement-from-backlog loop anymore. Updated cross-references in `pre-launch-testing-event.md`, `deploy-verify-flow.md`, `decisions/index.md`, `project/index.md`, `wiki/README.md`, `wiki/AGENTS.md`.

## 2026-07-22 (1)

- **Updated** - `decisions/autonomous-routines.md`: `persona-simulation` gained a third persona, Platform Admin (`admin/admin123`) exercising the new `AdministrationPage` (#768), alongside a same-day audit of `.claude/skills/` and root/`frontend` `AGENTS.md` that fixed stale component references (`OrganizationOverviewPage` -> the widget-based `app/OrgDashboardPage/`, a nonexistent `OrganizationEngagementsPage`/`OpportunityMap`) and added two root-`AGENTS.md` gotchas: MCP tool grants (incl. the `playwright` plugin) don't propagate to `Agent`-tool subagents, and live staging accumulates smoke-test debris from shared `vera`/`olaf`/`admin` accounts (`.github/workflows/reset-staging.yml` is the fix).

## 2026-07-18

- **Added** - first roundup: 17 concept pages harvested from the repo, `docs/`, `.claude/` config, git history, and the GitHub tracker, grouped under `process/`, `gotchas/`, `reference/`, `decisions/`, `ci/`. Every falsifiable claim was adversarially verified against the working tree and public issues/PRs; 8 refuted claims were corrected (Testcontainers -> Aspire/DCP, the OwnershipGuard path, the full Keycloak `passwordPolicy`, the organizations-are-both-Keycloak-and-DB correction, React Router v8, PR #716 merged not open, and two others). Graph is fully bidirectional; `backend-conventions` is the hub.
- **Updated** - rewrote `project-vision.md` and `pre-launch-testing-event.md` as synthesis rather than 1:1 copies of the loose notes, and moved both under `project/`. Wired them into the new graph (vision -> deploy-verify + autonomous-routines; testing-event -> persona-simulation via autonomous-routines + the a11y gap in frontend-conventions).
- **Added** - per-section `index.md` files and a rewritten root `index.md` routing table.
