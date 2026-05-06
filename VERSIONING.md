# Versioning & Publishing Strategy

This project is a monorepo with independently versioned components.
Each component is published as its own Docker image via **GitHub Container Registry (ghcr.io)**.

## Tag Format

Releases are triggered via Git tags. Each component has its own tag prefix:

| Component | Tag Pattern              | Image                                        |
|-----------|--------------------------|----------------------------------------------|
| Keycloak  | `keycloak/v<version>`    | `ghcr.io/<owner>/einsatzbereit-keycloak`     |
| Frontend  | `frontend/v<version>`    | `ghcr.io/<owner>/einsatzbereit-frontend`     |
| Backend   | `backend/v<version>`     | `ghcr.io/<owner>/einsatzbereit-backend`      |

## Versioning Scheme

### Keycloak

Format: `<upstream>.<patch>`

The first three parts match the Keycloak upstream version in use.
The fourth part is the internal patch version for changes to configuration, realm exports, or Dockerfile.

Examples:
- `keycloak/v26.5.6.1` - First internal build based on Keycloak 26.5.6
- `keycloak/v26.5.6.2` - Second change (e.g. new realm config)
- `keycloak/v26.5.6.2-rc.1` - Release candidate for patch 2
- `keycloak/v27.0.0.1` - Upgrade to Keycloak 27.0.0

### Frontend & Backend

Standard [SemVer](https://semver.org/):

- `frontend/v1.0.0`
- `backend/v0.1.0-rc.1`

## Prerelease Tags

All components support `-rc.N` as the only prerelease suffix:

- `keycloak/v26.5.6.2-rc.1`
- `frontend/v1.0.0-rc.1`
- `backend/v0.1.0-rc.1`

Prerelease tags produce Docker images that are **not** tagged as `latest`.

## Workflow

1. Merge changes into `main`
2. Set tag: `git tag keycloak/v26.5.6.1`
3. Push tag: `git push origin keycloak/v26.5.6.1`
4. GitHub Actions builds and pushes the Docker image automatically
