using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrgAppMobileResponsiveTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int MobileWidth = 375;
	private const int MobileHeight = 812;

	[Test]
	public async Task MobileHeader_OrgSwitcherDoesNotBlockControls_HamburgerRevealsProfileAndLanguage()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var mobileBell = Page.GetByTestId("notification-bell-mobile");
		await Expect(mobileBell).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var hamburger = Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" });
		await Expect(hamburger).ToBeVisibleAsync();

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
			.Not.ToBeVisibleAsync();

		await hamburger.ClickAsync(new() { Timeout = 10_000 });

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "My profile" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task MobileHeader_OrgSwitcherName_StaysLegibleForOrgsSharingAnInitial()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var switcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		await switcherBtn.ClickAsync();

		var animalWelfareRow = Page.GetByTestId("org-switch-row")
			.Filter(new() { HasText = "Lindenauer Tierschutzverein e.V." });
		try
		{
			await animalWelfareRow.WaitForAsync(new() { Timeout = 10_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("seed data changed - nothing to compare against");
		}

		await animalWelfareRow.ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });

		var nameSpan = Page.GetByTestId("org-switcher-current-name");
		await Expect(nameSpan).ToHaveTextAsync("Lindenauer Tierschutzverein e.V.", new() { Timeout = 15_000 });
		var box = await nameSpan.BoundingBoxAsync();
		box.Should().NotBeNull();
		box!.Width.Should().BeGreaterThan(60,
			"the org name must keep enough width on mobile to show more than just its "
			+ "first letter - it previously rendered at ~0px wide here");
	}

	[Test]
	public async Task MobileHeader_OrgSwitcherDoesNotOverlapControls_At320pxViewport()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
		await Page.SetViewportSizeAsync(320, 568);

		var mobileBell = Page.GetByTestId("notification-bell-mobile");
		await Expect(mobileBell).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var hamburger = Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" });
		await Expect(hamburger).ToBeVisibleAsync();

		await hamburger.ClickAsync(new() { Timeout = 10_000 });

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task OrgHeader_AtTheDesktopBreakpoint_KeepsTheAccountMenuAndLanguageSwitcherReachable()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
		await Page.SetViewportSizeAsync(1024, 768);

		await Expect(Page.GetByTestId("org-switcher-current-name"))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		var overflow = await Page.EvaluateAsync<int>(
			"() => document.documentElement.scrollWidth - document.documentElement.clientWidth");
		overflow.Should().BeLessThanOrEqualTo(0,
			"the org switcher plus the primary nav used to push the bell, the account menu "
			+ "and the language switcher past the right edge of a 1024px viewport, so the "
			+ "only way to reach your own account menu was to scroll the whole page "
			+ "sideways (#2321)");

		// Below the breakpoint where the row fits, the collapsed header holds and the
		// hamburger carries the same controls - what must never happen is that they are
		// rendered somewhere off-screen.
		var hamburger = Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" });
		await Expect(hamburger).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await hamburger.ClickAsync(new() { Timeout = 10_000 });

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch language" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task MembersList_At320px_KeepsEveryMemberNameLegible()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/app/{pinnedOrgId!.Value}/dashboard/members");
		await Page.SetViewportSizeAsync(320, 568);

		var names = Page.GetByTestId("org-member-name");
		try
		{
			await names.First.WaitForAsync(new() { Timeout = 15_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("seed data changed - this organization has no member rows to measure");
		}

		var widths = await names.EvaluateAllAsync<int[]>("els => els.map(el => el.clientWidth)");
		widths.Should().NotBeEmpty();
		widths.Should().AllSatisfy(width => width.Should().BeGreaterThan(0),
			"the action buttons never shrank, so on a 320px viewport they took the whole "
			+ "row and the member's name rendered at zero width - leaving a Remove button "
			+ "with no indication of which member it would remove (#2321)");
	}

	[Test]
	public async Task NotificationPanelHeader_AtMobileWidth_WrapsInsteadOfOverflowing()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");

		var olafSession = await Fixture.SignInAsync("olaf", "olaf123");
		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafSession.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(olafHttp,
			"/v1/organizations", new { name = $"NotifHeaderWrap Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var veraSession = await Fixture.SignInAsync("vera", "vera123");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraSession.AccessToken}");

		async Task CreateOpportunityAndApplyAsync(string label)
		{
			var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
			{
				titleDe = $"{label} {suffix}",
				descriptionDe = "Created by OrgAppMobileResponsiveTests",
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

			var applyResponse = await veraHttp.PostAsJsonAsync(
				$"/v1/volunteer-opportunities/{opportunityId}/engagements",
				new { message = $"Apply for {label}" });
			applyResponse.EnsureSuccessStatusCode();
		}

		await CreateOpportunityAndApplyAsync("Header Wrap Unread");
		await CreateOpportunityAndApplyAsync("Header Wrap Read");

		var notificationsResponse = await olafHttp.GetAsync("/v1/notifications");
		notificationsResponse.EnsureSuccessStatusCode();
		var notificationsPage = await notificationsResponse.Content.ReadFromJsonAsync<JsonElement>();
		var firstNotificationId = notificationsPage.GetProperty("items")[0].GetProperty("id").GetString();

		var markReadResponse = await olafHttp.PostAsync($"/v1/notifications/{firstNotificationId}/read", null);
		markReadResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123", pinActiveOrg: false);
		await Page.SetViewportSizeAsync(320, 568);

		var mobileBell = Page.GetByTestId("notification-bell-mobile");
		await Expect(mobileBell).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await mobileBell.ClickAsync();

		var panel = Page.GetByTestId("notification-panel-mobile");
		await Expect(panel).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Mark all as read" })).ToBeVisibleAsync();
		await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Clear read" })).ToBeVisibleAsync();

		var header = panel.GetByTestId("notification-panel-header");
		var headerOverflows = await header.EvaluateAsync<bool>("el => el.scrollWidth > el.clientWidth + 1");
		headerOverflows.Should().BeFalse(
			"the title and both action buttons must wrap onto their own line rather than "
				+ "overflowing the 320px panel");

		await olafHttp.DeleteAsync($"/v1/organizations/{organizationId}");
	}
}
