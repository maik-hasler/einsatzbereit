# Lens: Dead code

Goal: code that can be deleted today with zero behavior change -
unreferenced symbols, unreachable branches, unused dependencies and
assets. Deletion candidates, each with proof.

## Method

**Frontend (tooling-backed):**
1. `pnpm install`, then `npx knip` for unused files, exports, and
   dependencies. knip has no repo config - treat its output as candidate
   list, not verdict. Verify each hit manually before reporting.
2. Unused locale keys: run `node scripts/check-i18n-keys.js` first
   (repo's authority), then check dynamic key construction (repo-map
   trap 5) for anything it flags.
3. Unused assets (`src/assets`, `public/`): grep each filename across
   `src/` and `index.html`.
4. CSS: selectors in `styles/global.css` never used in any `className`.
   Beware computed class strings (`formClasses.ts` composes classes) -
   trace composition before flagging.
5. Unused dependencies knip flags: cross-check against vite config,
   eslint config, and scripts - build-time usage doesn't show in imports.

**Backend (static, no build):**
1. Enumerate public types per project (`grep -rn "public \(class\|record\|interface\|enum\)" backend/src`).
2. For each candidate, exhaustive reference search across ALL of
   `backend/` - remembering repo-map trap 1: handlers/validators/
   endpoints are DI entry points. A handler is dead only if its
   *request type* is never constructed by any endpoint.
3. Unreachable branches: conditions on enum values that no longer exist,
   flags always constant, `if` arms whose condition the type system
   already excludes.
4. Unused NuGet packages: package refs in `.csproj` with no matching
   `using`/namespace usage. Without a build this is **Likely** at best -
   analyzers and implicit MSBuild behavior can consume packages
   invisibly. Say so.

**Repo-wide:** scripts in `frontend/scripts/` nothing invokes (search
workflows, package.json scripts, docs, hooks) - there is no root `scripts/`
anymore, it was removed wholesale (`wiki/bundle/decisions/scripts-folder-removed.md`),
so don't flag its absence as a finding; `.claude/` agents/skills referenced
by nothing (check `.claude` configs and CLAUDE.md files before flagging).

## Verification bar

Every finding shows the exhaustive search: the exact command(s), and
"zero hits" or the full hit list explained away. If you cannot search
exhaustively (dynamic dispatch, reflection), the finding is Likely, and
the evidence names the unverifiable assumption.

## Output guidance

Group micro-findings (e.g. 12 unused locale keys) into ONE finding with
a table - the 10-finding cap counts substantive items, not lines.
Estimated deletion size per finding (files/LOC) helps the user
prioritize cleanup PRs.
