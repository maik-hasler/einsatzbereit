# Einsatzbereit
Einsatzbereit connects engaged volunteers with regional needs.
Spontaneously, quickly, and effectively.

## Motivation
Volunteering doesn't require a long-term commitment.
Sometimes an afternoon is enough, sometimes a week.
But finding out where help is currently needed is often difficult -
whether at large NGOs or a local sports tournament.
Existing platforms are too complex, too generic, or barely used.
Einsatzbereit makes concrete needs visible: what, where, when.

## Local Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://docs.docker.com/get-docker/) (Aspire spins up Postgres + Keycloak as containers)
- [pnpm](https://pnpm.io/installation) (frontend package manager)

### Starting Up

```bash
dotnet run --project backend/src/Aspire/AppHost
```

The Aspire AppHost provisions PostgreSQL, Keycloak, the backend API, and the Vite frontend. The Aspire dashboard prints service URLs on startup. On first start, databases are created automatically and the Keycloak realm is imported.

### Services

| Service    | URL                        | Credentials                              |
|------------|----------------------------|------------------------------------------|
| Frontend   | http://localhost:4321      | -                                        |
| Backend    | http://localhost:5000      | -                                        |
| Keycloak   | http://localhost:8080      | `admin` / `admin`                        |
| pgAdmin    | http://localhost:5050      | `admin@admin.com` / `admin`              |
| PostgreSQL | `localhost:5432`           | `postgres` / `postgres`                  |

### Test Users

| Username | Password     | Roles                 | Persona              | Can                                  |
|----------|--------------|-----------------------|----------------------|--------------------------------------|
| `hannah` | `hannah123`  | `user`                | Volunteer Hannah     | Browse volunteer opportunities       |
| `olaf`   | `olaf123`    | `user`, `organisator` | Organizer Olaf       | Browse and create opportunities      |
| `admin`  | `admin123`   | `admin`               | Administrator        | Full administration                  |

### Databases

| Database        | Purpose                 |
|-----------------|-------------------------|
| `einsatzbereit` | Application data        |
| `keycloak`      | Keycloak internal data  |

pgAdmin is pre-connected to the PostgreSQL server on startup.

## Contributing
Read the [Contributing Guidelines](CONTRIBUTING.md) and our [Code of Conduct](CODE_OF_CONDUCT.md) to get started.

## License
Einsatzbereit is intentionally licensed under the [GNU Affero General Public License v3.0](LICENSE).
It doesn't matter who develops the project further.
The benefit to society comes first.
No profit, no closed source, no lost knowledge.
