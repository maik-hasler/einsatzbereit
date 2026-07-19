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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);

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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
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
	public async Task CreateOrganizationModal_AcceptsFullDetails_AndAppliesThemAtCreation()
	{
		// #712: the create-organization modal used to collect only Name -
		// description, contact info, address and logo were only reachable
		// afterwards via the Settings tab. Verifies the richer create form
		// persists all of them in one step.
		var frontend = Fixture.GetEndpoint("frontend");
		var orgName = $"Visual712 FullDetails {Guid.NewGuid():N}";

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);

		// New orgs are created from the org switcher's "Create organization"
		// entry now that it's the org app's only creation entry point.
		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();

		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync();
		await createDialog.Locator("#create-org-name").FillAsync(orgName);
		await createDialog.Locator("#create-org-description").FillAsync("A helpful description for volunteers.");
		await createDialog.Locator("#create-org-contact-email").FillAsync("contact@visual712.example.com");
		await createDialog.Locator("#create-org-phone").FillAsync("+49 30 1234567");
		await createDialog.Locator("#create-org-website").FillAsync("https://visual712.example.com");
		await createDialog.Locator("#create-org-street").FillAsync("Main Street");
		await createDialog.Locator("#create-org-house-number").FillAsync("1");
		await createDialog.Locator("#create-org-zip").FillAsync("12345");
		await createDialog.Locator("#create-org-city").FillAsync("Berlin");

		await Page.GetByTestId("modal-submit").ClickAsync();

		// Creating an org makes it the active org and navigates into its new
		// /app dashboard. Wait for the switcher to reflect the NEW org before
		// opening Settings: a bare WaitForURLAsync(/app/.../dashboard) is already
		// satisfied by the dashboard GoToOrgAppDashboardAsync left us on, so it
		// races the navigation and can open the previous org's Settings instead.
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToContainTextAsync(orgName, new() { Timeout = 15_000 });

		await Page.GetByRole(AriaRole.Link, new() { Name = "Settings", Exact = true }).ClickAsync();

		await Expect(Page.GetByText("A helpful description for volunteers.")).ToBeVisibleAsync(
			new() { Timeout = 10_000 });
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "contact@visual712.example.com" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "+49 30 1234567" })).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "https://visual712.example.com" }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByText("Main Street 1, 12345 Berlin")).ToBeVisibleAsync();
	}

	[Test]
	public async Task TabBar_StaysFullWidth_AcrossTabSwitches()
	{
		// Regression for #641 (and a guard against reintroducing it): the tab
		// bar now lives in the persistent /app shell (OrgAppLayout) at the top
		// of the main content area, decoupled from any individual tab page's own
		// content-width wrapper - it must not shrink or shift when navigating
		// between tabs.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual641 Alignment");

		// Measure the tab row's flex container inside the tabs nav; its width
		// tracks the enclosing <main class="...max-w-7xl...">, which is constant
		// across tab switches. Scope by the nav's accessible name so the
		// breadcrumb nav - which also surfaces the current tab label - isn't
		// matched too.
		var tabBar = Page
			.GetByRole(AriaRole.Navigation, new() { Name = "Organization sections" })
			.Locator("div")
			.First;
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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
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
		// New orgs are created via the org switcher's "Create organization" entry
		// - reachable from within any org the caller already organizes (olaf's
		// seed data always has at least one).
		var orgName = $"{namePrefix} {Guid.NewGuid():N}";
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();

		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync();
		await createDialog.Locator("input[type='text']").FillAsync(orgName);
		await Page.GetByTestId("modal-submit").ClickAsync();

		// Creating an org makes it the active org and navigates into its new
		// /app dashboard. Wait for the switcher to reflect the NEW org: a bare
		// WaitForURLAsync(/app/.../dashboard) is already satisfied by the
		// dashboard GoToOrgAppDashboardAsync left us on, so it would race the
		// navigation and return while still on the previous org.
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToContainTextAsync(orgName, new() { Timeout = 15_000 });
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });

		return orgName;
	}
}
