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

- [x] `Application.UnitTests.csproj`: xUnit → TUnit
- [x] `Application.UnitTests/**/*.cs`: `[Fact]` → `[Test]`, `using Xunit` entfernen
- [x] `ArchitectureTests.csproj`: xUnit → TUnit
- [x] `ArchitectureTests/**/*.cs`: `[Fact]` → `[Test]`
- [x] `IntegrationTests.csproj`: xUnit + WAF + Testcontainers → TUnit + `Aspire.Hosting.Testing`
- [x] `IntegrationTestFixture.cs`: neu auf Basis von `DistributedApplicationTestingBuilder`
- [x] `IntegrationTestCollection.cs`: löschen (nicht mehr nötig)
- [x] `IntegrationTests/**/*.cs`: `[Collection]` → `[ClassDataSource<T>]`, `[Fact]` → `[Test]`, `IAsyncLifetime` → `IAsyncInitializer`
- [ ] `IntegrationTests`: ausführen und ergebnis auf korrekt prüfen andernfalls fixen

## Phase 2.1 — Echte Keycloak-Instanz für IntegrationTests

- [x] `AppHost.cs`: `TestMode`-Branch entfernen, Keycloak immer starten
- [x] `Api/Program.cs`: `TestSigningKey`-Auth-Branch entfernen
- [x] `Infrastructure/ServiceCollectionExtensions.cs`: InMemory-Fallback entfernen
- [x] `Infrastructure/Keycloak/InMemoryKeycloakOrganizationService.cs`: löschen
- [x] `keycloak/realms/einsatzbereit-realm.json`: feste `id` für hannah/olaf/admin
- [x] `IntegrationTestFixture`: Password-Grant gegen `frontend`-Client statt selbst-signierter JWTs
- [x] `IntegrationTestFixture`: `ResetKeycloakOrganizationsAsync` via Backend-Service-Account
- [x] Tests: `Before(Test)`-Hook ruft `fixture.ResetAsync()` (DB + Keycloak Orgs)
- [x] Test-Klassen `[NotInParallel("IntegrationDb")]` — verhindern paralleles Schreiben auf gemeinsamen DB-/KC-State
- [x] `dotnet test` IntegrationTests grün (40/40)

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

Install playwright browser
```
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework>net10.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
		<IsPackable>false</IsPackable>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="Aspire.Hosting.Testing" />
		<PackageReference Include="TUnit" />
		<PackageReference Include="TUnit.Playwright" />
		<PackageReference Include="Verify.TUnit" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="..\Aspire\AppHost\AppHost.csproj"/>
		<ProjectReference Include="..\Backend\Backend.csproj"/>
	</ItemGroup>

	<ItemGroup>
	  <None Update="GetQuestionsTests.ShouldReturnQuestions.verified.txt">
	    <DependentUpon>GetQuestionsTests.cs</DependentUpon>
	  </None>
	</ItemGroup>

	<!-- Install Playwright browsers after build -->
	<Target Name="InstallPlaywrightBrowsers" AfterTargets="Build">
		<Exec Command="&quot;$(OutputPath).playwright/node/win32_x64/node.exe&quot; &quot;$(OutputPath).playwright/package/cli.js&quot; install --with-deps chromium"
		      Condition="'$(OS)' == 'Windows_NT'" />
		<Exec Command="&quot;$(OutputPath).playwright/node/linux-x64/node&quot; &quot;$(OutputPath).playwright/package/cli.js&quot; install --with-deps chromium"
		      Condition="'$(OS)' != 'Windows_NT'" />
	</Target>

</Project>
```

Example source code for visual tests
```
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class QuestionnaireVisualTests(AspireFixture fixture) : PageTest, IAsyncInitializer
{
	public Task InitializeAsync()
		=> fixture.WaitForResourceAsync("frontend");

	[Test]
	[Explicit]
	public async Task QuestionnairePage()
	{
		await Page.GotoAsync(fixture.GetEndpoint("frontend").ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var screenshot = await Page.ScreenshotAsync();
		await Verify(screenshot, "png");
	}

	[Test]
	[Explicit]
	public async Task ScorePage()
	{
		await Page.GotoAsync(fixture.GetEndpoint("frontend").ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		foreach (var fieldset in await Page.Locator("fieldset").AllAsync())
			await fieldset.Locator("input[type='radio']").First.CheckAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Submit answers" }).ClickAsync();
		await Page.WaitForURLAsync("**/score");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var screenshot = await Page.ScreenshotAsync();
		await Verify(screenshot, "png");
	}
}
```

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
