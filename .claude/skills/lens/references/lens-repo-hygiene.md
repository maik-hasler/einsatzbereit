# Lens: Repo & filesystem hygiene

Goal: the repository as a filesystem - what is tracked that shouldn't
be, what is missing that should exist, what sits in the wrong place.

## Method

1. **Full inventory pass:** `git ls-files` - read the whole list (it is
   short enough). Flag: build artifacts, editor/OS junk, generated files
   that should be gitignored instead (NB: the NSwag client is
   *deliberately* committed and hook-protected - not junk), anything
   secret-shaped (also run a pattern scan: `key`, `token`, `secret`,
   `password`, PEM headers across tracked files; keycloak realm JSON per
   repo-map trap 4).
2. **Size outliers:** `git ls-files -z | xargs -0 du -b | sort -rn | head -20`
   plus history bloat check on the worst offenders
   (`git log --oneline -- <file> | wc -l` on binaries).
3. **.gitignore adequacy:** simulate a dev day - build outputs
   (`bin/`, `obj/`, `dist/`, `node_modules/`, `.env`), IDE dirs, Aspire
   artifacts. Are all covered? Anything tracked *despite* matching?
4. **Missing standard files:** judge, don't cargo-cult. For a solo-
   maintained open source repo, evaluate: SECURITY.md (security.yml
   workflow exists - where do humans report?), CODEOWNERS (single
   maintainer - probably noise), CHANGELOG (VERSIONING.md exists; is the
   release process's output discoverable?), .editorconfig ↔ prettier
   overlap/conflict.
5. **Placement:** root-level clutter vs `docs/`; `frontend/scripts/` is the
   only scripts dir now (root `scripts/` was removed wholesale, see trap
   below - don't flag its absence). Config files that belong closer to
   their consumer. Naming consistency of dirs (casing, singular/plural).
6. **Config drift:** `.env.example` keys vs variables actually read in
   code, docker-compose, AppHost, and workflows (repo-map trap 7 - search
   both naming conventions). Ports/credentials consistent across
   README table, docker-compose, and AppHost.

## Verification bar

"Should be ignored" findings show the tracked path AND the ignore rule
that ought to cover it. "Missing file" findings argue from this repo's
actual needs (cite the workflow/doc that creates the need), not from
generic checklists. Placement findings are Medium at most and must name
the concrete confusion they cause.

## Traps

`.claude/` is first-class content. docker-compose next to Aspire is not
automatically redundant - find its consumer (CI? production?) before
flagging. There is no root `package.json` anymore - live-verification
Playwright scripts are scratch-only, installed ad hoc outside the repo
(`wiki/bundle/decisions/scripts-folder-removed.md`); do not flag its
absence as a gap.
