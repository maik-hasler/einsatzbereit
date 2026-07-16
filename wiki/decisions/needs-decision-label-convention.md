---
type: decision-note
title: needs-decision blocks autonomous implementation until the repo owner makes a call
description: Issue #708 (a draft-save toast that doesn't say which tab the draft landed on) is a live example of the needs-decision label used exactly as intended - multiple valid fixes, no clear best one.
resource: https://github.com/maik-hasler/einsatzbereit/issues/708
tags: [meta, issue-triage, persona-simulation]
timestamp: 2026-07-16
---

# Schema

The `needs-decision` label (used by `persona-simulation` and honored by `issue-triage`, see their skill files) marks a finding where more than one fix is defensible and picking one is a product call, not an engineering one. `issue-triage`'s Survey step skips anything so labeled, so it can never get auto-implemented out from under the repo owner.

# Examples

Issue #708: after an organizer saves a new opportunity as a draft, the toast only says it was saved to "your organization dashboard," without naming the Calendar tab - the only place the Drafts section is visible. Because the Create Opportunity modal can be opened from any dashboard tab, organizers saving from elsewhere have no way to find their draft without prior UI knowledge.

Rather than prescribing a fix, the issue poses three options: auto-switch the organizer to the Calendar tab on save (risks interrupting mid-task work), move the Drafts section to its own tab or a persistent banner, or just reword the toast to name the Calendar tab as an interim fix. Labeled `bug` plus `needs-decision`; affected persona Organizer Olaf, priority Medium.

# Citations

- `#708` https://github.com/maik-hasler/einsatzbereit/issues/708
- `.claude/skills/persona-simulation/SKILL.md`
- `.claude/skills/issue-triage/SKILL.md`
