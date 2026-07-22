---
type: "decision-note"
title: "The autonomous routines and their guardrails"
description: "issue-triage, its persona-simulation fallback, and deep-lens-review - plus the boundaries the owner keeps for themselves."
tags:
  - autonomous
  - self-review
  - persona
  - wiki
  - deploy-verify
timestamp: 2026-07-18
---

# The autonomous routines and their guardrails

This repo runs unsupervised from Claude Code on the web. Three routines do the work, and each one earns that autonomy by having a hard line it never crosses. Learn the lines first; they are what keep an owner-less run from doing damage.

## issue-triage: the main loop

Five stages: **Survey -> Implement -> Validate -> Release -> Document**.

Two rules bend for nothing:

- **Never merge the PR.** Merging is the owner's call, no matter how green CI is or how clean the live verification looks. Direct pushes to `main` are blocked in the sandbox anyway, but the rule is broader than that restriction - even where merging were possible, it is still not this routine's decision.
- **Never use a closing keyword** (`Fixes`/`Closes`/`Resolves`, any tense, anywhere in the PR title, body, or commits). Use `Addresses #NNN` or `Relates to #NNN`. A closing keyword auto-closes the issue the instant the owner merges, turning issue closure into a side effect instead of the deliberate, separate act it must stay.

Survey decides what to pick, and mostly what to skip:

- Skip anything labelled `needs-decision` outright - do not triage, implement, or comment on it beyond what is already there.
- Skip anything an open PR already covers; note the relationship in a comment and move on. Several issues resolvable by one coherent change may be done together.
- Prefer an issue triaged actionable across several prior cycles but never picked up, over re-triaging the backlog forever and reaching for whatever is newest. Resisting the shiny-new pull is the point of that rule.
- If nothing is actionable (everything left is `needs-decision`, bot-managed like Renovate's Dependency Dashboard, or PR-covered), fall through to `persona-simulation` rather than stopping.

Validate and Release are the mandatory deploy-verify flow: cut the RC branch from the feature branch itself so live staging verification happens before the owner ever looks, and keep the PR open the whole time. Document leaves one concise comment per issue investigated (what was analyzed, whether it reproduced, what changed, how it was verified), and skips the comment when a recent one already says the same thing. Making no change and no commit is a valid outcome.

## persona-simulation: the empty-backlog fallback

Runs only when Survey finds nothing actionable. It never competes with real triage - if there is an issue to implement, do that instead.

- **Issues only, no code.** Never touch source, never open a branch or PR. That single constraint is what makes an otherwise-unsupervised run safe.
- **The 1.0 feature set is frozen:** calendar, opportunities, applications/engagements, achievements, profiles, organizations. Hunt for gaps inside those, not adjacent domains (no payments, no chat), even when a missing piece seems to obviously call for one.
- Drives live staging as **Organizer Olaf** (`olaf/olaf123`), **Volunteer Vera** (`vera/vera123`), and **Platform Admin** (`admin/admin123`, added to this skill 2026-07-22, after `AdministrationPage` shipped in #768 on 2026-07-20 - the admin persona was missing from this skill for a while despite being a role card in the human pre-launch testing event) with Playwright, at least one full pass per persona.
- **Clean up test data.** Anything created against the live database (a draft opportunity, an application) is deleted or withdrawn before the run ends - it runs about every five hours, indefinitely, and must leave nothing behind.
- Cap at roughly 3-5 findings by real impact on the persona's task. An empty-handed run is a common, fine result. Dedup against existing `label:persona-sim` issues before filing.

Filing routes the finding by how decided it is. A clear bug or broken flow gets `bug`/`user-story` plus `persona-sim`, and is fair game for the next cycle to implement unsupervised. A judgment call - subjective, more than one defensible approach, unclear 1.0 scope - gets `needs-decision` on top plus a bolded "Needs your decision before implementation" line, so Survey leaves it alone. When unsure which bucket, prefer `needs-decision`: a wrongly-deferred bug costs one extra cycle, a wrongly-automated judgment call costs a decision made without the owner.

## deep-lens-review: the on-demand audit

A separate routine, not part of the loop. Whole-repo, but exactly **one lens per run** - bugs, dead code, dead features, repo hygiene, docs drift, test gaps, CI health, security, or contributor accessibility. Report-only: it never pushes, opens PRs, or edits the repo. Hard cap of 10 findings, ranked by severity, each carrying executed proof. Side-findings outside the chosen lens go to a one-line parking lot, uninvestigated - chasing them is the skill's main failure mode.

The trap it names explicitly: this is not a substitute for the diff-scoped `self-review` skill. `self-review` is pre-PR and diff-scoped; `deep-lens-review` is routine and whole-repo. If the request is about a diff or a PR, this is the wrong routine.

## What the owner keeps

Three settled calls. Do not re-propose them.

- **Sweeping restructures are done by hand.** The routine's org app shell PR #738 was closed with "I will do this manually with PR #702" - the owner reclaimed the restructure rather than take the automated version.
- **The CI-gate optimization was deferred to ship 1.0.** PR #746 (replace `publish.yml`'s full re-test with a `require-green` preflight that proves the deployed tree is already green) is a sound idea, closed with "Currently it is more important to me, to ship a working 1.0 asap." Parked, not rejected - but do not re-raise it before 1.0.
- **Blast-radius refactors get escalated, never silently picked up.** The stale DDD refactor was raised as a `[Decision]` issue (#710), implemented only after the owner greenlit it, opened as PR #716 for review, and merged by the owner rather than auto-merged by the routine. That run also deferred an unsafe sub-part (an `EngagementConfirmed` domain-event handler that would crash on re-entrant transaction dispatch) rather than ship something that could drop data, and documented the gap instead.

## The labels in practice

`persona-sim` marks a finding that came from a code-free live persona run. `needs-decision` is created on first use by `persona-simulation` and is honored by Survey (which skips it), but it is currently unused across open issues: #710 shipped carrying only `enhancement` despite its body arguing it warranted the label, and open items surfaced from user testing (for example the draft-save feedback bug #708) carry ordinary `bug` labels. Treat `needs-decision` as a live mechanism you will rarely see actually applied yet, not a label to rely on spotting in the wild.

# Related

- [claude-check-setup](/decisions/claude-check-setup.md) - the loops depend on the check agents and self-review skill
- [wiki-maintenance](/process/wiki-maintenance.md) - the ingest/query/lint skills are part of the same autonomous tooling
- [pre-launch-testing-event](/project/pre-launch-testing-event.md) - persona-simulation automates, against the same personas, what the live event does with human guests
- [deploy-verify-flow](/process/deploy-verify-flow.md) - the loop's Release/Validate stage is the mandatory verify flow
- [project-vision](/project/project-vision.md) - these routines are the LLM-sandbox learning goal made concrete while shipping toward 1.0

# Citations

- `.claude/skills/issue-triage/SKILL.md` - the five-stage loop, the never-merge and no-closing-keyword rules, Survey's skip/prefer logic
- `.claude/skills/persona-simulation/SKILL.md` - the fallback trigger, issues-only rule, frozen feature set, cleanup, cap, and label routing
- `.claude/skills/deep-lens-review/SKILL.md` - one-lens-per-run, report-only, 10-finding cap, and the boundary against self-review
- #746 - CI-gate optimization deferred to ship 1.0 ("ship a working 1.0 asap"), closed unmerged
- #738 - org app shell restructure closed so the owner could do it manually via #702
- #710 - DDD refactor raised as a [Decision] issue, greenlit, then implemented into PR #716, merged by the owner (never auto-merged by the routine)
