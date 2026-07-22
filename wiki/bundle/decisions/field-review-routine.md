---
type: "decision-note"
title: "field-review: one lens-based routine replacing three"
description: "Why issue-triage, persona-simulation, and deep-lens-review were merged into field-review, what it does differently, and the guardrails the owner keeps for themselves."
tags:
  - autonomous
  - field-review
  - persona
  - accessibility
  - complexity
  - wiki
  - deploy-verify
timestamp: 2026-07-22
superseded: "decisions/autonomous-routines.md"
---

# field-review: one lens-based routine replacing three

This repo used to run three separate autonomous routines: `issue-triage`
(survey issues, implement one, open a PR), its `persona-simulation`
fallback (drive live staging as Vera/Olaf, file issues when the backlog was
empty), and `deep-lens-review` (an on-demand, one-whole-repo-lens-per-run
static audit). They were merged into a single skill,
`.claude/skills/field-review/`, on 2026-07-22.

## Why merge

The split cost more than it saved. `persona-simulation` and
`deep-lens-review` were both "one deep pass, evidenced findings, report
don't fix" tools with the same shape - they differed only in *where* they
looked (live site vs. static repo). Running them separately meant a live
observation and its root cause in source were two disconnected findings
instead of one; a live design-review session (2026-07-19) found this
directly - a visual anomaly on three different pages turned out to be one
`mx-auto max-w-2xl` wrapper reused across `OrganizationProfileView.tsx`,
`OrgMembersPage.tsx`, and `UserProfilePage.tsx`, and only grepping from the
live finding back into source tied it together. A tool that only ever does
one side of that would have filed three unrelated-looking issues instead
of one root-caused one.

`issue-triage`'s implement loop is the piece that did not survive the
merge, not folded in. It kept shipping fixes for things nobody had reviewed
end to end - `field-review` only ever looks and files; nothing in this
repo's Claude Code tooling currently implements from the backlog
unsupervised. A filed issue waits for a human, or a separately-invoked
session explicitly asked to work on it, same as any other issue.

## What field-review does

One lens per run - the same discipline `deep-lens-review` proved out:
shallow "review everything" passes produce noise, so incomplete coverage
per run is fine and depth is the point. Twelve lenses, triaged by
signal/impact scoring unless the user names one:

- The nine static lenses `deep-lens-review` already had: bugs & correctness,
  dead code, dead features, repo & filesystem hygiene, docs drift, test
  gaps, CI health & performance, security smells, contributor accessibility.
- Three new ones absorbing and extending `persona-simulation`'s live-staging
  work: **personas** (drives staging as Volunteer Vera, Organizer Olaf, and
  now also **Platform Admin** - `admin/admin123`, added the same day as this
  merge, since the admin-only `AdministrationPage` had no automated coverage
  at all before), **accessibility** (a live/keyboard/axe-core pass behind
  what CI's jsx-a11y and `AccessibilityTests.cs` already guarantee), and
  **design & content** (layout consistency, visual-language drift, sparse
  pages - the screenshot-driven method a live design-review session used to
  produce its findings, now a repeatable lens instead of a one-off).
- One genuinely new lens: **code & comment complexity** - structural
  hotspots (length, nesting, branch count) cross-referenced with what the
  comments sitting on that code are actually saying (a defensive comment is
  often marking real fragility worth removing, not just narrating it; a
  comment restating the line below it, or describing behavior long since
  changed, is noise worth cutting).

Output is GitHub issues only, capped at 5 per run, labelled `field-review`
plus `bug`/`user-story`, `needs-decision` added on top for anything that's
a judgment call rather than a clear fix - same labeling scheme
`persona-simulation` used, carried forward under the new skill's name.

## What the owner keeps

These three settled calls predate the merge and still hold - do not
re-propose them:

- **Sweeping restructures are done by hand.** The routine's org app shell PR #738 was closed with "I will do this manually with PR #702" - the owner reclaimed the restructure rather than take the automated version.
- **The CI-gate optimization was deferred to ship 1.0.** PR #746 (replace `publish.yml`'s full re-test with a `require-green` preflight that proves the deployed tree is already green) is a sound idea, closed with "Currently it is more important to me, to ship a working 1.0 asap." Parked, not rejected.
- **Blast-radius refactors get escalated, never silently picked up.** The stale DDD refactor was raised as a `[Decision]` issue (#710), implemented only after the owner greenlit it, opened as PR #716 for review, and merged by the owner rather than auto-merged by the routine.

# Related

- [claude-check-setup](/decisions/claude-check-setup.md) - field-review depends on the same check agents and self-review skill the old routines did
- [wiki-maintenance](/process/wiki-maintenance.md) - the ingest/query/lint skills are unaffected by this merge, part of the same autonomous tooling
- [pre-launch-testing-event](/project/pre-launch-testing-event.md) - field-review's personas lens automates, against the same personas (now including Platform Admin), what the live event does with human guests
- [project-vision](/project/project-vision.md) - these routines are the LLM-sandbox learning goal made concrete while shipping toward 1.0
- [autonomous-routines](/decisions/autonomous-routines.md) - the superseded three-routine design this page replaces; kept for history

# Citations

- `.claude/skills/field-review/SKILL.md` - the merged lens catalog, non-negotiables, triage/filing mechanics
- `.claude/skills/field-review/references/lens-personas.md`, `lens-accessibility.md`, `lens-design-content.md`, `lens-complexity.md` - the four lenses new to this merge
- AGENTS.md (root) - the subagent/MCP-tool-grant gotcha and the `reset-staging.yml` pointer added the same day, both learned from the live design-review session that motivated this merge
- #738, #746, #710 - the three settled owner calls, unchanged by this merge
