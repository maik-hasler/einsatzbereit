---
name: issue-triage
description: >
  The recurring issue-triage-and-implement loop for this repo: review open
  issues and PRs, pick something actionable and not already covered, implement
  it, release it, and document the outcome on the issue. Use when asked to
  review/triage open issues, when running the recurring routine for this
  repo, or when told to "check open issues and implement one".
---

# Issue triage and implement

## 1. Survey

Review all open issues and all open PRs before starting any work.

For each open issue:

- Determine whether it's still relevant and actionable.
- Check open PRs for related work - if one already resolves, partially
  resolves, or clearly addresses it, don't duplicate the work. Document the
  relationship in a comment and move on.
- Multiple issues solvable through one coherent change may be addressed
  together.
- Prefer an issue that's already been triaged as actionable across multiple
  prior cycles without being picked up, over re-triaging the same backlog
  indefinitely and always reaching for something newer/shinier.

## 2. Implement

- Always work from the latest state of `main`.
- Implement on a dedicated working branch and open a PR into `main` -
  direct pushes to `main` are blocked in this sandbox (see root `CLAUDE.md`,
  "Sandbox Limitations"). "Directly on main" means "merged into main via
  PR", not a direct push.
- Follow existing project conventions and architecture. Smallest reasonable
  change that fully resolves the issue.
- Update or add tests where appropriate. Run `/self-review` before opening
  the PR.
- Only create a commit if a meaningful code/config/test change was made. If
  no actionable issues remain, or everything is already covered by an open
  PR, make no changes and no commits.

## 3. Validate

- Test the change against the live application before considering it done.
- Docker/Aspire (`IntegrationTests`, `VisualTests`, the AppHost) is not
  reliably available in this sandbox - see root `CLAUDE.md`, "Sandbox
  Limitations". Validate locally with what doesn't need Docker (build,
  `Application.UnitTests`, `ArchitectureTests`); rely on CI for the full
  suite; do live-application verification via the release-candidate +
  staging Playwright flow in the "Mandatory: Deploy and verify" section.
- If the issue can't be reproduced, lacks enough information, or can't be
  resolved safely, document that finding instead of making a speculative
  change.

## 4. Release

- Every time a PR is merged into `main`, immediately create a
  release-candidate branch from that merge commit (see "Mandatory: Deploy
  and verify" for the exact steps) - only after the merge commit has
  actually landed and the branch push has succeeded.

## 5. Document, don't spam

For every issue investigated this cycle, leave one concise comment
covering: what was analyzed, whether it was reproduced, whether an open PR
already covers it, what changed (if anything), how it was validated, and
any remaining limitations/follow-up.

Skip leaving a new comment if an existing comment from a recent prior cycle
already says the same thing with nothing new to add - re-confirm the
decision to yourself, don't restate it on the issue. If the repository owner
has explicitly asked for no further comments on an issue, honor that
indefinitely, not just for the cycle it was said in.

Never close issues - that's the repository owner's call.
