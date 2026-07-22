---
name: persona-simulation
description: >
  Simulates the app's real personas (Volunteer Vera, Organizer Olaf, Platform
  Admin) against live staging to surface friction, bugs, and small gaps in
  the existing 1.0 feature set - for when issue-triage's Survey step finds no
  actionable open issue. Never invents new feature areas and never touches
  code; it only files GitHub issues, flagging anything that needs the repo
  owner's own product call so the automated triage loop won't implement it
  unsupervised. Use as the fallback of the recurring issue-triage routine
  when there is nothing left to triage, or when asked to "simulate persona
  usage", "audit the app as a user", or "find gaps for 1.0".
---

# Persona simulation

## When this runs

Only as the fallback of `issue-triage`'s Survey step: after confirming every
open issue is either not actionable, already covered by an open PR, or
bot-managed (e.g. Renovate's Dependency Dashboard). This skill never competes
with real triage work - if there's an issue to implement, do that instead.

## Ground rules

- **Never touch code, never open a branch or PR.** Output is GitHub issues
  (and comments) only - the one thing that keeps an otherwise-unsupervised
  run safe.
- **No new feature areas.** The 1.0 feature set is intentionally frozen -
  calendar, opportunities, applications/engagements, achievements, profiles,
  organizations, platform administration. Look for gaps *within* those, not
  adjacent domains (no payments, chat, etc.), even if a gap seems to
  "obviously" need one.
- **Clean up after yourself.** Anything created or changed against the live
  database - a draft opportunity, an application, an edited profile field -
  must be deleted, withdrawn, or reverted before the run ends. This runs
  indefinitely on a schedule - it must never leave test data or mutated
  demo-account state behind. If a run ever ends early (crash, timeout) before
  cleanup finishes, say so explicitly in the report rather than going silent
  - `.github/workflows/reset-staging.yml` (manual, destructive, wipes and
  reseeds all of staging) is the owner's backup for exactly this, but it's
  not this skill's to trigger.
- **Cap findings at ~3-5 per run**, prioritized by real impact on the
  persona's task, not nitpicks. An empty-handed run ("exercised all three
  personas, nothing new to report") is a fine, common outcome.
- **Dedup first.** `search_issues` for `label:persona-sim` plus keywords
  from the candidate finding before filing anything - don't re-report the
  same friction every cycle just because it's still there.

## Personas

Use the real seeded test accounts (root `AGENTS.md`, "Development Setup").
Run at least one full pass per persona per invocation - don't cherry-pick a
single screen. Component/page names below are a pointer, not a contract -
this app ships fast; if a name has moved, trust `frontend/src/App.tsx`'s
routes over this file and note the drift in the report.

**Organizer Olaf** (`olaf/olaf123`) - runs an org day to day:
- Opens the org dashboard (`app/OrgDashboardPage/`, the org app shell's
  landing tab) and checks whatever widgets are placed there - calendar,
  to-do, upcoming opportunities - for what needs attention.
- Creates a new opportunity through the full wizard
  (`CreateVolunteerOpportunityModal`).
- Reviews applications/engagements and accepts, waitlists, or rejects one
  (from the org Opportunities tab's "Manage applications" link into
  `EngagementManagementPage`).
- Skims the org profile (`OrganizationProfilePage`) for how it presents to
  volunteers.

**Volunteer Vera** (`vera/vera123`) - looks for and works opportunities:
- Browses/filters the opportunity list (`HomePage`,
  `VolunteerOpportunitiesList`) with a couple of realistic filters (location
  + radius, category, date range).
- Opens a detail page and submits an application
  (`VolunteerOpportunityDetailPage`).
- Checks when the opportunity she applied to actually starts, via her
  engagement list.
- Looks at her achievements (`UserAchievementsPage`, `BadgeGrid`,
  `ShareAchievementsModal`) and profile (`ProfileOverviewPage`,
  `UserProfilePage`).

**Platform Admin** (`admin/admin123`) - moderates the platform, not a single
org: opens `AdministrationPage` and works through what it offers - verifying
or unverifying an organization, checking the user list, toggling a user's
admin/enabled status if a realistic candidate exists. This persona was
missing from this skill for a long time despite being one of the role cards
in the human pre-launch testing event
(`wiki/bundle/project/pre-launch-testing-event.md`) - an admin-only screen
that nobody, human or automated, ever opened is exactly the kind of gap this
skill exists to catch, and it did previously slip through. Don't skip this
pass just because it's new.

## Method

Live staging only - see root `AGENTS.md`'s "Sandbox Limitations" for why no
local Aspire/Docker stack is available. Run this yourself in the current
session rather than delegating it to a subagent - MCP tool grants (including
the `playwright` plugin, when it's the thing driving the browser) do not
propagate to a spawned subagent, so it would silently have no way to browse.
Drive it with Playwright the same way the smoke-test scripts do:

```js
import { launchLiveBrowser, loginKeycloak } from "/home/user/einsatzbereit/scripts/lib/live-browser.mjs";
```

Write the driver script into the session scratchpad, not `scripts/` - this
is exploratory persona-driving, not a committed fix-verification smoke test.
Sign in as each persona in turn and walk the flows above, noting anything
that:

- breaks outright (error, dead end, silent no-op),
- would confuse a first-time user doing the realistic task (unclear state,
  missing feedback, a control that doesn't do what it looks like it does),
  or
- falls short of what the persona actually needs to get the real-life job
  done - "Olaf can't do X his role requires", not "wouldn't it be nice".

## Filing findings

For each finding that survives dedup, use `issue_write` (method `create`),
matching the shape of the existing `bug_report.yml` (Affected Persona,
Priority, Description with Actual/Expected, Steps to Reproduce, Environment,
Additional Information) or `user_story.yml` (Persona, Priority, User Story,
Description, Acceptance Criteria, Implementation Proposal, Additional
Information) template - whichever fits the finding.

- **Clear, unambiguous fix** (bug, broken flow, obviously-missing
  validation): label it `bug` or `user-story` plus `persona-sim`. This is
  fair game for next cycle's `issue-triage` to pick up and implement
  unsupervised, same as any other issue.
- **Needs the repo owner's own call** - a subjective/design decision, more
  than one defensible approach, unclear whether it's in scope for 1.0, or
  touches something already flagged as unsatisfying (e.g. achievements -
  the icon set and unlock criteria are known to need a rethink, not a
  patch): add the `needs-decision` label on top, and open the body with a
  bolded **"Needs your decision before implementation"** line plus the
  specific question(s) to answer. `issue-triage`'s Survey step skips
  anything labelled `needs-decision`, so this can never get auto-implemented
  out from under you.

Both labels are created automatically on first use, same as any other label
here (`dependencies`/`renovate` on issue #25 were never pre-declared either -
no `labels.yml` in this repo).

When genuinely unsure which bucket a finding belongs in, prefer
`needs-decision` - a wrongly-deferred bug costs one extra triage cycle; a
wrongly-automated judgment call costs a decision made without you.

## Report

End with a short summary of what was exercised (all three personas, which
flows) and what, if anything, was filed. That summary is the entire output
of a run that otherwise made no changes.
