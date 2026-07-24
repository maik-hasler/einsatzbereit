---
type: "gotcha"
title: "What the Claude Code web sandbox cannot do"
description: "No reliable Docker, git proxy blocks main and tag pushes, and gh/mcp GitHub tools are unavailable - use WebFetch for the tracker."
tags:
  - sandbox
  - git-proxy
  - release
  - deploy-verify
  - playwright
timestamp: 2026-07-18
---

# What the Claude Code web sandbox cannot do

Limits of a Claude Code web/cloud session that either block a command outright or fail after looking like they worked. Each one has burned time when discovered mid-task instead of up front.

# Docker runs but Aspire does not

`docker info` can return success and you still cannot run anything real. Container networking is not reliable enough for Aspire/DCP orchestration, which fails after the daemon reports healthy. That false green is the trap: the failure looks like an app bug, so the time goes into debugging the app rather than accepting the sandbox limit. Three things depend on this and cannot run locally here:

- `dotnet run --project backend/src/Aspire/AppHost` (the full stack)
- the `IntegrationTests` project (Aspire.Hosting.Testing / DCP via AppHost)
- the `VisualTests` project (Aspire + Playwright)

Do not retry them against a passing `docker info`.

# What does run locally

The Docker-free verify subset is `dotnet build`, the `Application.UnitTests` project, and the `ArchitectureTests` project. Run those before finishing. The full suite, `IntegrationTests` and `VisualTests` included, runs on a real runner in CI's `dotnet.yml`. Because the local dev server cannot come up, verify anything user-visible on live staging instead (see deploy-verify-flow).

# Git proxy is working-branch only

The proxy blocks two push targets: `main` and any tag. Both blocks hold even when an instruction says to work "directly on main" or to push a release tag - the instruction does not override the proxy. Commit to the assigned `claude/...` branch and open a PR. For a release, push a `release/vX.Y.Z[-rc.N]` branch and let the workflow create the tag (see release-workflow).

# No gh, no GitHub MCP tools

`gh` is not installed and the `mcp__github__*` tools are not available in a web/cloud session, so the tag, check-run, and issue calls that AGENTS.md references elsewhere cannot be made here. The repo is public, so read issues, PRs, and labels over `WebFetch` against the public GitHub URLs. `WebFetch` cannot reach authenticated endpoints, which rules out creating an issue or reading a PR's check runs; for those, push a branch and let CI and the workflows act on your behalf.

# Related
- [deploy-verify-flow](/process/deploy-verify-flow.md) - no local run is why user-visible changes must be verified on live staging instead
- [release-workflow](/process/release-workflow.md) - the tag-push block is the reason releases go through a release/* branch
- [live-playwright-scripts](/process/live-playwright-scripts.md) - the egress-proxy TLS trap the launch args work around is a sandbox effect
- [backend-conventions](/reference/backend-conventions.md) - the IntegrationTests/VisualTests that need DCP orchestration are the ones that cannot run here

# Citations
- AGENTS.md (root, 'Sandbox Limitations')
- AGENTS.md:113
- AGENTS.md:114
- https://github.com/maik-hasler/einsatzbereit
