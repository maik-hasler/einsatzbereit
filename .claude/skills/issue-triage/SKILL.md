---
name: issue-triage
description: >
  The recurring issue-triage-and-implement loop for this repo: review open
  issues and PRs, pick something actionable and not already covered, implement
  it, open a PR and verify it via a release candidate for the repository
  owner to review and merge themselves, and document the outcome on the
  issue. Use when asked to review/triage open issues, when running the
  recurring routine for this repo, or when told to "check open issues and
  implement one".
---

# Issue triage and implement

## 1. Survey

Review all open issues and all open PRs before starting any work.

For each open issue:

- Determine whether it's still relevant and actionable.
- Skip anything labelled `needs-decision` entirely - it's a finding that
  needs the repo owner's own product/design call before it can be
  implemented (see `persona-simulation`). Don't triage it, don't implement
  it, don't comment on it beyond what's already there. It stays untouched
  until the owner removes the label.
- Check open PRs for related work - if one already resolves, partially
  resolves, or clearly addresses it, don't duplicate the work. Document the
  relationship in a comment and move on.
- Multiple issues solvable through one coherent change may be addressed
  together.
- Prefer an issue that's already been triaged as actionable across multiple
  prior cycles without being picked up, over re-triaging the same backlog
  indefinitely and always reaching for something newer/shinier.
- If, after all of the above, no open issue is actionable (everything
  remaining is `needs-decision`, bot-managed like Renovate's Dependency
  Dashboard, or already covered by an open PR), run the `persona-simulation`
  skill instead of stopping here - it's the fallback for a genuinely empty
  backlog, not a replacement for real triage work.

## 2. Implement

- Always work from the latest state of `main`.
- Implement on a dedicated working branch and open a PR into `main`. **Never
  merge the PR yourself, under any circumstances** - merging is the
  repository owner's decision alone, regardless of how green CI is or how
  clean the live-verification result looks. (Direct pushes to `main` are
  also blocked in this sandbox anyway - see root `CLAUDE.md`, "Sandbox
  Limitations" - but the point is broader than that restriction: even where
  merging were possible, it's still not this routine's call to make.)
- **Never use a GitHub closing keyword** (`Fixes`/`Closes`/`Resolves`, any
  tense, case-insensitive) followed by an issue number, in the PR title,
  body, or any commit message. Use non-linking phrasing instead - `Addresses
  #NNN`, `Relates to #NNN`. A closing keyword auto-closes the issue the
  instant the owner merges, turning issue closure into an automatic side
  effect instead of the deliberate, separate action it must always be.
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

- Cut the release-candidate branch from the **feature/PR branch itself**
  (see "Mandatory: Deploy and verify" for the exact steps) so live
  verification happens before the repository owner ever needs to look at
  it. Do not wait for, or perform, a merge into `main` first - the PR stays
  open throughout this entire step.
- Once the RC deploys and live verification passes, the PR is ready for the
  owner's review, nothing more. This routine never merges it.

## 5. Document, don't spam

For every issue investigated this cycle, leave one concise comment
covering: what was analyzed, whether it was reproduced, whether an open PR
already covers it, what changed (if anything - link the PR and note it's
open and awaiting the repository owner's review, since this routine never
merges it), how it was validated (including the release-candidate
live-verification result), and any remaining limitations/follow-up.

Skip leaving a new comment if an existing comment from a recent prior cycle
already says the same thing with nothing new to add - re-confirm the
decision to yourself, don't restate it on the issue. If the repository owner
has explicitly asked for no further comments on an issue, honor that
indefinitely, not just for the cycle it was said in.

Never close issues - that's the repository owner's call.
