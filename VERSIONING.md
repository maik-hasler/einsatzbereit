# Versioning & Publishing Strategy

Dieses Projekt ist ein Monorepo mit unabhängig versionierten Komponenten.
Jede Komponente wird als eigenes Docker Image über **GitHub Container Registry (ghcr.io)** publiziert.

## Tag-Format

Releases werden über Git-Tags ausgelöst. Jede Komponente hat ein eigenes Tag-Prefix:

| Komponente | Tag-Pattern              | Image                                        |
|------------|--------------------------|----------------------------------------------|
| Keycloak   | `keycloak/v<version>`    | `ghcr.io/<owner>/einsatzbereit-keycloak`     |
| Frontend   | `frontend/v<version>`    | `ghcr.io/<owner>/einsatzbereit-frontend`     |
| Backend    | `backend/v<version>`     | `ghcr.io/<owner>/einsatzbereit-backend`      |

## Versionierungsschema

### Keycloak

Format: `<upstream>.<patch>`

Die ersten drei Stellen entsprechen der verwendeten Keycloak-Upstream-Version.
Die vierte Stelle ist die eigene Patch-Version für Änderungen an Konfiguration, Realm-Exports oder Dockerfile.

Beispiele:
- `keycloak/v26.5.6.1` - Erster eigener Build auf Basis von Keycloak 26.5.6
- `keycloak/v26.5.6.2` - Zweite Änderung (z.B. neue Realm-Config)
- `keycloak/v26.5.6.2-rc.1` - Release Candidate für Patch 2
- `keycloak/v27.0.0.1` - Upgrade auf Keycloak 27.0.0

### Frontend & Backend

Standard [SemVer](https://semver.org/):

- `frontend/v1.0.0`
- `backend/v0.1.0-rc.1`

## Prerelease-Tags

Alle Komponenten unterstützen `-rc.N` als einziges Prerelease-Suffix:

- `keycloak/v26.5.6.2-rc.1`
- `frontend/v1.0.0-rc.1`
- `backend/v0.1.0-rc.1`

Prerelease-Tags erzeugen Docker Images, die **nicht** als `latest` getaggt werden.

## Workflow

1. Änderungen auf `main` mergen
2. Tag setzen: `git tag keycloak/v26.5.6.1`
3. Tag pushen: `git push origin keycloak/v26.5.6.1`
4. GitHub Actions baut und pusht das Docker Image automatisch
