---
name: lens
description: >
  Deep, single-lens review of the einsatzbereit repository and its live
  staging site (https://github.com/maik-hasler/einsatzbereit,
  https://einsatzbereit.maik-hasler.de). Each run triages where review
  effort is most valuable right now, then goes deep on exactly ONE lens -
  bugs, dead code, dead features, repo hygiene, docs drift, test gaps, CI
  health, security, contributor accessibility, live personas (Volunteer
  Vera/Organizer Olaf/Platform Admin - functional friction and visual/
  content quality together), accessibility, or code/comment complexity -
  and files evidenced, ranked GitHub issues. Never writes code, never opens
  a branch or PR. Use whenever asked to review the repo or the live app,
  run the recurring routine, audit einsatzbereit, hunt for dead code/bugs/
  UX gaps/accessibility issues/overly complex code, simulate a user, or
  names any of the lenses - even without the word "review".
---

# Lens

One repository (code and live site both), one lens, full depth, filed as
issues. This skill exists because shallow "review everything" passes
produce noise; it runs routinely, so incomplete coverage per run is fine -
depth is the whole point, same as it always was.

This skill replaces three earlier ones - `issue-triage`, `persona-simulation`,
and `deep-lens-review` - that used to divide this work. Two problems with
that split: the live-persona lens and the static lenses never shared what
they learned (a design flaw found live and its root cause in source were
two separate findings instead of one), and a recurring "implement whatever
Survey picks" loop kept shipping fixes for things nobody had actually
looked at end to end. This skill only ever looks and reports; implementing
a fix is a separate, deliberate act a human or a later session decides to
take on a specific filed issue.

## Non-negotiables

1. **One lens per run.** If you notice something outside the current lens,
   write ONE line in the report's parking lot and move on. Do not
   investigate it. The temptation to chase interesting side-findings is
   the main failure mode of this skill.
2. **Evidence or it didn't happen.** Every finding carries proof: the
   exact location (file:line, or the live page/persona/network evidence
   for the live lenses), plus the command/search/repro/screenshot that
   demonstrates it. A dead-code claim without the exhaustive reference
   search, or a "this looks broken" claim without a screenshot and a
   retry-with-wait, is worthless - worse, it costs trust in the whole run.
3. **Never touch code, never open a branch or PR.** Output is GitHub
   issues (and comments) only - the one thing that keeps an otherwise-
   unsupervised, recurring run safe. Few and deep beats many and shallow
   here too: cap at 5 filed issues per run, prioritized by severity/impact;
   group related micro-findings into one issue with a table rather than
   filing one issue each.
4. **Run live-driving lenses yourself, never delegate them.** MCP tool
   grants (including the `playwright` plugin) do not propagate to a
   subagent spawned via the `Agent` tool - confirmed the hard way, a
   subagent asked to drive a browser had none of the tools it needed and
   came back empty-handed. `lens-personas.md` and `lens-accessibility.md`'s
   live pass both drive the live app; do that in the current session
   directly.

## Environment capabilities - probe, don't assume

This skill runs in different environments (claude.ai sandbox, Claude Code
inside the repo checkout, Claude Code on the web). Capabilities differ;
probe them first and set the verification bar accordingly:

- **Backend toolchain:** try `dotnet --version` and a restore. If it
  works, USE it - build the solution and run the test suites where the
  lens benefits; compiler and test output upgrade findings to Confirmed.
  If NuGet is unreachable, fall back to static reading and
  cross-referencing, and be explicit that behavior claims then cap at
  Likely.
- **Frontend toolchain:** npm registry is usually reachable everywhere.
  `npm i -g pnpm`, `pnpm install`, then real tooling: `pnpm check`,
  `pnpm lint`, `npx knip`.
- **Live browser access:** required for `lens-personas.md` and
  `lens-accessibility.md`'s live pass. `ToolSearch` for `browser_navigate`
  (the `playwright` plugin); if nothing resolves, fall back to a scratch
  Playwright script (`npm install playwright` once per session in a scratch
  dir, not the repo - there is no root `package.json` anymore; Chromium ships
  pre-installed at `/opt/pw-browsers`; see
  `wiki/bundle/process/live-playwright-scripts.md` for the TLS-workaround
  launch args). If neither is available at all, these two lenses cannot run
  this session - say so in triage and exclude them from scoring rather than
  attempting a code-only substitute.
- **GitHub API:** `search_issues`/`issue_write` for dedup and filing;
  unauthenticated `https://api.github.com/repos/maik-hasler/einsatzbereit/...`
  for Actions run durations and outcomes if authenticated tools aren't
  available (~60 requests/hour - enough for triage probes, not bulk
  crawling).

Where a lens file says something "cannot run in this sandbox", read that
as: cannot run in restricted environments. If your probe says you can, do
it and raise the confidence bar.

## Workflow

### Step 0 - Lens named by the user?

If the user names a lens (or something that clearly maps to one, e.g.
"simulate a user" -> personas, "check contrast" -> accessibility, "find
overly clever code" -> complexity), skip triage and go to Step 2. If they
reference the previous run ("last time was CI"), exclude that lens from
triage.

### Step 1 - Locate the repo and triage

If the working directory already is an einsatzbereit checkout, use it:
record HEAD (`git log -1 --format="%H %ad"`) and whether the working tree
is dirty, and review that state - also `git fetch origin main` and skim
`git log origin/main --oneline -20` first, this repo ships fast and a
review that assumes last week's state is current wastes most of its effort
re-finding things that already shipped. Otherwise clone in full:

```bash
git clone https://github.com/maik-hasler/einsatzbereit.git
cd einsatzbereit && git log -1 --format="%H %ad"
```

Then run the triage probes. Timebox: ~5 minutes total. Triage selects a
lens; it is not the review. Do not start investigating findings here.

| Probe | Command / source | Feeds lens |
|---|---|---|
| Churn hotspots | `git log --since=21.days --name-only --pretty=format: \| sort \| uniq -c \| sort -rn \| head -25` | bugs, test-gaps, complexity |
| Fix density | `git log --since=60.days --oneline \| grep -i "fix"` - count and cluster by area | bugs |
| Tracked-file scan | `git ls-files` - eyeball for junk, secrets-shaped names, size outliers | repo-hygiene, security |
| Docs staleness | last-commit date of each `*.md` vs churn of the code it describes | docs-drift |
| CI signal | GitHub API `actions/runs?per_page=30` - failure rate, durations | ci |
| Community signal | GitHub API open issues/PRs - age, labels, unanswered | contributor-dx, bugs |
| Test-to-src churn | ratio of changed test files to changed src files (from churn probe) | test-gaps |
| a11y coverage gap | grep `AccessibilityTests.cs` for `HasNoSeriousA11yViolations`, diff against `App.tsx` routes | accessibility |
| Comment hedge scan | grep `careful\|hack\|workaround\|don't\|must\|NOTE\|WARNING` density across `backend/src`, `frontend/src` | complexity |
| Days since last live pass | check recent closed/open issues labeled `lens` for a personas-lens finding's timestamp | personas |

Score every lens 1-5 for **signal** (evidence something is off) and
**impact** (cost if it stays unaddressed). Pick the highest product. On a
tie, prefer whichever of bugs/security/personas scores highest - user-
facing correctness and safety over polish. Record the full ranking - it
goes in the run's closing summary so future runs can steer.

### Step 2 - Load context, then the lens

Read `references/repo-map.md` first - always. It contains the repo
layout, stack facts, and the false-positive traps that have burned
reviews before (DI-resolved handlers, generated clients, EF migrations).
Then read exactly one lens file:

| Lens | File | Character |
|---|---|---|
| Bugs & correctness | `references/lens-bugs.md` | static, one vertical slice per run |
| Dead code | `references/lens-dead-code.md` | static |
| Dead features | `references/lens-dead-features.md` | static |
| Repo & filesystem hygiene | `references/lens-repo-hygiene.md` | static |
| Docs quality & drift | `references/lens-docs-drift.md` | static |
| Test gaps | `references/lens-test-gaps.md` | static |
| CI health & performance | `references/lens-ci.md` | static |
| Security smells | `references/lens-security.md` | static |
| Contributor accessibility | `references/lens-contributor-dx.md` | static |
| Personas | `references/lens-personas.md` | live - drives staging as Vera/Olaf/Admin; functional friction and visual/content quality together |
| Accessibility | `references/lens-accessibility.md` | static + live |
| Code & comment complexity | `references/lens-complexity.md` | static |

### Step 3 - Execute the lens

Follow the lens file's method. Budget the bulk of the run here. Verify
each candidate finding to one of these confidence levels before it may be
filed:

- **Confirmed** - executed proof: tool output, exhaustive search with
  zero hits, a traced end-to-end repro narrative, or (for the live lenses)
  a screenshot plus the specific evidence (network status, console error,
  innerText comparison) - checked with the retry-with-wait discipline
  where "is this actually broken" is in question.
- **Likely** - strong static cross-reference, or a live observation not
  yet root-caused in source; exactly one assumption you could not verify
  (name it in the evidence).
- **Hypothesis** - plausible pattern; needs a human or a running system to
  confirm. Use sparingly; more than 3 hypotheses in a run means the
  verification bar slipped. Hypothesis-level findings are not filed as
  issues - note them in the closing summary's parking lot instead.

Severity rubric:

- **Critical** - data loss, security exposure, or a broken main user flow.
- **High** - user-visible defect, a maintainability landmine that will
  bite the next contributor, or a live persona genuinely blocked from
  their role's task.
- **Medium** - quality erosion, an inconsistency next to clearly better
  work nearby, or complexity that will bite eventually; wrong but
  contained.
- **Low** - polish.

### Step 4 - Dedup, then file

Before filing anything, `search_issues` for `label:lens` plus
keywords from the candidate finding - don't re-report the same thing
every run just because it's still true. If an open PR already addresses
it, note the relationship in a comment on the existing issue instead of
filing a duplicate.

For each finding that survives dedup and the confidence bar, use
`issue_write` (method `create`), matching the shape of the existing
`bug_report.yml` (Affected Persona/Area, Priority, Description with
Actual/Expected, Steps to Reproduce or repro evidence, Environment,
Additional Information) or `user_story.yml` (Persona, Priority, User
Story, Description, Acceptance Criteria, Implementation Proposal,
Additional Information) template - whichever fits. Label every filed
issue `lens` plus `bug` or `user-story` as appropriate (both
labels are created automatically on first use, same as any other label in
this repo).

Route judgment calls explicitly:

- **Clear, unambiguous fix** (bug, broken flow, obviously-missing
  validation, dead code with exhaustive proof): file it as-is. Anyone -
  human or a future implement-focused session - can pick it up.
- **Needs the repo owner's own call** - a subjective/design decision, more
  than one defensible approach, unclear scope, or touches something
  already flagged as unsatisfying: add `needs-decision` on top, and open
  the body with a bolded **"Needs your decision before implementation"**
  line plus the specific question(s) to answer. Nothing in this repo's
  tooling currently auto-implements from the backlog, but the label still
  tells whoever triages it that this one isn't ready to just build.

When genuinely unsure which bucket a finding belongs in, prefer
`needs-decision` - a wrongly-deferred bug costs one extra review cycle; a
wrongly-treated-as-obvious judgment call costs a decision made without the
owner.

## Output guidance

Group micro-findings (e.g. 12 unused locale keys, or the same layout bug
repeated across 4 files) into ONE issue with a table or a list of every
location - the 5-issue cap counts substantive findings, not individual
occurrences.

End every run with a short closing summary in chat (not a file): the lens
chosen and why, what was exercised (which flows/personas for live lenses,
which slice for static ones), how many issues were filed vs. deduped away,
and the parking lot. That summary is the entire visible output of a run
that filed nothing - a clean pass is a fine, common outcome, not a failure
to find something.

## Boundary to the in-repo `self-review` skill

The repository ships its own `.claude/skills/self-review` - that one is
diff-scoped and pre-PR, for whoever is about to open one. This skill is
whole-repo (and whole-live-site) and routine, and it never touches code.
Never substitute one for the other; if the user asks about a diff or a
PR, this is the wrong skill.
