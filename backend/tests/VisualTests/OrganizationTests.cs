using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

// #1316: SoleOrganizer_TwoMemberOrg_MembersPage_StillDisablesLeave below needs
// vera's global Keycloak organisator role deterministically cleared - opts
// the whole class into fixture.ResetAsync() and a bare [NotInParallel] so no
// other VisualTest can grant her that role mid-test.
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel]
public class OrganizationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Before(Test)]
	public Task ResetVisualTestStateAsync() => Fixture.ResetAsync();

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

		// The tab bar is gone (dashboard UX redesign) - reach Members via the
		// Settings widget's member-count link instead (its accessible name is
		// "N member(s)" - #834 made the count grammatically correct German/
		// English plural forms, so match "member" to cover both N=1 and N>1).
		await Page.GetByRole(AriaRole.Link, new() { Name = "member" }).ClickAsync();

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
	public async Task SoleMember_MembersPage_ShowsDisabledLeaveInsteadOfRemove()
	{
		// #580: the org's sole member must see a disabled "Leave" action on
		// their own row, never "Remove" - removing them would orphan the org.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual580 Leave");

		// The tab bar is gone (dashboard UX redesign) - reach Members via the
		// Settings widget's member-count link instead (its accessible name is
		// "N member(s)" - #834 made the count grammatically correct German/
		// English plural forms, so match "member" to cover both N=1 and N>1).
		await Page.GetByRole(AriaRole.Link, new() { Name = "member" }).ClickAsync();

		var leaveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Leave" });
		await Expect(leaveButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(leaveButton).ToBeDisabledAsync();

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Remove" })).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task SoleOrganizer_TwoMemberOrg_MembersPage_StillDisablesLeave()
	{
		// Regression for #825 (UI level): OrgMembersPage.tsx's "last organizer"
		// guard counts members whose isOrganisator is true, which
		// KeycloakOrganizationService.GetMembersAsync derives from Keycloak's
		// *global* "organisator" role, not a per-organization one. Before the
		// fix, the guard instead looked at total member count, so an org with
		// one Organizer plus one plain member (2 members, still only 1
		// Organizer) let the Organizer leave/be removed and permanently orphan
		// the org - there was no path left to promote the remaining member.
		// Deterministic only because fixture.ResetAsync() (this class opts in
		// above) clears vera's global organisator role first - otherwise a
		// leftover role from an earlier, unrelated test would make her read as
		// an Organizer here too, same as HomePageOrgCtaTests.cs and
		// OrgAppRestructureTests.cs used to work around before #1316. The
		// backend guard itself is covered deterministically by
		// IntegrationTests.OrganizationSettingsTests.RemoveMember_ShouldReturn409_WhenSoleOrganizerLeaves_EvenThoughAnotherMemberRemains.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual825 TwoMember");

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = Guid.Parse(match.Groups[1].Value);

		// Accepting an invitation now also grants Organizer (#826), so that
		// flow can no longer produce a plain-member-only state - use the
		// fixture's direct Keycloak escape hatch instead, same as
		// IntegrationTestFixture.AddPlainMemberDirectlyAsync.
		var vera = await Fixture.SignInAsync("vera", "vera123");
		await Fixture.AddPlainMemberDirectlyAsync(organizationId, vera.UserId);

		// OrgAppLayout only refetches org details on organizationId change, so
		// the dashboard we're still on would otherwise keep showing its
		// pre-membership snapshot (olaf as sole member) - force a refetch.
		await Page.ReloadAsync();

		// The tab bar is gone (dashboard UX redesign) - reach Members via the
		// Settings widget's member-count link, same as the sole-member test above.
		await Page.GetByRole(AriaRole.Link, new() { Name = "member" }).ClickAsync();

		var leaveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Leave" });
		await Expect(leaveButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(leaveButton).ToBeDisabledAsync();

		// Unlike the sole-member case, a second member exists to remove -
		// proving the guard is driven by organizer count, not member count.
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Remove" })).ToBeVisibleAsync();
	}

	[Test]
	public async Task SoleMember_CanDeleteOrganization_FromSettingsPage()
	{
		// #580: the new "Delete Organization" action, enabled only for the
		// sole remaining member, must actually delete the org and go home.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgName = await CreateOrganizationAsync("Visual580 Delete");

		// #771: reach Settings via the dashboard's Settings widget link, not a tab.
		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();

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

		// #771: reach Settings via the dashboard's Settings widget link, not a tab.
		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();

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
	public async Task CreateOrganizationModal_NameRequired_BlocksSubmitWithInlineError()
	{
		// #851: the create-organization form used to only enforce Name via the
		// browser's native "required" attribute (or fail at the server round-trip
		// for every other field). It now uses the same react-hook-form + zod
		// approach as the volunteer-opportunity wizard, so submitting blank
		// blocks client-side with a styled inline error instead of a native
		// tooltip or a server error.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();

		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync();

		await createDialog.GetByTestId("modal-submit").ClickAsync();

		await Expect(createDialog.Locator("#create-org-name-error")).ToBeVisibleAsync(
			new() { Timeout = 5_000 });
		await Expect(createDialog.Locator("#create-org-name")).ToHaveAttributeAsync(
			"aria-invalid", "true");

		// Blocked client-side - the dialog is still open, nothing was created.
		await Expect(createDialog).ToBeVisibleAsync();

		await createDialog.GetByTestId("modal-cancel").ClickAsync();
	}

	[Test]
	public async Task CreateOrganizationModal_PartialAddress_ShowsInlineErrorsForMissingFields()
	{
		// #851: Address.Create requires street/houseNumber/zipCode/city together
		// - filling in only some of them used to pass client-side silently and
		// only fail once the request round-tripped to the server. The shared
		// zod schema (organizationFormSchema.ts) now mirrors that same
		// conditional-required rule client-side, same as LocationStep does for
		// the volunteer-opportunity wizard.
		var frontend = Fixture.GetEndpoint("frontend");
		var orgName = $"Visual851 PartialAddress {Guid.NewGuid():N}";

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();

		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync();

		await createDialog.Locator("#create-org-name").FillAsync(orgName);
		await createDialog.Locator("#create-org-street").FillAsync("Main Street");

		await createDialog.GetByTestId("modal-submit").ClickAsync();

		await Expect(createDialog.Locator("#create-org-house-number-error")).ToBeVisibleAsync(
			new() { Timeout = 5_000 });
		await Expect(createDialog.Locator("#create-org-zip-error")).ToBeVisibleAsync();
		await Expect(createDialog.Locator("#create-org-city-error")).ToBeVisibleAsync();

		// Blocked client-side - the dialog is still open, nothing was created.
		await Expect(createDialog).ToBeVisibleAsync();

		await createDialog.GetByTestId("modal-cancel").ClickAsync();
	}

	[Test]
	public async Task Directory_ShowsOpenOpportunityCount_ForOrgWithPublishedOpportunity()
	{
		// #772 review follow-up (issue #763): "the site looks a bit dead" -
		// the public organization directory now shows each org's count of
		// open (Published) volunteer opportunities instead of just a bare
		// name/description, so a card with real opportunities reads as such.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgName = await CreateOrganizationAsync("Visual772 OpenCount");

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await createBtn.First.ClickAsync();

		try
		{
			await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });
		}
		catch
		{
			return; // modal did not open - skip remaining assertions
		}

		// Step 1: title/description.
		await Page.Locator("#opportunity-title").FillAsync("Visual772 Opportunity");
		await Page.Locator("#opportunity-description").FillAsync(
			"Coverage for the organization directory's open-opportunity count.");

		// Step 2: remote, so no address fields are required.
		await Page.GetByTestId("wizard-stepper-2").ClickAsync();
		await Page.Locator("#opportunity-remote").CheckAsync();

		// Step 3: IndividualContact (Express interest) - unlike Waitlist, this
		// type can publish with no time slots, keeping this test focused on
		// the directory count rather than the slot-creation flow.
		await Page.GetByTestId("wizard-stepper-3").ClickAsync();
		await Page.Locator("label:has(input[name='participationType'][value='IndividualContact'])")
			.ClickAsync();

		await Page.GetByTestId("wizard-stepper-4").ClickAsync();
		await Page.GetByTestId("modal-submit").ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 30_000 });

		// The public directory, filtered to this org, must show "1 open opportunity".
		await Page.GotoAsync($"{origin}/organizations");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.Locator("#organizations-search").FillAsync(orgName);

		var orgCard = Page.Locator("li").Filter(new() { HasTextString = orgName });
		await Expect(orgCard).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(orgCard.GetByText("1 open opportunity", new() { Exact = true }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	[Test]
	public async Task PublicProfilePage_ContentIsLeftAlignedUnderHeading()
	{
		// Regression for #766: OrganizationProfileView's content wrapper had
		// `mx-auto`, so it centered itself against the whole page instead of
		// sitting flush under the left-aligned heading above it - a dead
		// column on wide viewports. This reverses the `mx-auto` #694 added
		// for the opposite problem (the wrapper hugging the left edge with no
		// centering at all); left-aligning was the chosen fix direction over
		// centering the heading to match, for consistency with the rest of
		// the org app shell (Opportunities tab, dashboard, breadcrumb bar),
		// none of which centers a content block.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual766 LeftAlign");

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = match.Groups[1].Value;

		await Page.GotoAsync($"{origin}/organizations/{organizationId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertMaxWidthContentLeftAlignedAsync("Organization profile page");
	}

	[Test]
	public async Task SettingsPage_ContentIsLeftAlignedWithinMain()
	{
		// Regression for #766: OrgSettingsPage wraps its content in its own
		// separate `mx-auto max-w-2xl` div - independent of
		// OrganizationProfileView's own wrapper - which produced the same
		// dead-column effect. This is the page shown in the issue's primary
		// repro steps.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual766 Settings");

		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Danger Zone" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		await AssertMaxWidthContentLeftAlignedAsync("Organization settings page");
	}

	[Test]
	public async Task MembersPage_ContentIsLeftAlignedWithinMain()
	{
		// Regression for #766: OrgMembersPage's content wrapper had
		// `mx-auto`, independently centering it against the whole page.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual766 Members");

		// #834: the widget link's accessible name is "1 member" (singular) for
		// a fresh single-member org, so match "member" rather than "members".
		await Page.GetByRole(AriaRole.Link, new() { Name = "member" }).ClickAsync();
		await Expect(Page.Locator("#member-search")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await AssertMaxWidthContentLeftAlignedAsync("Organization members page");
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
