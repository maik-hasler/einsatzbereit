---
type: "decision-note"
title: "The root scripts/ folder and root package.json were removed"
description: "98 tracked .mjs/.sh files and the Playwright pin they depended on are gone; live-verification scripts are now scratch-only and never committed."
tags:
  - scripts
  - playwright
  - repo-hygiene
  - deploy-verify
  - smoke-test
timestamp: 2026-07-24
---

# What changed

The root `scripts/` directory (98 tracked files: 16 orphaned debug/explore
scripts with zero references anywhere, plus 80+ `smoke-test-*.mjs` files
mapping to past issues/PRs, plus `scripts/lib/live-browser.mjs`) and the root
`package.json`/`package-lock.json` that pinned Playwright for it are deleted.
The mandatory deploy-and-verify flow (root `AGENTS.md`, step 6) still requires
a live Playwright script against staging for every fix, but that script is now
written to a scratch directory outside the repo, run once, and discarded - it
is never `git add`ed.

# Why

#791 found the 16 orphaned scripts (added by accident in an unrelated commit,
referenced nowhere) and asked only for those to go. The repo owner pushed
further: the wiki's own [deploy-verify-flow](/process/deploy-verify-flow.md)
already documented the live Playwright script as "throwaway proof that the
change works on live staging right now" - the durable, reviewable record was
always meant to be the C# TUnit test in `backend/tests/VisualTests/`, not the
`.mjs` file. Persisting 80+ scripts explicitly marked throwaway in git was
debris by the process's own definition, not an intentional archive - nobody
had ever gone back and deleted one. Contributor Caro (the persona #791 is
filed against) hits the same confusion either way: a loose `.mjs` file with no
CI reference and no obvious expiry date, safe or unsafe to run against staging
depending on which one it is.

# What this does not change

Step 6 of the mandatory flow is unchanged in substance: write a Playwright
script, launch it with the sandbox's TLS-workaround args, log in, exercise the
changed behaviour, require exit 0. Only where the script lives changed - a
scratch directory instead of a tracked one - and the TLS launch args and
Keycloak login steps that used to live in `scripts/lib/live-browser.mjs` are
now inlined in [live-playwright-scripts](/process/live-playwright-scripts.md)
and in `AGENTS.md` itself, since there is no committed file left to import
them from. Root `package.json` also carried an `editorconfig-checker`
devDependency, but CI's `lint.yml` already invokes
`npx --yes editorconfig-checker@6.1.1` directly with its own pinned version,
never through that file - removing it did not touch CI.

# Related

- [deploy-verify-flow](/process/deploy-verify-flow.md) - step 6 is the process this decision changed the mechanics of, not the substance
- [live-playwright-scripts](/process/live-playwright-scripts.md) - now describes the scratch-script approach this decision put in place

# Citations

- #791
- AGENTS.md (root, "Mandatory: Deploy and verify every bug fix / feature")
- .github/workflows/lint.yml
