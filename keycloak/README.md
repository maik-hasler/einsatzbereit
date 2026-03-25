# Keycloak Docker Image

## Production (`Dockerfile`)

Optimiertes Multi-stage Image. Folgende Umgebungsvariablen müssen zur Laufzeit übergeben werden:

| Variable | Beispiel |
|---|---|
| `KC_HOSTNAME` | `auth.example.com` |
| `KC_DB_URL` | `jdbc:postgresql://db:5432/keycloak` |
| `KC_DB_USERNAME` | `keycloak` |
| `KC_DB_PASSWORD` | `secret` |
