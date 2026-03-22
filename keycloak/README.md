# Keycloak Docker Images

## Production (`Dockerfile`)

Optimiertes Multi-stage Image. Folgende Umgebungsvariablen müssen zur Laufzeit übergeben werden:

| Variable | Beispiel |
|---|---|
| `KC_HOSTNAME` | `auth.example.com` |
| `KC_DB_URL` | `jdbc:postgresql://db:5432/keycloak` |
| `KC_DB_USERNAME` | `keycloak` |
| `KC_DB_PASSWORD` | `secret` |

## Integrationstests (`Dockerfile.integration`)

Dev-Mode Image mit automatischem Realm-Import. `KC_HOSTNAME` ist **nicht** erforderlich.

Realm-JSON-Dateien im Verzeichnis `realms/` werden beim Start automatisch importiert. Die DB-Variablen (`KC_DB_URL`, `KC_DB_USERNAME`, `KC_DB_PASSWORD`) müssen ebenfalls gesetzt werden.
