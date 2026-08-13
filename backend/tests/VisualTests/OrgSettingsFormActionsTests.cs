using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests for the org settings form's own action row (frontend
/// OrgSettingsPage.tsx), added for #1784.
///
/// Cancel/Save are QuickActions registered by the page and rendered only by
/// OrgPageHeader, so in edit mode they sat in the header band while the form
/// carried on past the fold through Website, street, house number, ZIP and
/// city. Having filled in the address at the bottom, the organizer had to
/// scroll all the way back to the top of the page to commit - a long scroll
/// away from where their attention was, and easy to lose track of on a touch
/// device.
///
/// The header pair stays; these tests pin the repeat at the end of the form:
/// that it is genuinely reachable from the last field, that it commits
/// through the same path the header's requestSubmit() does, that it makes the
/// form's Enter key submit (implicit submission needs a submit button, and the
/// form had none), and that a failure triggered from down there doesn't land
/// silently off-screen in the banner above the first field.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgSettingsFormActionsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int ViewportWidth = 1280;

	// Short enough that the settings form reliably runs past the fold on a
	// desktop viewport - which is the whole premise of #1784, and of the
	// off-screen assertions below.
	private const int ViewportHeight = 720;

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

		// The repeat belongs to the form, not to more page chrome below it -
		// that is what lets it be a real submit button rather than a second
		// caller of formRef.requestSubmit().
		var formSave = Page.Locator("main form [data-testid=org-settings-form-save]");
		await Expect(formSave).ToBeVisibleAsync();
		await Expect(formSave).ToHaveAttributeAsync("type", "submit");
		await Expect(Page.Locator("main form [data-testid=org-settings-form-cancel]")).ToBeVisibleAsync();

		// Scrolling the last field of the form into view puts the organizer
		// exactly where the issue describes: the address is filled in, and the
		// header's Save is now somewhere above the top of the screen.
		//
		// The scroll is re-applied inside the loop rather than once before it,
		// for the same reason CreateOpportunityModalViewportTests re-dispatches
		// its wheel ticks: entering edit mode also queues a focus of the
		// header's Cancel button (useEditModeQuickActions, one
		// requestAnimationFrame later), and focusing scrolls. Under this
		// suite's own CPU contention that frame can land after a single
		// up-front scroll, leaving a poll that can never recover.
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

		// ...and committing from there behaves exactly like the header's Save:
		// same validation, same request, back to the read-only view with the
		// new name reflected across the shell.
		var newName = $"Renamed From The Form Footer {Guid.NewGuid():N}";
		await Page.GetByLabel("Name *").FillAsync(newName);
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

		// Implicit submission needs a submit button in the form. Before #1784
		// this form had none, so Enter in any of its fields did nothing at all
		// - even though onSave routes through requestSubmit() precisely so that
		// it matches what pressing Enter does.
		var newName = $"Renamed With The Enter Key {Guid.NewGuid():N}";
		var nameField = Page.GetByLabel("Name *");
		await nameField.FillAsync(newName);
		await nameField.PressAsync("Enter");

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

		// 400, not 500: a 5xx also raises a global toast (api-instance.ts), and
		// this test is about the page's own inline banner rather than about
		// which other error surfaces react.
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

		// ClickAsync scrolls its target into view first, so the submit really
		// is issued from the bottom of the form - which is the position that
		// makes the banner above the first field invisible.
		await Page.GetByLabel("Name *").FillAsync($"Save Will Fail {Guid.NewGuid():N}");
		await formSave.ClickAsync();

		// The banner sits above the first field, so from the bottom of the form
		// a failure would otherwise be indistinguishable from nothing having
		// happened at all - the page scrolls it into view rather than leaving
		// role="alert" to carry the whole message on its own.
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

		// Scrolled *and* focused, the DetailsStep/#688 pairing: the control
		// that submitted went disabled for the duration of the request, which
		// blurs it to <body>, so a keyboard user would otherwise be left at the
		// top of the document a screen away from the message.
		await Expect(banner).ToBeFocusedAsync();

		// The form stays open with the organizer's edits intact, so they can
		// fix and retry rather than retype.
		await Expect(formSave).ToBeVisibleAsync();

		// AccessibilityTests covers this page's read-only, edit and
		// field-validation states, but not the server-error one - and this test
		// has already built it, so scan it here rather than standing up the
		// same 400 intercept a second time over there.
		var axe = await Page.RunAxe();
		axe.Violations.Where(v => v.Impact is "serious" or "critical").Should().BeEmpty();

		await Page.UnrouteAsync($"**/v1/organizations/{organizationId}");
		await DeleteOrganizationAsync(backend, organizationId);
	}

	/// <summary>
	/// Creates an organization through the API with the signed-in user's own
	/// token, so the caller organizes it - same approach as
	/// OrgAppCompactHeaderTests, and faster than driving the switcher's
	/// create-organization dialog.
	/// </summary>
	private async Task<string> CreateOrganizationAsync(string name)
	{
		var backend = Fixture.GetEndpoint("backend");
		using var http = await CreateAuthenticatedHttpClientAsync(backend);
		var response = await http.PostAsJsonAsync("/v1/organizations", new { name });
		response.EnsureSuccessStatusCode();
		var org = await response.Content.ReadFromJsonAsync<JsonElement>();
		return org.GetProperty("id").GetProperty("value").GetString()!;
	}

	/// <summary>
	/// The shared olaf account accumulates test debris across this suite's
	/// session (see the root AGENTS.md note about live staging) - clean up
	/// the organizations these tests create.
	/// </summary>
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
