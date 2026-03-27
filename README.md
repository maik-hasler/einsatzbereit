# Einsatzbereit
Einsatzbereit bringt engagierte Helfer:innen mit regionalen Bedarfen zusammen.
Spontan, schnell und wirksam.

## Motivation
Wer helfen will, muss sich nicht langfristig binden.
Manchmal passt ein Nachmittag, manchmal eine Woche.
Aber einen Überblick zu bekommen, wo gerade Hilfe gebraucht wird, ist oft schwer.
Egal ob bei großen NGOs oder beim Sportturnier um die Ecke.
Bestehende Plattformen sind zu kompliziert, zu allgemein oder werden kaum genutzt.
Einsatzbereit zeigt konkrete Bedarfe: was, wo, wann.

## Lokale Entwicklung

### Voraussetzungen

- [Docker](https://docs.docker.com/get-docker/) mit Docker Compose

### Starten

```bash
docker compose up --build
```

Beim ersten Start werden die Datenbanken automatisch angelegt und Keycloak-Realms importiert.

### Nützliche Befehle

```bash
docker compose up --build          # Alle Services starten (mit Rebuild)
docker compose up -d               # Im Hintergrund starten
docker compose down                # Alles stoppen
docker compose down -v             # Stoppen + Volumes löschen (Reset)
docker compose logs -f <service>   # Logs eines Services verfolgen
docker compose restart <service>   # Einzelnen Service neustarten
```

### Services

| Service    | URL                        | Credentials                              |
|------------|----------------------------|------------------------------------------|
| Frontend   | http://localhost:4321      | –                                        |
| Backend    | http://localhost:5000      | –                                        |
| Keycloak   | http://localhost:8080      | `admin` / `admin`                        |
| pgAdmin    | http://localhost:5050      | `admin@admin.com` / `admin`              |
| PostgreSQL | `localhost:5432`           | `postgres` / `postgres`                  |

### Testbenutzer

| Benutzer | Passwort     | Rollen              | Persona              | Kann                          |
|----------|--------------|---------------------|----------------------|-------------------------------|
| `hannah` | `hannah123`  | `user`              | Helferin Hannah      | Bedarfe ansehen               |
| `olaf`   | `olaf123`    | `user`, `organisator` | Organisator Olaf   | Bedarfe ansehen und erstellen |
| `admin`  | `admin123`   | `admin`             | Administrator        | Administration                |

### Datenbanken

| Datenbank       | Zweck              |
|-----------------|--------------------|
| `einsatzbereit` | Applikationsdaten  |
| `keycloak`      | Keycloak-Daten     |

pgAdmin ist beim Start bereits mit dem PostgreSQL-Server verbunden.

## Mitmachen
Lies die [Hinweise zum Mitwirken](CONTRIBUTING.md) und unseren [Verhaltenskodex](CODE_OF_CONDUCT.md), um loszulegen.

## Lizenz
Einsatzbereit ist bewusst unter [GNU Affero General Public License v3.0](LICENSE) lizenziert.
Es ist zweitrangig, wer das Projekt weiterentwickelt.
Der Nutzen soll im Vordergrund stehen.
Kein Profit, kein closed source, kein verlorenes Wissen.
