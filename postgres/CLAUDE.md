# Postgres — Database Init

```
postgres/
└── init-databases.sh   Creates application databases on first container startup
```

## What it does

`init-databases.sh` runs once when the PostgreSQL container initializes (Docker init script convention). Creates two databases:

- `keycloak` — Keycloak's internal storage (users, clients, sessions, realm config)
- `einsatzbereit` — Application data (managed by EF Core migrations)

## Schema

Application schema is managed by EF Core migrations in `backend/src/Infrastructure/Persistence/Migrations/`. The init script only creates the empty databases.

Keycloak manages its own schema on first startup.

## Connection strings

| Consumer | Connection |
|---|---|
| Backend (EF Core) | `Host=postgres;Database=einsatzbereit;Username=postgres;Password=postgres` |
| Keycloak | `jdbc:postgresql://postgres:5432/keycloak` |
| pgAdmin (dev) | http://localhost:5050, credentials: admin@admin.com / admin |
