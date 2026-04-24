# Migration: Aspire + TUnit

Vollständige Migration von docker-compose → Aspire, xUnit → TUnit, TypeScript-Playwright → C# VisualTests.

## Phase 1 — Aspire-Infrastruktur

- [x] `backend/src/Aspire/ServiceDefaults/ServiceDefaults.csproj` anlegen
- [x] `backend/src/Aspire/ServiceDefaults/Extensions.cs` anlegen (OTel, Health, ServiceDiscovery)
- [x] `backend/src/Aspire/AppHost/AppHost.csproj` anlegen
- [x] `backend/src/Aspire/AppHost/Program.cs` anlegen (PostgreSQL, Keycloak, Backend, Frontend)
- [x] `backend/src/Api/Api.csproj`: ServiceDefaults-Referenz hinzufügen
- [x] `backend/src/Api/Program.cs`: `AddServiceDefaults()` integrieren, `DockerRewriteHandler` entfernen
- [x] `backend/Directory.Packages.props`: Aspire + TUnit + OTel Pakete hinzufügen, xUnit entfernen
- [x] `backend/global.json`: Microsoft.Testing.Platform Runner konfigurieren
- [x] `backend/Einsatzbereit.slnx`: neue Projekte eintragen
- [x] `docker-compose.yml` löschen

## Phase 2 — TUnit Migration

- [ ] `Application.UnitTests.csproj`: xUnit → TUnit
- [ ] `Application.UnitTests/**/*.cs`: `[Fact]` → `[Test]`, `using Xunit` entfernen
- [ ] `ArchitectureTests.csproj`: xUnit → TUnit
- [ ] `ArchitectureTests/**/*.cs`: `[Fact]` → `[Test]`
- [ ] `IntegrationTests.csproj`: xUnit + WAF + Testcontainers → TUnit + `Aspire.Hosting.Testing`
- [ ] `IntegrationTestFixture.cs`: neu auf Basis von `DistributedApplicationTestingBuilder`
- [ ] `IntegrationTestCollection.cs`: löschen (nicht mehr nötig)
- [ ] `IntegrationTests/**/*.cs`: `[Collection]` → `[ClassDataSource<T>]`, `[Fact]` → `[Test]`, `IAsyncLifetime` → `IAsyncInitializer`

## Phase 3 — VisualTests (neu)

- [ ] `backend/tests/VisualTests/VisualTests.csproj` anlegen (TUnit.Playwright + Aspire.Hosting.Testing)
- [ ] `AspireFixture.cs` anlegen (startet Full-Stack via Aspire)
- [ ] `AuthHelper.cs` anlegen (Login via Playwright, Keycloak-Flow)
- [ ] `SmokeTests.cs` anlegen
- [ ] `NavigationTests.cs` anlegen
- [ ] `AuthGuardTests.cs` anlegen
- [ ] `OrganizationTests.cs` anlegen
- [ ] `VolunteerOpportunityTests.cs` anlegen
- [ ] `frontend/tests/` komplett löschen (global-setup.ts, e2e/**, helpers/, playwright.config.ts)
- [ ] `frontend/package.json`: `@playwright/test` entfernen

## Phase 4 — CI/CD

- [ ] `.github/workflows/dotnet.yml`: auf Microsoft.Testing.Platform anpassen
- [ ] `.github/workflows/frontend.yml`: E2E-Job entfernen

## Dev-Workflow nach Migration

```bash
# Lokale Entwicklung
dotnet run --project backend/src/Aspire/AppHost

# Tests
dotnet test backend/Einsatzbereit.slnx

# Visuelle Tests (manuell)
dotnet run --project backend/tests/VisualTests -- --explicit
```
