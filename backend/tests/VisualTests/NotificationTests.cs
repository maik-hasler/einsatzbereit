using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class NotificationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task EngagementCreatedNotification_NavigatesToEngagementManagementPage()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"NotifDeepLink Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"NotifDeepLink Opportunity {suffix}";
		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by NotificationTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
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

		await notificationItem.GetByRole(AriaRole.Link).First.ClickAsync();

		await Page.WaitForURLAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements",
			new() { Timeout = 15_000 });

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Page not found" })).Not.ToBeVisibleAsync();

		await Expect(Page.Locator("main").GetByRole(AriaRole.Heading, new() { Level = 1 }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	[Test]
	public async Task InvitationReceivedNotification_NavigatesToMySignups_WhereTheInviteeCanAcceptIt()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgName = $"NotifInvite Org {suffix}";
		var orgResponse = await PostJsonWithRetryAsync(olafHttp, "/v1/organizations", new { name = orgName });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var veraSession = await Fixture.SignInAsync("vera", "vera123");

		var inviteResponse = await olafHttp.PostAsJsonAsync(
			$"/v1/organizations/{organizationId}/invitations",
			new { inviteeId = veraSession.UserId, role = "Member" });
		inviteResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");

		var bell = Page.GetByTestId("notification-bell");
		await Expect(bell).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await bell.ClickAsync();

		var panel = Page.GetByTestId("notification-panel");
		await Expect(panel).ToBeVisibleAsync(new() { Timeout = 5_000 });

		var notificationItem = panel.Locator("li", new() { HasText = orgName }).First;
		await Expect(notificationItem).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await notificationItem.GetByRole(AriaRole.Link).First.ClickAsync();

		await Page.WaitForURLAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/my-signups",
			new() { Timeout = 15_000 });

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Open invitations" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		var invitationCard = Page.Locator("li", new() { HasText = orgName });
		await Expect(invitationCard).ToBeVisibleAsync();

		await invitationCard.GetByRole(AriaRole.Button, new() { Name = "Accept" }).ClickAsync();

		await Expect(invitationCard).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	[Test]
	public async Task ClickingNotification_StillNavigates_WhenMarkAsReadFails()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"NotifMarkReadFail Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"NotifMarkReadFail Opportunity {suffix}";
		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Created by NotificationTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
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

		await Page.RouteAsync("**/v1/notifications/*/read", async route =>
		{
			await route.FulfillAsync(new()
			{
				Status = 500,
				ContentType = "application/json",
				Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
				Body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.6.1\",\"status\":500}",
			});
		});

		var bell = Page.GetByTestId("notification-bell");
		await Expect(bell).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await bell.ClickAsync();

		var panel = Page.GetByTestId("notification-panel");
		await Expect(panel).ToBeVisibleAsync(new() { Timeout = 5_000 });

		var notificationItem = panel.Locator("li", new() { HasText = oppTitle }).First;
		await Expect(notificationItem).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await notificationItem.GetByRole(AriaRole.Link).First.ClickAsync();

		await Page.WaitForURLAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements",
			new() { Timeout = 15_000 });

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Page not found" })).Not.ToBeVisibleAsync();

		var errorToast = Page.GetByRole(AriaRole.Alert)
			.Filter(new() { HasTextString = "Failed to mark notification as read." });
		await Expect(errorToast).ToBeVisibleAsync(new() { Timeout = 5_000 });
	}
}
