# Lens: Docs quality & drift

Goal: documentation that lies, and documentation that reads badly.
Drift first - a wrong README costs contributors hours; a clumsy one
costs minutes.

## Method

1. **Claim-test the README:** every executable claim gets checked
   against the repo. Commands exist and are spelled right? Prerequisite
   versions match `global.json`/`.csproj` TargetFramework and
   `package.json` engines? The services/ports table matches AppHost and
   docker-compose? Test-user credentials match the Keycloak realm
   export? You cannot *run* the stack here - verify against source of
   truth in config, and mark run-only claims as unverifiable.
2. **Same treatment** for CONTRIBUTING.md (does the described PR process
   match what pr-title.yml and branch protection actually enforce?),
   VERSIONING.md (matches release-rc.yml/publish.yml behavior?),
   frontend/ and keycloak/ READMEs.
3. **CLAUDE.md accuracy (all five):** these steer AI contributors. Do
   referenced agents, hooks, paths, and commands exist? Stale AI
   instructions actively cause bad PRs - weight drift here High.
4. **ADR/TDR spot-check:** sample 3-5 decisions; is the code still doing
   what the decision says? An overturned-but-undocumented ADR is a
   finding (suggest superseding note, not deletion).
5. **Link check:** internal links resolve to existing files/anchors.
   External links: most domains are unreachable from this sandbox - list
   them as unverified rather than guessing.
6. **Writing quality (second priority):** apply Wolf Schneider standards
   to prose the user owns: bury-the-verb constructions, filler, walls of
   text, headline says nothing, inconsistent terminology (Engagement vs
   Opportunity vs Einsatz - is the glossary stable across docs and UI?).
   Report patterns with 2-3 examples, not a line-by-line copy edit.

## Verification bar

A drift finding quotes the doc (≤1 line), cites the contradicting
source (`path:line`), and states which side is presumably right.
Style findings are Low/Medium and never outnumber drift findings.

## Traps

Docs may describe intended future state - check git history: if the doc
predates the divergent code, it is drift; if it postdates it, it may be
a roadmap statement. Say which.
