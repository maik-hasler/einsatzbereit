---
type: decision-note
title: Release RCs are pushed as branches, not tags, because the sandbox's git proxy blocks tag pushes
description: Claude Code on the web can only push its current working branch, so a release/vX.Y.Z-rc.N branch stands in for a tag push, and a workflow promotes it to a real tag using a dedicated RELEASE_TOKEN PAT.
tags: [ci-cd, releases, sandbox]
timestamp: 2026-07-16
---

# Schema

When an execution environment can only push the current working branch (no direct tag pushes), a release flow can still get to a real tag: push a branch named after the intended tag, and let a workflow running with elevated permissions create and push the actual tag from that branch's HEAD. The workflow needs a personal access token rather than the default `GITHUB_TOKEN`, because GitHub deliberately prevents tags pushed with `GITHUB_TOKEN` from triggering downstream workflows, to avoid workflow-triggering-workflow loops.

# Examples

`release-rc.yml` validates a pushed `release/vX.Y.Z[-rc.N]` branch name against `^v[0-9]+\.[0-9]+\.[0-9]+(-rc\.[0-9]+)?$`, creates an annotated tag on that branch's HEAD, pushes it using a repository secret named `RELEASE_TOKEN` (so `publish.yml` actually fires), then deletes the `release/...` branch. Without `RELEASE_TOKEN`, the workflow fails at checkout. One-time setup: a fine-grained PAT scoped to the repo with `contents: write`, added as that secret.

# Citations

- `.github/CLAUDE.md` (Cutting a release from Claude Code on the web section)
- `CLAUDE.md` (Releases section)
