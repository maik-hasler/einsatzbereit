# Lens: Code & comment complexity

Goal: code that is harder to change safely than the problem it solves
requires - measured by structure (nesting, length, branching) and by what
the comments around it are actually saying. Complexity is not a defect on
its own; unexplained or unnecessary complexity is.

## Method

1. **Find structural hotspots.** No repo-configured complexity linter
   exists here (check before assuming - `eslint.config.js` for a
   `complexity`/`cognitive-complexity` rule, `.editorconfig-checker.json`,
   any Roslyn analyzer ruleset), so use heuristics and say so:
   - Frontend: component/function line count, JSX nesting depth, and
     hook count per component (`useState`/`useEffect`/`useMemo` piling up
     in one file is a real signal - it usually means the component is
     doing several jobs). `git log --since=60.days --name-only` from the
     triage churn probe doubles as a hotspot list here: a file that
     changes constantly *and* is long/deeply nested is the highest-value
     target, not the longest file in isolation.
   - Backend: method length, cyclomatic proxies (count of `if`/`else
     if`/`switch` arms/`&&`/`||` per method), and constructors/methods
     with long parameter lists as a coupling smell.
   - Both: deeply nested conditionals/callbacks (4+ levels), and any
     function whose name no longer describes everything it does (a
     `Save`/`Update` that also sends notifications and recalculates
     achievements, say).
2. **Read the comments as evidence, not decoration.** Grep for hedge and
   warning language - `careful`, `hack`, `workaround`, `don't`, `must`,
   `NOTE`, `WARNING`, `important`, `order matters`, `do not` - across
   `backend/src` and `frontend/src`. A defensive comment like this is
   usually marking real fragility (an implicit ordering dependency, a
   non-obvious invariant); read the code it's attached to and judge
   whether the comment is compensating for complexity that could instead
   be removed (extract a function, name the invariant in a type, add a
   guard clause) rather than just narrated.
3. **Find comment noise in the other direction.** Comments that restate
   what the next line already says (`// increment counter` above
   `counter++`), comments describing behavior the code below no longer
   has (stale - cross-check against git blame/history if the mismatch is
   surprising), and outlier comment-to-code density (a file or function
   with far more comment lines than its neighbors) - the last one cuts
   both ways: it can mean good documentation of real complexity, or it can
   mean the code needed that many words *because* it's more tangled than
   it should be. Read enough to tell which.
4. **Cross-reference against this repo's own stated preference.** Root
   `AGENTS.md`'s "Simple code: the source code should be simple enough
   that anyone can contribute" is a named goal, not an aspiration to
   ignore - weight a complexity finding higher if it sits in a feature
   area a new contributor would plausibly touch early (see
   `lens-contributor-dx.md`), lower if it's inherent complexity in a
   genuinely hard problem (the dashboard widget drag/resize/overlap
   logic, geocoding, timezone handling) that a simpler version would just
   be wrong.

## Verification bar

A structural finding cites the file, the specific metric (line count,
nesting depth, branch count) and how it compares to the rest of the
codebase - not a bare "this is complex". A comment finding quotes the
comment and the code it sits on, and states which failure mode it is
(masking removable complexity, stale, redundant, or - the good case -
genuinely load-bearing documentation of real complexity, worth leaving
alone and saying so). Recommend the smallest concrete simplification
(extract this, name that invariant, delete this comment) rather than
"refactor this".

## Traps

Long is not automatically complex - a component that is long because it
lists many similar, independent JSX blocks (a form with twenty plain
fields) is lower-risk than a short function with five nested conditionals.
Generated code (`api-client.ts`, EF migrations) and test files with
intentionally repetitive setup are out of scope - complexity there is not
the codebase's to fix. A comment explaining *why*, not *what*, next to
genuinely non-obvious code is the target state this repo already asks
for (root `AGENTS.md` style guidance) - don't flag it as noise.
