using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgSettingsFormActionsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int ViewportWidth = 1280;

	private const int ViewportHeight = 720;

	private ILocator NameField => Page.GetByLabel("Name");

	[Test]
	public async Task EditMode_RepeatsSaveAtTheEndOfTheForm_ReachableFromTheLastField()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(ViewportWidth, ViewportHeight);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual SettingsActions {Guid.NewGuid():N}");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/settings");
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByTestId("quick-action-edit").ClickAsync();

		var formSave = Page.Locator("main form [data-testid=org-settings-form-save]");
		await Expect(formSave).ToBeVisibleAsync();
		await Expect(formSave).ToHaveAttributeAsync("type", "submit");
		await Expect(Page.Locator("main form [data-testid=org-settings-form-cancel]")).ToBeVisibleAsync();

		var city = Page.GetByLabel("City");
		var headerSave = Page.GetByTestId("quick-action-save");
		var lastObserved = "<none>";
		await PollUntilAsync(async () =>
		{
			await city.ScrollIntoViewIfNeededAsync();
			var formSaveBox = await formSave.BoundingBoxAsync();
			var headerSaveBox = await headerSave.BoundingBoxAsync();
			lastObserved = $"form Save top={formSaveBox?.Y:F0} bottom={(formSaveBox is null ? null : formSaveBox.Y + formSaveBox.Height):F0}, "
				+ $"header Save bottom={(headerSaveBox is null ? null : headerSaveBox.Y + headerSaveBox.Height):F0}";
			if (formSaveBox is null || headerSaveBox is null)
				return false;
			return headerSaveBox.Y + headerSaveBox.Height <= 0
				&& formSaveBox.Y >= 0
				&& formSaveBox.Y + formSaveBox.Height <= ViewportHeight;
		}, () => "standing at the last field of the form, a Save action must be on screen without scrolling back "
			+ $"up - and the header's own Save is expected to have scrolled off the top of the {ViewportHeight}px "
			+ $"viewport by then (last observed: {lastObserved})");

		var newName = $"Renamed From The Form Footer {Guid.NewGuid():N}";
		await NameField.FillAsync(newName);
		await formSave.ClickAsync();

		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByTestId("org-app-header")).ToContainTextAsync(newName, new() { Timeout = 15_000 });

		await DeleteOrganizationAsync(backend, organizationId);
	}

	[Test]
	public async Task EditMode_EnterInAField_SubmitsTheForm()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(ViewportWidth, ViewportHeight);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual SettingsEnter {Guid.NewGuid():N}");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/settings");
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.Locator("main form [data-testid=org-settings-form-save]")).ToBeVisibleAsync();

		var newName = $"Renamed With The Enter Key {Guid.NewGuid():N}";
		await NameField.FillAsync(newName);
		await NameField.PressAsync("Enter");

		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByTestId("org-app-header")).ToContainTextAsync(newName, new() { Timeout = 15_000 });

		await DeleteOrganizationAsync(backend, organizationId);
	}

	[Test]
	public async Task EditMode_FailedSaveFromTheFormActionRow_BringsTheErrorBannerIntoView()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.SetViewportSizeAsync(ViewportWidth, ViewportHeight);
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var organizationId = await CreateOrganizationAsync($"Visual SettingsSaveError {Guid.NewGuid():N}");

		await Page.RouteAsync($"**/v1/organizations/{organizationId}", async route =>
		{
			if (route.Request.Method != "PUT")
			{
				await route.ContinueAsync();
				return;
			}

			await route.FulfillAsync(new()
			{
				Status = 400,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.1\",\"status\":400}",
			});
		});

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/settings");
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		var formSave = Page.Locator("main form [data-testid=org-settings-form-save]");
		await Expect(formSave).ToBeVisibleAsync();

		await NameField.FillAsync($"Save Will Fail {Guid.NewGuid():N}");
		await formSave.ClickAsync();

		var banner = Page.Locator("p[role=alert]").Filter(new() { HasTextString = "Failed to save." });
		await Expect(banner).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var lastObserved = "<none>";
		await PollUntilAsync(async () =>
		{
			var box = await banner.BoundingBoxAsync();
			lastObserved = box is null ? "<no box>" : $"top={box.Y:F0} bottom={box.Y + box.Height:F0}";
			return box is not null && box.Y >= 0 && box.Y + box.Height <= ViewportHeight;
		}, () => "a save that fails from the form's own action row must scroll its error banner into view "
			+ $"(last observed in a {ViewportHeight}px viewport: {lastObserved})");

		await Expect(banner).ToBeFocusedAsync();

		await Expect(formSave).ToBeVisibleAsync();

		var axe = await Page.RunAxe();
		axe.Violations.Where(v => v.Impact is "serious" or "critical").Should().BeEmpty();

		await Page.UnrouteAsync($"**/v1/organizations/{organizationId}");
		await DeleteOrganizationAsync(backend, organizationId);
	}

	private async Task<string> CreateOrganizationAsync(string name)
	{
		var backend = Fixture.GetEndpoint("backend");
		using var http = await CreateAuthenticatedHttpClientAsync(backend);
		var response = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name });
		response.EnsureSuccessStatusCode();
		var org = await response.Content.ReadFromJsonAsync<JsonElement>();
		return org.GetProperty("id").GetProperty("value").GetString()!;
	}

	private async Task DeleteOrganizationAsync(Uri backend, string organizationId)
	{
		using var http = await CreateAuthenticatedHttpClientAsync(backend);
		await http.DeleteAsync($"/v1/organizations/{organizationId}");
	}

	private async Task<HttpClient> CreateAuthenticatedHttpClientAsync(Uri backend)
	{
		var token = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < sessionStorage.length; i++) {
				const key = sessionStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(sessionStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in sessionStorage after login");

		var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
		return http;
	}
}
