# Lens: Docs quality

Goal: documentation sized and aimed at the reader who actually uses it -
not documentation that merely accumulated. Judge audience fit first
(arc42's own bar: "as much as necessary, as little as possible" - right
content, right length, for who actually reads it); accuracy is a
compact second step, not the majority of the lens. A bloated CLAUDE.md/
AGENTS.md loads in full every session and measurably degrades how
reliably instructions get followed; a wrong README claim costs a
contributor minutes once caught - both are findings, weighted by that
real cost, not by which is easier to grep for. Score against the named,
citable norms below, not personal taste - the old version of this lens
leaned on a single style authority for prose; that survives as one
Low-weight prose check, not the rubric.

## Method

1. **Sort by audience before judging anything:** arriving human
   (README, CODE_OF_CONDUCT.md, SECURITY.md), contributing human
   (CONTRIBUTING.md), architecture stakeholder (`docs/Architecture`,
   ADRs, TDRs), AI agent (every `AGENTS.md`/`CLAUDE.md` in the tree,
   loaded automatically). Content aimed at the wrong reader is a
   finding on its own, independent of accuracy: setup trivia an agent
   never needs to re-derive, or process detail duplicated between
   README and CONTRIBUTING instead of one linking to the other.
2. **arc42 docs (`docs/Architecture`, ADRs, TDRs) - fit over
   completeness:** does each populated chapter earn its place, or
   restate the code without adding a decision or constraint (arc42's
   own "as much as necessary" bar, arc42.org)? Section 3 (context and
   scope) - is the business/domain perspective present as the default
   view, with the technical context (channels, protocols, hardware)
   only added where docs.arc42.org actually calls it out as necessary,
   not duplicated wholesale? Section 1.2 (quality goals) - top 3-5, concrete,
   stakeholder-tied, buzzword-free (arc42's own guidance), not a longer
   wishlist? Section 9 / ADRs - Nygard shape (Context/Decision/Status/
   Consequences), explaining *why* rather than restating *what* the
   code already shows; an ADR the code has since abandoned needs a
   superseding note, not silence (carried over from the old lens -
   still the sharpest finding class here). Do the building-block (5)
   and runtime (6) views tie back to the goals in 1.2, or float free of
   them?
3. **README / CONTRIBUTING / community-health files - findability and
   non-duplication:** does the README answer what this is, who it is
   for, and how to start, above the fold (standard-readme's own
   ordering - optional sections may be dropped, not padded in to match
   a template)? Does CONTRIBUTING own process detail with README only
   linking to it? Are CODE_OF_CONDUCT.md/SECURITY.md/issue-and-PR
   templates present and each doing one job without overlapping the
   others (GitHub's Community Health Files split)? Restated content
   between README, CONTRIBUTING, and a component `AGENTS.md` is a
   finding - name both locations and say which one should keep it.
4. **`AGENTS.md`/`CLAUDE.md` at every level - the reader is an agent,
   budget accordingly:** these load in full at every session start (via
   the `@AGENTS.md` import chain into each directory's `CLAUDE.md`) -
   nothing here is silently truncated, but length still has a
   documented, measurable cost. Run `wc -l` on every `AGENTS.md`/
   `CLAUDE.md` in the tree and check it against the official target -
   "target under 200 lines per CLAUDE.md file. Longer files consume
   more context and reduce adherence" (code.claude.com/docs) - flagging
   overage in proportion to how far over. Then judge signal density on
   what's there: is every line something an agent could not otherwise
   derive (a real constraint, a non-obvious command, a trap that has
   actually bitten a run), or prose it could infer from the code, or a
   restatement of README/CONTRIBUTING in a different voice? Flag
   generic verification-reminder filler ("always run the tests",
   "double-check your work") - it spends tokens without adding
   information a capable model doesn't already default to. Prefer
   pointers (`file:line`, a linked reference, a named skill) over
   inlined code blocks or walkthroughs that belong next to the code.
5. **Accuracy (compact, evidence-gated - the whole lens before, one
   step now):** claim-test README/CONTRIBUTING/`AGENTS.md` against
   source of truth - commands exist and are spelled right, prerequisite
   versions match `global.json`/`.csproj`/`package.json` engines, the
   services/ports table matches AppHost and docker-compose, CONTRIBUTING's
   described PR process matches what `pr-title.yml` and branch
   protection actually enforce, referenced agents/hooks/paths in
   `AGENTS.md` exist. Internal links resolve to existing files/anchors;
   most external domains are unreachable from this sandbox - list them
   unverified rather than guessing.
6. **Prose quality (Low/Medium, last):** bury-the-verb constructions,
   filler, walls of text, a headline that says nothing, inconsistent
   terminology (Engagement vs Opportunity vs Einsatz - is the glossary
   stable across docs and UI?). Report patterns with 2-3 examples, not
   a line-by-line copy edit.

## Verification bar

Every finding names the document, quotes the doc (<=1 line), and states
the norm it fails against - cite arc42.org/docs.arc42.org by section,
standard-readme, GitHub's Community Health Files, or the CLAUDE.md size
guidance directly, not "this reads long" or "this feels thin". Length/
budget findings show the actual `wc -l` count against the ~200-line
target and the adherence-cost rationale, not a claim that content is
dropped. Accuracy findings additionally cite the
contradicting source (`path:line`) and state which side is presumably
right. Style findings stay Low/Medium and never outnumber audience-fit
and accuracy findings combined.

## Traps

Docs may describe intended future state - check git history: if the doc
predates the divergent code, it's drift; if it postdates it, it may be a
roadmap statement, say which. `docs/Architecture` is AsciiDoc built to
HTML by `docs.yml` for its actual (human) readers - judge the rendered
result's fit and completeness, not raw source line count. A short doc is
not automatically a better one: the ~200-line `AGENTS.md`/`CLAUDE.md`
target is an adherence guideline, not a hard technical cutoff - these
files load in full regardless of length, so cutting real signal just to
land under the number is its own finding. Don't conflate this with Claude
Code's separate auto-memory `MEMORY.md` (`~/.claude/projects/.../memory/`,
not part of this repo), which genuinely is hard-truncated at 200 lines/
25KB - that mechanism and its threshold don't apply here.
`.claude/skills/*/references/*.md` (this file included) are a third
category again: agent-audience, but not loaded at session start at all -
only when the `lens` skill triggers. No line-count target applies to
them; judge those on signal density and progressive disclosure only.
