# Versioning & Publishing Strategy

Dieses Projekt ist ein Monorepo mit einheitlicher Versionierung.
Alle Komponenten werden gemeinsam als Docker Images über **GitHub Container Registry (ghcr.io)** publiziert.

## Tag-Format

Ein einziger Git-Tag loest den Release aller Komponenten aus:

| Tag-Pattern           | Beschreibung              |
|-----------------------|---------------------------|
| `v<major>.<minor>.<patch>` | Stabiler Release     |
| `v<major>.<minor>.<patch>-rc.<n>` | Release Candidate |

Beispiele:
- `v1.0.0` - Erster stabiler Release
- `v1.1.0-rc.1` - Release Candidate fuer Version 1.1.0

## Publizierte Images

Jede Komponente bekommt dasselbe Versions-Tag:

| Komponente | Image                                        |
|------------|----------------------------------------------|
| Backend    | `ghcr.io/<owner>/einsatzbereit-backend`      |
| Frontend   | `ghcr.io/<owner>/einsatzbereit-frontend`     |
| Keycloak   | `ghcr.io/<owner>/einsatzbereit-keycloak`     |

## Versionierungsschema

Standard [SemVer](https://semver.org/) fuer alle Komponenten:

- **MAJOR**: Breaking change in einer Komponente (z.B. inkompatible API-Aenderung)
- **MINOR**: Neue Funktionalitaet, abwaertskompatibel
- **PATCH**: Bugfix oder kleinere Konfigurationsaenderung

## Keycloak-Upstream-Version

Da das Keycloak-Image auf einem bestimmten Upstream-Release basiert, wird die
Upstream-Version als OCI-Label in das Image eingebettet:

```
org.opencontainers.image.base.version=<keycloak-upstream-version>
```

Die verwendete Upstream-Version ist direkt dem `keycloak/Dockerfile` zu entnehmen
(erste `FROM`-Zeile). Bei einem Keycloak-Upgrade wird die Upstream-Version
automatisch aus dem Dockerfile ausgelesen und in den Image-Labels gesetzt.

## Prerelease-Tags

Release Candidates werden mit `-rc.<n>` als Suffix gekennzeichnet:

- `v1.0.0-rc.1`
- `v1.0.0-rc.2`

Prerelease-Tags erzeugen Docker Images, die **nicht** als `latest` getaggt werden.

## Workflow

1. Aenderungen auf `main` mergen
2. Tag setzen: `git tag v1.0.0`
3. Tag pushen: `git push origin v1.0.0`
4. GitHub Actions baut und pusht alle drei Docker Images automatisch

