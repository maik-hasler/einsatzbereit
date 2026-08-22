# Keycloak Docker Image

## Released image (`Dockerfile`)

Optimized multi-stage image, configured entirely through environment variables at container start. This table documents what each variable is for; whoever runs the image supplies the values.

| Variable | Purpose | Example |
|---|---|---|
| `KC_HOSTNAME` | Public hostname Keycloak is served at | `https://login.example.com` |
| `KC_DB_URL` | JDBC connection string for Keycloak's own database | `jdbc:postgresql://db:5432/keycloak` |
| `KC_DB_USERNAME` | Database username | `keycloak` |
| `KC_DB_PASSWORD` | Database password | `secret` |
| `KC_BOOTSTRAP_ADMIN_USERNAME` | Master-realm admin username | `admin` |
| `KC_BOOTSTRAP_ADMIN_PASSWORD` | Master-realm admin password | - |
| `KC_FRONTEND_URL` | Resolves the `${KC_FRONTEND_URL}` placeholders in the imported realm (the `frontend` client's redirect URI, web origin and post-logout redirect URI) | `https://app.example.com` |
| `KEYCLOAK_BACKEND_SECRET` | Resolves the `${KEYCLOAK_BACKEND_SECRET}` placeholder in the imported realm (the `backend` client's secret) | - |
| `KC_SMTP_HOST` | Resolves the realm's `smtpServer` host placeholder so `verifyEmail`/`resetPasswordAllowed` can send mail | `smtp.example.com` |
| `KC_SMTP_PORT` | SMTP port | `587` |
| `KC_SMTP_FROM` | From-address for outgoing mail | `no-reply@example.com` |
| `KC_SMTP_USER` | SMTP auth username | - |
| `KC_SMTP_PASSWORD` | SMTP auth password | - |
| `KC_PROXY_HEADERS` | Trust `X-Forwarded-*` from the reverse proxy in front of Keycloak | `xforwarded` |

`KEYCLOAK_BACKEND_SECRET` and the `KC_SMTP_*` values must match the backend's own `Keycloak__ClientSecret` and `Smtp__*` settings - see `keycloak/AGENTS.md` for how the realm resolves `${VAR}` placeholders at import time.

## Local development

Local Aspire runs (`dotnet run --project backend/src/Aspire/AppHost`) do not use this image - `AppHost.cs` launches the stock `quay.io/keycloak/keycloak` container directly with `KC_DB=dev-file`, so none of the above applies there.
