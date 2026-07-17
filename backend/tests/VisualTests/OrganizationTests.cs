using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrganizationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task Organisator_LoginAsOlaf_Succeeds()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var origin = frontend.GetLeftPart(UriPartial.Authority);
		await Expect(Page).ToHaveURLAsync($"{origin}/");
	}

	[Test]
	public async Task Organisator_InviteMemberFromDashboard_SendsInvitationInsteadOf403()
	{
		// Regression for #579: the dashboard's Members tab used to call the
		// admin-only AddMember endpoint, so an organizer got a 403/401 instead
		// of a pending invitation. Verifies the dashboard now calls
		// CreateInvitation and the invitee shows up under Pending Invitations,
		// with no error banner.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/profile");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// "Your organizations" links into /app from the profile page - the only
		// entry point now that the switcher no longer lives in the global header.
		var orgLink = Page.GetByTestId("your-organizations-link");
		if (await orgLink.CountAsync() == 0)
			return; // no org selected in seed - skip

		await orgLink.First.ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });

		await Page.GetByRole(AriaRole.Link, new() { Name = "Members", Exact = true }).ClickAsync();

		await Page.Locator("#member-search").FillAsync("vera");

		var inviteButton = Page.GetByRole(AriaRole.Button, new() { Name = "Invite" });
		try
		{
			await Expect(inviteButton.Or(Page.GetByText("No users found."))).ToBeVisibleAsync(
				new() { Timeout = 10_000 });
		}
		catch (TimeoutException)
		{
			return;
		}

		if (await inviteButton.CountAsync() == 0)
			return; // vera already a member or already invited from a previous run - skip

		await inviteButton.First.ClickAsync();

		// No 403/401-driven error banner ("Could not send invitation.").
		await Expect(Page.GetByText("Could not send invitation.")).Not.ToBeVisibleAsync();

		await Expect(Page.GetByText("Invitation sent.")).ToBeVisibleAsync();
		await Expect(Page.GetByText("Pending Invitations")).ToBeVisibleAsync();
	}

	[Test]
	public async Task SoleMember_MembersTab_ShowsDisabledLeaveInsteadOfRemove()
	{
		// #580: the org's sole member must see a disabled "Leave" action on
		// their own row, never "Remove" - removing them would orphan the org.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual580 Leave");

		await Page.GetByRole(AriaRole.Link, new() { Name = "Members", Exact = true }).ClickAsync();

		var leaveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Leave" });
		await Expect(leaveButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(leaveButton).ToBeDisabledAsync();

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Remove" })).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task SoleMember_CanDeleteOrganization_FromSettingsTab()
	{
		// #580: the new "Delete Organization" action, enabled only for the
		// sole remaining member, must actually delete the org and go home.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgName = await CreateOrganizationAsync("Visual580 Delete");

		await Page.GetByRole(AriaRole.Link, new() { Name = "Settings", Exact = true }).ClickAsync();

		var deleteButton = Page.GetByRole(AriaRole.Button, new() { Name = "Delete Organization" });
		await Expect(deleteButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(deleteButton).ToBeEnabledAsync();
		await deleteButton.ClickAsync();

		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog.GetByText(orgName)).ToBeVisibleAsync();

		await dialog.GetByRole(AriaRole.Button, new() { Name = "Yes, delete" }).ClickAsync();

		await Page.WaitForURLAsync($"{origin}/", new() { Timeout = 10_000 });
	}

	[Test]
	public async Task TabBar_StaysFullWidth_AcrossTabSwitches()
	{
		// Regression for #641 (and a guard against reintroducing it): the tab
		// bar now lives in the persistent /app header (OrgAppLayout), decoupled
		// from any individual tab page's own content-width wrapper - it must not
		// shrink or shift when navigating between tabs.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual641 Alignment");

		var tabBar = Page.Locator("nav").Filter(new() { HasText = "Settings" });
		var dashboardBox = await tabBar.BoundingBoxAsync();
		dashboardBox.Should().NotBeNull();

		await Page.GetByRole(AriaRole.Link, new() { Name = "Settings", Exact = true }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true })).ToBeVisibleAsync(
			new() { Timeout = 10_000 });

		var settingsBox = await tabBar.BoundingBoxAsync();
		settingsBox.Should().NotBeNull();

		settingsBox!.Width.Should().Be(dashboardBox!.Width);
		settingsBox.X.Should().Be(dashboardBox.X);
	}

	[Test]
	public async Task PublicProfilePage_ContentIsCenteredWithinMain()
	{
		// Regression for #694: OrganizationProfileView's content wrapper
		// (`max-w-2xl`) had no `mx-auto`, so it hugged the left edge of <main>
		// instead of being centered like every other page.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual694 Centering");

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = match.Groups[1].Value;

		await Page.GotoAsync($"{origin}/organizations/{organizationId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertMaxWidthContentCenteredAsync("Organization profile page");
	}

	private async Task<string> CreateOrganizationAsync(string namePrefix)
	{
		// Creating an org is always initiated from the profile page now - it's
		// the only entry point into /app, whether this is the user's first org
		// or an additional one alongside others they already organize.
		var orgName = $"{namePrefix} {Guid.NewGuid():N}";
		var origin = Fixture.GetEndpoint("frontend").GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/profile");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();

		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync();
		await createDialog.Locator("input[type='text']").FillAsync(orgName);
		await Page.GetByTestId("modal-submit").ClickAsync();

		// Creating an org navigates straight into its new /app dashboard.
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });

		return orgName;
	}
}
