# Versioning & Publishing Strategy

This project is a monorepo with a unified versioning strategy.
All components are published together as Docker images via **GitHub Container Registry (ghcr.io)**.

## Tag Format

A single Git tag triggers the release of all components:

| Tag Pattern                        | Description       |
|------------------------------------|-------------------|
| `v<major>.<minor>.<patch>`         | Stable release    |
| `v<major>.<minor>.<patch>-rc.<n>` | Release candidate |

Examples:
- `v1.0.0` - First stable release
- `v1.1.0-rc.1` - Release candidate for version 1.1.0

## Published Images

Every component receives the same version tag:

| Component | Image                                        |
|-----------|----------------------------------------------|
| Backend   | `ghcr.io/<owner>/einsatzbereit-backend`      |
| Frontend  | `ghcr.io/<owner>/einsatzbereit-frontend`     |
| Keycloak  | `ghcr.io/<owner>/einsatzbereit-keycloak`     |

## Versioning Scheme

Standard [SemVer](https://semver.org/) for all components:

- **MAJOR**: Breaking change in any component (e.g. incompatible API change)
- **MINOR**: New functionality, backwards-compatible
- **PATCH**: Bug fix or minor configuration change

## Keycloak Upstream Version

Since the Keycloak image is based on a specific upstream release, the upstream version is
embedded as an OCI label in the image:

```
org.opencontainers.image.base.version=<keycloak-upstream-version>
```

The upstream version in use can be read directly from `keycloak/Dockerfile` (first `FROM` line).
When upgrading Keycloak, the upstream version is automatically extracted from the Dockerfile
and written into the image labels.

## Prerelease Tags

See the Tag Format table and Examples above for the `-rc.<n>` suffix. Unlike stable tags, prerelease tags produce Docker images that are **not** tagged as `latest`.

## Workflow

1. Merge changes into `main`
2. Set tag: `git tag v1.0.0`
3. Push tag: `git push origin v1.0.0`
4. GitHub Actions builds and pushes all three Docker images automatically

## Release Notes

Every tag (stable and `-rc.N`) gets a [GitHub Release](https://github.com/maik-hasler/einsatzbereit/releases)
with auto-generated notes grouped by Conventional Commit type (Features, Bug
Fixes, Performance, Refactoring, Documentation, Reverts, and any `!`-marked
Breaking Changes) since the previous tag. Release candidates are marked as
prereleases. This is the canonical human-readable record of what shipped in
each version - there is no separate `CHANGELOG.md` file to keep in sync.
