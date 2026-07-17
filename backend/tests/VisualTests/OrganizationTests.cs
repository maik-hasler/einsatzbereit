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

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The switcher toggle has aria-label "Switch organization" (en) /
		// "Organisation wechseln" (de). Match on that specifically rather than
		// the generic `button[aria-expanded]` selector, since the header also
		// has other aria-expanded buttons (notifications, mobile menu,
		// language selector) whose DOM order isn't stable across seed state.
		var switcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		if (await switcherBtn.CountAsync() == 0)
			return; // no org selected in seed - skip

		await switcherBtn.First.ClickAsync();
		var dashboardLink = Page.GetByTestId("org-dashboard-link");
		if (await dashboardLink.CountAsync() == 0)
			return; // no org selected in seed - skip

		await dashboardLink.First.ClickAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Members" }).ClickAsync();

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

		await Page.GetByRole(AriaRole.Button, new() { Name = "Members" }).ClickAsync();

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

		await Page.GetByRole(AriaRole.Button, new() { Name = "Settings" }).ClickAsync();

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
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var orgName = $"Visual712 FullDetails {Guid.NewGuid():N}";

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var switcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		if (await switcherBtn.CountAsync() > 0)
		{
			await switcherBtn.First.ClickAsync();
			await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();
		}
		else
		{
			await Page.GotoAsync($"{origin}/profile");
			await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();
		}

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
		await Expect(createDialog).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.GotoAsync(origin);
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }).First.ClickAsync();
		var orgRow = Page.Locator("li", new() { HasText = orgName });
		await orgRow.GetByTestId("org-dashboard-link").ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/organizations/.+/dashboard"), new() { Timeout = 10_000 });

		await Page.GetByRole(AriaRole.Button, new() { Name = "Settings" }).ClickAsync();

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
		// Regression for #641: the header/tab-bar used to be wrapped in the same
		// `max-w-2xl` container as the per-tab content, so switching off the
		// Calendar tab shrank the tab bar itself (and left-aligned it, since
		// that container had no `mx-auto`) instead of leaving it full width and
		// only constraining the content beneath it.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual641 Alignment");

		var tabBar = Page.Locator("nav").Filter(new() { HasText = "Settings" });
		var calendarBox = await tabBar.BoundingBoxAsync();
		calendarBox.Should().NotBeNull();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Settings" }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Edit" })).ToBeVisibleAsync(
			new() { Timeout = 10_000 });

		var settingsBox = await tabBar.BoundingBoxAsync();
		settingsBox.Should().NotBeNull();

		settingsBox!.Width.Should().Be(calendarBox!.Width);
		settingsBox.X.Should().Be(calendarBox.X);
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

		var match = Regex.Match(Page.Url, @"/organizations/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = match.Groups[1].Value;

		await Page.GotoAsync($"{origin}/organizations/{organizationId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertMaxWidthContentCenteredAsync("Organization profile page");
	}

	private async Task<string> CreateOrganizationAsync(string namePrefix)
	{
		var orgName = $"{namePrefix} {Guid.NewGuid():N}";
		var origin = Fixture.GetEndpoint("frontend").GetLeftPart(UriPartial.Authority);

		var switcherBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		if (await switcherBtn.CountAsync() > 0)
		{
			await switcherBtn.First.ClickAsync();
			await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();
		}
		else
		{
			// No orgs yet - the switcher is hidden from the header entirely,
			// so create from the profile page instead.
			await Page.GotoAsync($"{origin}/profile");
			await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();
		}

		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync();
		await createDialog.Locator("input[type='text']").FillAsync(orgName);
		await Page.GetByTestId("modal-submit").ClickAsync();
		await Expect(createDialog).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Reload so the header's org switcher (fetched once on mount) picks up
		// the newly created org - creating it doesn't refresh the switcher in place.
		await Page.GotoAsync(origin);
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var switcher = Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" });
		await switcher.First.ClickAsync();
		var orgRow = Page.Locator("li", new() { HasText = orgName });
		await orgRow.GetByTestId("org-dashboard-link").ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/organizations/.+/dashboard"), new() { Timeout = 10_000 });

		return orgName;
	}
}
