using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class NotificationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task NotificationBell_IsVisible_WhenAuthenticated()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var bell = Page.GetByTestId("notification-bell");
		await Expect(bell).ToBeVisibleAsync(new() { Timeout = 15_000 });
	}

	[Test]
	public async Task NotificationBell_OpensPanel_WhenClicked()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var bell = Page.GetByTestId("notification-bell");
		await Expect(bell).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await bell.ClickAsync();

		var panel = Page.GetByTestId("notification-panel");
		await Expect(panel).ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	/// <summary>
	/// Regression for #1015: the org app was restructured in #9 to nest
	/// opportunities/members/settings under a /dashboard parent segment, but
	/// the EngagementCreated notification's actionUrl was never updated to
	/// match, so clicking it sent an organizer to a 404 instead of the
	/// engagement management page.
	/// </summary>
	[Test]
	public async Task EngagementCreatedNotification_NavigatesToEngagementManagementPage()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await olafHttp.PostAsJsonAsync(
			"/v1/organizations", new { name = $"NotifDeepLink Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"NotifDeepLink Opportunity {suffix}";
		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = oppTitle,
			description = "Created by NotificationTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");
		var applyResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Notify Olaf please." });
		applyResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");

		var bell = Page.GetByTestId("notification-bell");
		await Expect(bell).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await bell.ClickAsync();

		var panel = Page.GetByTestId("notification-panel");
		await Expect(panel).ToBeVisibleAsync(new() { Timeout = 5_000 });

		var notificationItem = panel.Locator("li", new() { HasText = oppTitle }).First;
		await Expect(notificationItem).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await notificationItem.GetByRole(AriaRole.Button).ClickAsync();

		await Page.WaitForURLAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements",
			new() { Timeout = 15_000 });

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Page not found" })).Not.ToBeVisibleAsync();
		await Expect(Page.Locator("nav[aria-label='Breadcrumb']")).ToBeVisibleAsync(new() { Timeout = 10_000 });
	}
}
