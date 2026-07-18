---
type: "process"
title: "Tag-driven releases via a release/* branch"
description: "How releases actually fire: push a release/vX.Y.Z branch (not a tag), which release-rc.yml promotes to a tag; only RC tags deploy to staging."
tags:
  - release
  - ci
  - git-proxy
  - staging
  - sandbox
  - ghcr
timestamp: 2026-07-18
---

# Tag-driven releases via a release/* branch

Releases fire on a **tag push**, but from Claude Code on the web you never push a tag. The git proxy allows pushes to the current working branch only, so you push a branch named `release/vX.Y.Z[-rc.N]` and a workflow promotes it to the tag for you. The chain is: `release/*` branch push -> `release-rc.yml` creates the tag -> `publish.yml` builds images and (for RC tags only) deploys to staging.

## Push a branch, not a tag

`release-rc.yml` triggers on any push to `release/v*`. It strips the `release/` prefix and validates the remainder against `^v[0-9]+\.[0-9]+\.[0-9]+(-rc\.[0-9]+)?$`; a name that doesn't match fails the job. On a valid name it creates an **annotated** tag (`git tag -a`) on the branch HEAD, pushes it, then deletes the `release/*` branch. The tag-creation step is idempotent: if the tag already exists it prints a message and exits 0 rather than failing, so re-pushing the same branch is safe.

Cut the branch from whatever commit you want released (typically the feature branch, so the fix is actually in it), carry it with an empty commit, and push:

```bash
git checkout -b release/v1.2.3-rc.1 <feature-branch>
git commit --allow-empty -m "release: v1.2.3-rc.1"
git push -u origin release/v1.2.3-rc.1
```

## RELEASE_TOKEN, not GITHUB_TOKEN

`release-rc.yml` checks out and pushes the tag using the repo secret `RELEASE_TOKEN`, a fine-grained PAT scoped to this repo with `contents: write`. This is load-bearing, not a preference. A tag pushed with the built-in `GITHUB_TOKEN` does **not** trigger other workflows; GitHub blocks that to prevent workflow loops. If the tag were pushed with `GITHUB_TOKEN`, `publish.yml` would silently never run and the chain would break with no error. Without `RELEASE_TOKEN` configured, `release-rc.yml` fails at checkout. This is the one-time setup the repo owner must do.

## What publish.yml builds

`publish.yml` triggers on **unprefixed** tags: `v[0-9]*.[0-9]*.[0-9]*` and `v[0-9]*.[0-9]*.[0-9]*-rc.[0-9]*`. It builds all three images together in one run: backend, frontend, and keycloak, all pushed to GHCR. Each job derives `prerelease` from whether the tag contains `-rc.`, and tags the image accordingly:

- Every tag: the raw version (e.g. `1.2.3` or `1.2.3-rc.1`).
- Full release (`prerelease=false`): also tags `latest`.
- RC (`prerelease=true`): also tags `staging`, and skips `latest`.

The backend job reruns the full test suite (Application.UnitTests, ArchitectureTests, IntegrationTests, VisualTests) before pushing, so a release can still fail here on the same IntegrationTests behaviour that bites in CI.

## Only RC tags reach staging

`deploy-staging` needs all three publish jobs and gates on `if: needs.publish-backend.outputs.prerelease == 'true'` (only `publish-backend` exports the `prerelease` output). So a full `vX.Y.Z` release publishes and tags images `latest` but **does not deploy anywhere**. Cutting an `-rc.N` is the only path that pushes code to the live staging site, runs the post-deploy health gate against `https://api.maik-hasler.de/health`, and rolls back on failure. To verify a change on the live site you must cut an RC, not a full release.

## Docs drift warning

`.github/AGENTS.md` is stale on two points; trust the workflow files, not the doc:

- Its "Publish Workflows" table describes **component-prefixed, independently released** tags (`backend/vX.Y.Z`, `frontend/vX.Y.Z`, `keycloak/vX.Y.Z.W`). That scheme is gone. `publish.yml` triggers on a single unprefixed `vX.Y.Z` tag and builds all three components in one run.
- Its workflow list shows 7 files but the repo has 9 (`keycloak-realm-import.yml` and `security.yml` are omitted).

# Related

- [deploy-verify-flow](/process/deploy-verify-flow.md) - this workflow is invoked by the mandatory verify steps 3-5
- [sandbox-limitations](/gotchas/sandbox-limitations.md) - the git proxy tag-push block is why the release/* branch workaround exists
- [ci-traps](/ci/ci-traps.md) - the mandatory 'CI green before release' gate runs into the IntegrationTests hang described there

# Citations

- .github/AGENTS.md (Releases + Publish Workflows)
- .github/workflows/release-rc.yml
- .github/workflows/publish.yml
- AGENTS.md (root, 'Releases')
