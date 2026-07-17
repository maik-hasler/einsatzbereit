@AGENTS.md

## Cutting a release from Claude Code on the web

The Claude Code on the web git proxy restricts pushes to the current working branch only - tag refs cannot be pushed directly from the sandbox. To stay autonomous, use the `release-rc.yml` promotion workflow instead of asking the user to push a tag.

**The flow (Claude does this):**

```bash
# 1. From an up-to-date main, branch with the release name as the suffix.
git checkout -b release/v1.2.3-rc.1 main

# 2. Empty commit (or any commit on this branch) carries the push.
git commit --allow-empty -m "release: v1.2.3-rc.1"

# 3. Push the branch - sandbox allows this because it is the working branch.
git push -u origin release/v1.2.3-rc.1
```

`release-rc.yml` then:
1. Validates the suffix against `^v[0-9]+\.[0-9]+\.[0-9]+(-rc\.[0-9]+)?$`.
2. Creates an annotated tag with the same name on the branch's HEAD.
3. Pushes the tag using `RELEASE_TOKEN` (so `publish.yml` fires - see below).
4. Deletes the `release/...` branch.

After the tag exists, `publish.yml` runs end-to-end (build → GHCR → `deploy-staging` for RC tags).

**One-time setup the user must do:**

- Create a fine-grained Personal Access Token scoped to this repo with `contents: write`.
- Add it as a **repository secret** named `RELEASE_TOKEN`.

A PAT (not the default `GITHUB_TOKEN`) is mandatory because tags pushed with `GITHUB_TOKEN` do not trigger downstream workflows - GitHub explicitly prevents that to avoid workflow loops. Without `RELEASE_TOKEN`, `release-rc.yml` will fail at checkout.

**After pushing the branch:**

1. Poll the publish workflow's checks for the new tag (via `mcp__github__get_commit` → check_runs, or fetch `https://api.github.com/repos/{owner}/{repo}/commits/{sha}/check-runs`).
2. Once `deploy-staging` reports success, smoke-test live: `curl https://api.maik-hasler.de/health`, then HEAD-check `https://einsatzbereit.maik-hasler.de`.
3. If any publish job fails, diagnose from logs; if the deploy step itself fails, the SSH/GHCR secrets in the `staging` GitHub Environment are the most likely cause.
