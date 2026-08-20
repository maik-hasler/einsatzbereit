using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for #1017: QuickCheckInWidget's dashboard picker filtered
/// opportunities only by publication status, ignoring CheckInMethod - so an
/// organizer could select a Published opportunity configured for PIN code or
/// manual check-in and be offered a QR scanner that could never work for it.
/// filterQrCheckInOpportunities (frontend/src/lib/quickCheckIn.ts) now
/// restricts the widget to Published opportunities using QR code check-in.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class QuickCheckInWidgetTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task QuickCheckInWidget_DropdownOffersOnlyTheQrCodeOpportunity_WhenOrgHasBothQrAndPinCodeOpportunities()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		using var http = await CreateAuthenticatedHttpClientAsync(backend);

		var suffix = Guid.NewGuid().ToString("N");
		var organizationId = await CreateOrganizationAsync(http, $"Visual1017 QrAndPin {suffix}");

		var qrTitle = $"Visual1017 QR {suffix}";
		await CreatePublishedOpportunityAsync(http, organizationId, qrTitle, checkInMethod: "QRCode");
		var pinTitle = $"Visual1017 PIN {suffix}";
		await CreatePublishedOpportunityAsync(http, organizationId, pinTitle, checkInMethod: "PINCode");

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		await Page.WaitForURLAsync($"{origin}/app/{organizationId}/dashboard", new() { Timeout = 15_000 });

		var quickCheckInWidget = await AddQuickCheckInWidgetAsync();

		var combobox = quickCheckInWidget.GetByRole(AriaRole.Combobox);
		await Expect(combobox).ToContainTextAsync(qrTitle, new() { Timeout = 10_000 });

		await combobox.ClickAsync();
		var listbox = quickCheckInWidget.GetByRole(AriaRole.Listbox);
		await Expect(listbox).ToBeVisibleAsync();

		// Only the QR opportunity is selectable - the PIN one is filtered out
		// of the picker entirely rather than merely disabled, since scanning
		// for it could never succeed.
		await Expect(listbox.GetByRole(AriaRole.Option)).ToHaveCountAsync(1);
		await Expect(listbox.GetByText(qrTitle)).ToBeVisibleAsync();
		await Expect(listbox.GetByText(pinTitle)).ToHaveCountAsync(0);

		await Expect(quickCheckInWidget.GetByTestId("quick-checkin-scan-btn")).ToBeEnabledAsync();
	}

	[Test]
	public async Task QuickCheckInWidget_ShowsEmptyState_WhenOrgHasOnlyNonQrCodeOpportunities()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		using var http = await CreateAuthenticatedHttpClientAsync(backend);

		var suffix = Guid.NewGuid().ToString("N");
		var organizationId = await CreateOrganizationAsync(http, $"Visual1017 PinOnly {suffix}");

		var pinTitle = $"Visual1017 PinOnly Opportunity {suffix}";
		await CreatePublishedOpportunityAsync(http, organizationId, pinTitle, checkInMethod: "PINCode");

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard");
		await Page.WaitForURLAsync($"{origin}/app/{organizationId}/dashboard", new() { Timeout = 15_000 });

		var quickCheckInWidget = await AddQuickCheckInWidgetAsync();

		// No QR-enabled opportunity exists, so the widget must show its empty
		// state instead of offering the PIN-only opportunity in a dropdown
		// where scanning for it could never work - not merely no opportunities
		// at all, hence a PIN opportunity does exist in this org.
		await Expect(quickCheckInWidget.GetByText(
			"No published opportunities using QR code check-in yet."))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(quickCheckInWidget.GetByRole(AriaRole.Combobox)).ToHaveCountAsync(0);
		await Expect(quickCheckInWidget.GetByTestId("quick-checkin-scan-btn")).ToHaveCountAsync(0);
	}

	/// <summary>
	/// QuickCheckIn isn't in DEFAULT_LAYOUT (see widgetCatalog.ts), so a fresh
	/// org's dashboard never renders it without going through the "Edit" ->
	/// "Add widget" picker first, which is the only way to reach this widget's
	/// real rendered content in a browser.
	/// </summary>
	private async Task<ILocator> AddQuickCheckInWidgetAsync()
	{
		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Page.GetByTestId("quick-action-add-widget").ClickAsync();

		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await dialog.GetByTestId("add-widget-option-QuickCheckIn").ClickAsync();
		await dialog.GetByTestId("add-widget-done").ClickAsync();
		await Expect(dialog).Not.ToBeVisibleAsync();

		await Page.GetByTestId("quick-action-save").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-edit")).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var widget = Page.GetByTestId("widget-tile-QuickCheckIn");
		await Expect(widget).ToBeVisibleAsync();
		return widget;
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

	private static async Task<string> CreateOrganizationAsync(HttpClient http, string name)
	{
		var response = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name });
		response.EnsureSuccessStatusCode();
		var org = await response.Content.ReadFromJsonAsync<JsonElement>();
		return org.GetProperty("id").GetProperty("value").GetString()!;
	}

	private static async Task CreatePublishedOpportunityAsync(
		HttpClient http, string organizationId, string title, string checkInMethod)
	{
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Created by QuickCheckInWidgetTests.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "ScheduledSlots",
			checkInMethod,
			isDraft = true,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var start = DateTimeOffset.UtcNow.AddDays(3);
		var end = start.AddHours(2);
		(await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
			new { startDateTime = start, endDateTime = end, maxParticipants = 5, recurrenceCount = 1 }))
			.EnsureSuccessStatusCode();

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();
	}
}
