---
name: deep-lens-review
description: >
  Deep single-lens review of the einsatzbereit open source repository
  (https://github.com/maik-hasler/einsatzbereit). Each run clones the repo
  fresh, triages where review effort is most valuable right now, then
  examines exactly ONE lens in depth — bugs, dead code, dead features,
  repo hygiene, docs drift, test gaps, CI health & performance, security,
  or contributor accessibility — and delivers a downloadable Markdown
  report with evidenced, ranked findings. Use this skill whenever the user
  asks to review their repo, run the routine/weekly review, audit
  einsatzbereit, hunt for dead code or bugs in the repository, check the
  repo structure, or names any of the lenses — even without the word
  "review". Not for reviewing a single diff or PR.
---

# Einsatzbereit deep review

One repository, one lens, full depth. This skill exists because shallow
"review everything" passes produce noise; the user runs this as a routine,
so incomplete coverage per run is fine — depth is the whole point.

## Non-negotiables

1. **One lens per run.** If you notice something outside the current lens,
   write ONE line in the report's parking lot and move on. Do not
   investigate it. The temptation to chase interesting side-findings is
   the main failure mode of this skill.
2. **Evidence or it didn't happen.** Every finding carries proof: the
   exact location, plus the command/search/repro that demonstrates it.
   A dead-code claim without the exhaustive reference search is worthless
   — worse, it costs the user trust in the whole report.
3. **Few and deep beats many and shallow.** Hard cap of 10 findings,
   ranked by severity. If you have 25 candidates, verify and write up the
   10 most severe; the rest become parking-lot lines at most.
4. **Report, don't fix.** Never push, open PRs, or modify the repo. The
   deliverable is a Markdown report file.

## Environment capabilities — probe, don't assume

This skill runs in different environments (claude.ai sandbox, Claude
Code inside the repo checkout). Capabilities differ; probe them first
and set the verification bar accordingly:

- **Backend toolchain:** try `dotnet --version` and a restore. If it
  works (typical locally), USE it — build the solution and run the test
  suites where the lens benefits; compiler and test output upgrade
  findings to Confirmed. If NuGet is unreachable (typical in the
  claude.ai sandbox), fall back to static reading and cross-referencing,
  and be explicit that behavior claims then cap at Likely.
- **Frontend toolchain:** npm registry is usually reachable everywhere.
  `npm i -g pnpm`, `pnpm install`, then real tooling: `pnpm check`,
  `pnpm lint`, `npx knip`.
- **GitHub API:** unauthenticated access to this public repo
  (`https://api.github.com/repos/maik-hasler/einsatzbereit/...`) serves
  issues, PRs, Actions run durations and outcomes. ~60 requests/hour —
  enough for triage probes, not for bulk crawling.

Where a lens file says something "cannot run in this sandbox", read
that as: cannot run in restricted environments. If your probe says you
can, do it and raise the confidence bar.

## Workflow

### Step 0 — Lens named by the user?

If the user names a lens (or something that clearly maps to one, e.g.
"look for unused stuff" → dead code), skip triage and go to Step 2.
If they reference the previous run ("last time was CI"), exclude that
lens from triage.

### Step 1 — Locate the repo and triage

If the working directory already is an einsatzbereit checkout (typical
when this skill runs from the repo's own `.claude/skills/`), use it:
record HEAD (`git log -1 --format="%H %ad"`) and whether the working
tree is dirty, and review that state. Otherwise clone in full — history
is needed for churn analysis and the repo is small:

```bash
git clone https://github.com/maik-hasler/einsatzbereit.git
cd einsatzbereit && git log -1 --format="%H %ad"
```

Then run the triage probes. Timebox: ~5 minutes total. Triage selects a
lens; it is not the review. Do not start investigating findings here.

| Probe | Command / source | Feeds lens |
|---|---|---|
| Churn hotspots | `git log --since=21.days --name-only --pretty=format: \| sort \| uniq -c \| sort -rn \| head -25` | bugs, test-gaps |
| Fix density | `git log --since=60.days --oneline \| grep -i "fix"` — count and cluster by area | bugs |
| Tracked-file scan | `git ls-files` — eyeball for junk, secrets-shaped names, size outliers | repo-hygiene, security |
| Docs staleness | last-commit date of each `*.md` vs churn of the code it describes | docs-drift |
| CI signal | GitHub API `actions/runs?per_page=30` — failure rate, durations | ci |
| Community signal | GitHub API open issues/PRs — age, labels, unanswered | contributor-dx, bugs |
| Test-to-src churn | ratio of changed test files to changed src files (from churn probe) | test-gaps |

Score every lens 1–5 for **signal** (evidence something is off) and
**impact** (cost if it stays unaddressed). Pick the highest product.
On a tie, prefer security or bugs. Record the full ranking — it goes in
the report so the user can steer future runs.

### Step 2 — Load context, then the lens

Read `references/repo-map.md` first — always. It contains the repo
layout, stack facts, and the false-positive traps that have burned
reviews before (DI-resolved handlers, generated clients, EF migrations).
Then read exactly one lens file:

| Lens | File | Scope note |
|---|---|---|
| Bugs & correctness | `references/lens-bugs.md` | one vertical slice per run |
| Dead code | `references/lens-dead-code.md` | |
| Dead features | `references/lens-dead-features.md` | |
| Repo & filesystem hygiene | `references/lens-repo-hygiene.md` | |
| Docs quality & drift | `references/lens-docs-drift.md` | |
| Test gaps | `references/lens-test-gaps.md` | |
| CI health & performance | `references/lens-ci.md` | |
| Security smells | `references/lens-security.md` | |
| Contributor accessibility | `references/lens-contributor-dx.md` | |

### Step 3 — Execute the lens

Follow the lens file's method. Budget the bulk of the run here. Verify
each candidate finding to one of these confidence levels before it may
enter the report:

- **Confirmed** — executed proof: tool output, exhaustive search with
  zero hits, or a traced end-to-end repro narrative.
- **Likely** — strong static cross-reference; exactly one assumption you
  could not verify (name it in the evidence).
- **Hypothesis** — plausible pattern; needs a human or a running system
  to confirm. Use sparingly; more than 3 hypotheses in a report means
  the verification bar slipped.

Severity rubric:

- **Critical** — data loss, security exposure, or a broken main user flow.
- **High** — user-visible defect, or a maintainability landmine that will
  bite the next contributor.
- **Medium** — quality erosion; wrong but contained.
- **Low** — polish.

### Step 4 — Write the report

File name: `deep-lens-review-<lens>-<YYYY-MM-DD>.md`, structured
exactly as below. Location: the environment's output directory
(`/mnt/user-data/outputs/` in claude.ai — share it via the
file-presentation tool); in a local checkout, write outside the repo
(e.g. `/tmp`) or wherever the user designates. Never commit reports
into the repository unless the user explicitly asks. Keep chat output
to a 3–5 sentence summary of the top findings; the report is the
deliverable.

```markdown
# Einsatzbereit review — <Lens> — <YYYY-MM-DD>

Commit: `<sha>` (<commit date>) · Lens chosen by: triage | user

## Triage summary        <!-- omit this section when the user named the lens -->
| Lens | Signal | Impact | Score | Note |
|---|---|---|---|---|
Why <lens> won: <2 sentences>

## Scope & method
What was examined, what was deliberately excluded, which tools ran.

## Findings
### F1 — <short title>
**Severity:** <level> · **Confidence:** <level> · **Location:** `<path:line>`

Evidence: <the proof — command + output excerpt, or repro narrative>

Impact: <why it matters, 1–3 sentences>

Suggested fix: <concrete, smallest reasonable step>

<!-- repeat, ranked by severity, max 10 -->

## Parking lot
- <one line each: out-of-lens observations, unverified>

## Suggested next lens
<lens> — <one-sentence signal from this run>
```

Writing style for the report: short sentences, active voice, no filler,
no hedging beyond the confidence label. The user reads these routinely;
every wasted sentence is paid weekly.

## Boundary to the in-repo `self-review` skill

The repository ships its own `.claude/skills/self-review` — that one is
diff-scoped and pre-PR. This skill is whole-repo and routine. Never
substitute one for the other; if the user asks about a diff or a PR,
this is the wrong skill.
