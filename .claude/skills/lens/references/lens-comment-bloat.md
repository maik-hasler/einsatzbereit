# Lens: Comment bloat & noise

Goal: comments that cost more to read than the code they annotate returns
in value - length, redundancy, and staleness, independent of whether the
underlying code is structurally complex. LLM-authored contributions have a
well-documented tendency toward over-explaining: restating what a line
already says, multi-paragraph docstrings on simple functions, narrating a
change's history instead of stating a durable invariant. That pattern is
easy to miss as a single bullet inside a broader lens - it earns its own
dedicated sweep.

## Method

1. **Find comment-to-code density outliers.** Rough ratio per file
   (comment lines - `//`, `///`, `/* */`, `#` - versus total lines) across
   `backend/src` and `frontend/src`; sample enough files first to learn
   this codebase's own norm, then flag files/functions markedly above it -
   don't apply a universal threshold. Backend XML doc comments (`///`)
   repeated near-verbatim across many similar handlers/endpoints are a
   distinct boilerplate pattern worth its own line even when no single file
   is a density outlier.
2. **Read each candidate and classify the bloat:**
   - **Restates the code (WHAT, not WHY)** - `// increment counter` above
     `counter++`, a docstring that just re-lists the parameter names in
     prose.
   - **Over-explains the obvious** - a paragraph where the code needs a
     clause; explains something any contributor at this repo's stated
     skill level would already know.
   - **Narrates process, not invariant** - "we changed this because the
     old approach broke X", "added for the Y flow", "fix for issue #NNN".
     That belongs in the commit message or PR description; it rots the
     moment the code moves on and the comment doesn't.
   - **Stale** - describes behavior the code below no longer has; cross-
     check `git blame`/history when the mismatch is surprising.
   - **Genuinely load-bearing (the good case)** - explains a non-obvious
     WHY: a hidden constraint, a workaround, a subtle invariant. Read it,
     don't flag it - note a couple of these in the report as a positive
     baseline, not just a list of complaints.
3. **Judge length against necessity, not a word count.** A comment's
   length should track the complexity of the WHY, not the length of the
   code below it - a five-line comment above a five-line function is
   suspect regardless of content. Test: would deleting this comment
   confuse the next reader? If not, it is a deletion candidate, not a
   trim candidate.
4. **Check neighbors once you find one.** Bloat clusters - if `git blame`
   or a commit message on a candidate points at one PR/session, check the
   other files that same commit touched before generalizing from a single
   file to "this is everywhere".

## Verification bar

A finding quotes the comment verbatim (or the representative lines, if
long) plus the code it sits on, states the bloat category (restates /
over-explains / narrates process / stale), and gives a concrete fix -
delete entirely, trim to N lines, or move the one durable WHY into a
single line - not "shorten this". A density finding states the actual
ratio measured and the baseline it was compared against, learned from
sampling this codebase, not an arbitrary universal number.

## Traps

Comment volume is not a defect by default: domain-inherent complexity
(geocoding, timezone handling, the dashboard drag/resize/overlap logic)
can legitimately need a longer explanation - the target state is a
comment whose length matches the WHY's actual complexity, not a ceiling.
Generated code (`api-client.ts`'s NSwag header, EF migration files) and
vendored comment blocks (license headers, `.claude/skills/frontend-design`
per its own Apache-2.0 provenance note) are out of scope. Public API
surface doc comments required by convention (check whether one actually
exists here before assuming) are not bloat merely for existing - judge
their content, not their presence.

## Boundary to the code/comment complexity lens

`lens-complexity.md` also reads comments, but only as evidence of
underlying *structural* complexity - a hedge comment (`careful`, `hack`,
`must`) marking real fragility, or a density outlier read as a symptom of
tangled code. If a finding is really "this code is hard to change safely
and the comment proves it", file it there. This lens's findings stand on
their own even next to trivially simple code: a bloated, redundant, or
stale comment is a defect whether or not the function underneath it is
fragile. When a single comment fits both descriptions, one finding is
enough - file it wherever the *primary* problem lives (the code's
structure, or the comment's own writing) and don't double-count it in
both reports.
