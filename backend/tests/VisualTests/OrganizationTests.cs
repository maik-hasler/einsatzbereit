using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

// #1316: SoleOrganizer_TwoMemberOrg_MembersPage_StillDisablesLeave below
// needs vera's global Keycloak organisator role deterministically cleared -
// opts the whole class into fixture.ResetAsync() and a keyed [NotInParallel]
// so only other classes sharing the "visualtests-db" key (not the whole
// assembly) are excluded while this one resets that role.
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("visualtests-db")]
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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// A throwaway org rather than whichever seeded one olaf happens to be
		// pinned to, for the same reason the Organizer-role invite test below
		// uses one: vera is a baseline member of one of the two seeded orgs, so
		// on that org she never surfaces as an invitable candidate at all
		// ("No users found."). Which of the two is pinned is decided by
		// alphabetical order (activeOrg.ts's resolveActiveOrg fallback), which
		// a rename of the seed data silently flips - and did.
		await CreateOrganizationAsync("Visual579 InviteMember", pinnedOrgId!.Value);

		// Members lives in the page header's section rail (OrgPageHeader.tsx) -
		// the same rail an organizer uses, and unambiguous unlike a bare
		// "member" name match, which the Settings widget's own member-count link
		// also answers to.
		await Page.GetByTestId("org-tab-members").ClickAsync();

		await Page.Locator("#member-search").FillAsync("vera");

		var inviteButton = Page.GetByRole(AriaRole.Button, new() { Name = "Invite" });

		// A freshly created org has olaf as its only member and no invitation
		// rows at all, so vera is always an invitable candidate here. Assert
		// the invite button directly instead of tolerating "No users found."
		// as an alternate outcome.
		await Expect(inviteButton).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await inviteButton.First.ClickAsync();

		// No 403/401-driven error banner ("Could not send invitation.").
		await Expect(Page.GetByText("Could not send invitation.")).Not.ToBeVisibleAsync();

		await Expect(Page.GetByText("Invitation sent.")).ToBeVisibleAsync();
		await Expect(Page.GetByText("Pending invitations")).ToBeVisibleAsync();
	}

	[Test]
	public async Task MemberSearch_RequiresFourCharacters_AndNeverExposesCandidateEmail()
	{
		// Regression for #1170: any authenticated user could self-create an
		// organization to become an organizer, then abuse this search - a
		// 2-char minimum, each result carrying the candidate's email address -
		// to enumerate the realm-wide user directory. Verifies a 3-char query
		// returns nothing and a matching search never renders an email
		// address in the results.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual1170 MemberSearch", pinnedOrgId!.Value);

		await Page.GetByTestId("org-tab-members").ClickAsync();

		await Page.Locator("#member-search").FillAsync("ver");
		await Page.WaitForTimeoutAsync(800);
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Invite" })).Not.ToBeVisibleAsync();

		await Page.Locator("#member-search").FillAsync("vera");
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Invite" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Expect(Page.GetByText("vera@example.com")).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task SoleMember_MembersPage_ShowsDisabledLeaveInsteadOfRemove()
	{
		// #580: the org's sole member must see a disabled "Leave" action on
		// their own row, never "Remove" - removing them would orphan the org.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual580 Leave", pinnedOrgId!.Value);

		// Members lives in the page header's section rail (OrgPageHeader.tsx) -
		// the same rail an organizer uses, and unambiguous unlike a bare
		// "member" name match, which the Settings widget's own member-count link
		// also answers to.
		await Page.GetByTestId("org-tab-members").ClickAsync();

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
		// an Organizer here too. The backend guard itself is covered
		// deterministically by
		// IntegrationTests.OrganizationSettingsTests.RemoveMember_ShouldReturn409_WhenSoleOrganizerLeaves_EvenThoughAnotherMemberRemains.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual825 TwoMember", pinnedOrgId!.Value);

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

		// Members lives in the page header's section rail (OrgPageHeader.tsx) -
		// the same rail an organizer uses, and unambiguous unlike a bare
		// "member" name match, which the Settings widget's own member-count link
		// also answers to.
		await Page.GetByTestId("org-tab-members").ClickAsync();

		var leaveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Leave" });
		await Expect(leaveButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(leaveButton).ToBeDisabledAsync();

		// Unlike the sole-member case, a second member exists to remove -
		// proving the guard is driven by organizer count, not member count.
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Remove" })).ToBeVisibleAsync();
	}

	[Test]
	public async Task RemoveMember_ShowsConfirmationDialog_AndOnlyRemovesAfterConfirm()
	{
		// Regression for #1231: the Members page's "Remove" button used to call
		// RemoveMember directly on click - no confirmation, no way to back out,
		// unlike every other destructive action on this page (Leave, Delete
		// Organization) which already goes through ConfirmDialog. Verifies
		// "Remove" now opens a dialog naming the member, "Keep" cancels without
		// removing them, and only "Yes, remove" actually calls the API.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual1231 RemoveConfirm", pinnedOrgId!.Value);

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = Guid.Parse(match.Groups[1].Value);

		var vera = await Fixture.SignInAsync("vera", "vera123");
		await Fixture.AddPlainMemberDirectlyAsync(organizationId, vera.UserId);

		// OrgAppLayout only refetches org details on organizationId change, so
		// the dashboard we're still on would otherwise keep showing its
		// pre-membership snapshot (olaf as sole member) - force a refetch, same
		// as the SoleOrganizer test above.
		await Page.ReloadAsync();

		await Page.GetByTestId("org-tab-members").ClickAsync();

		// Scope to vera's row by her stable seed email rather than her display
		// name: other tests in this shared session (e.g. ProfileOverviewTests)
		// rename her last name to "Sample", and that mutation isn't reset
		// between tests, so asserting a specific full name here is order-
		// dependent flakiness waiting to happen.
		var veraRow = Page.Locator("li", new() { HasText = "vera@example.com" });
		await Expect(veraRow).ToBeVisibleAsync(new() { Timeout = 10_000 });
		var veraName = await veraRow.Locator("p").First.TextContentAsync();
		veraName.Should().NotBeNullOrEmpty();

		var removeButton = veraRow.GetByRole(AriaRole.Button, new() { Name = "Remove" });
		await removeButton.ClickAsync();

		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog.GetByText(veraName!)).ToBeVisibleAsync();

		// "Keep" must close the dialog without removing the member.
		await dialog.GetByRole(AriaRole.Button, new() { Name = "Keep" }).ClickAsync();
		await Expect(dialog).Not.ToBeVisibleAsync();
		await Expect(veraRow).ToBeVisibleAsync();

		// "Yes, remove" on a second attempt actually removes them.
		await removeButton.ClickAsync();
		await Expect(dialog).ToBeVisibleAsync();
		await dialog.GetByRole(AriaRole.Button, new() { Name = "Yes, remove" }).ClickAsync();

		await Expect(dialog).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(veraRow).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task Organizer_CanPromoteAndDemoteMember_ViaMembersPage()
	{
		// #1050: OrganizationMembership.Role was create-only, so every member
		// was forcibly an Organizer with no promote/demote path. Verifies the
		// new "Promote to organizer"/"Demote to member" actions round-trip
		// through the API and persist (survive a reload), not just update
		// local state optimistically.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual1050 PromoteDemote", pinnedOrgId!.Value);

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = Guid.Parse(match.Groups[1].Value);

		var vera = await Fixture.SignInAsync("vera", "vera123");
		await Fixture.AddPlainMemberDirectlyAsync(organizationId, vera.UserId);

		// OrgAppLayout only refetches org details on organizationId change, so
		// the dashboard we're still on would otherwise keep showing its
		// pre-membership snapshot - force a refetch, same as other tests above.
		await Page.ReloadAsync();

		await Page.GetByTestId("org-tab-members").ClickAsync();

		// Scope to vera's row by her stable seed email rather than her display
		// name - see RemoveMember_ShowsConfirmationDialog_AndOnlyRemovesAfterConfirm
		// above for why.
		var veraRow = Page.Locator("li", new() { HasText = "vera@example.com" });
		await Expect(veraRow).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Plain member: no Organizer badge, a "Promote" action, no "Demote".
		// einsatzbereit#1294: these buttons' accessible names now interpolate
		// the member's own name in the middle ("Promote {name} to organizer"),
		// so match with a regex rather than the old literal substring.
		await Expect(veraRow.GetByText("Organizer", new() { Exact = true })).Not.ToBeVisibleAsync();
		var promoteButton = veraRow.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Promote .* to organizer") });
		await Expect(promoteButton).ToBeVisibleAsync();

		await promoteButton.ClickAsync();

		await Expect(veraRow.GetByText("Organizer", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = 10_000 });
		var demoteButton = veraRow.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Demote .* to member") });
		await Expect(demoteButton).ToBeVisibleAsync();

		// Reload to prove the promotion was actually persisted server-side,
		// not just an optimistic local-state update. Already on the members
		// page (navigated here via the dashboard's member-count link earlier)
		// - the "member" link lives only on the dashboard's SettingsWidget, not
		// on this page, so reloading this page directly is all that's needed;
		// re-clicking that link here would look for an element that doesn't
		// exist on /dashboard/members and hang until timeout.
		await Page.ReloadAsync();
		await Expect(veraRow).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(veraRow.GetByText("Organizer", new() { Exact = true })).ToBeVisibleAsync();

		// Demote back to Member - olaf remains an organizer, so this is allowed.
		await veraRow.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Demote .* to member") }).ClickAsync();

		await Expect(veraRow.GetByText("Organizer", new() { Exact = true })).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(veraRow.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Promote .* to organizer") })).ToBeVisibleAsync();
	}

	[Test]
	public async Task MembersPage_ActionButtons_MeetMinimumTouchTargetSize()
	{
		// Regression for #1847: "Promote to organizer"/"Demote to member",
		// "Remove" and "Leave" rendered as bare text-xs buttons with no padding
		// beyond line-height, measuring ~16px tall at a 375px viewport - under
		// half the WCAG 2.2 SC 2.5.8 24x24 CSS px minimum - with "Promote" and
		// the destructive "Remove" only ~11px apart, a real mis-tap risk on a
		// touch screen.
		const float MinTargetSize = 24;
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual1847 TouchTarget", pinnedOrgId!.Value);

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = Guid.Parse(match.Groups[1].Value);

		var vera = await Fixture.SignInAsync("vera", "vera123");
		await Fixture.AddPlainMemberDirectlyAsync(organizationId, vera.UserId);

		// OrgAppLayout only refetches org details on organizationId change - force
		// a refetch, same as the other member-row tests above.
		await Page.ReloadAsync();

		await Page.GetByTestId("org-tab-members").ClickAsync();

		var veraRow = Page.Locator("li", new() { HasText = "vera@example.com" });
		await Expect(veraRow).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Resize down to the viewport the review measured the violation at,
		// after navigating - the org app's section rail is reached through the
		// default desktop viewport here, same as every other test in this file.
		await Page.SetViewportSizeAsync(375, 812);

		var promoteButton = veraRow.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Promote .* to organizer") });
		var removeButton = veraRow.GetByRole(AriaRole.Button, new() { Name = "Remove" });
		var leaveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Leave" });
		await Expect(leaveButton).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var promoteBox = await promoteButton.BoundingBoxAsync();
		var removeBox = await removeButton.BoundingBoxAsync();
		var leaveBox = await leaveButton.BoundingBoxAsync();
		promoteBox.Should().NotBeNull("Could not get bounding box for the Promote button");
		removeBox.Should().NotBeNull("Could not get bounding box for the Remove button");
		leaveBox.Should().NotBeNull("Could not get bounding box for the Leave button");

		(promoteBox!.Width >= MinTargetSize && promoteBox.Height >= MinTargetSize).Should().BeTrue(
			$"Promote hit target should meet the WCAG 2.2 24x24 minimum (measured {promoteBox.Width:F1}x{promoteBox.Height:F1}px)");
		(removeBox!.Width >= MinTargetSize && removeBox.Height >= MinTargetSize).Should().BeTrue(
			$"Remove hit target should meet the WCAG 2.2 24x24 minimum (measured {removeBox.Width:F1}x{removeBox.Height:F1}px)");
		(leaveBox!.Width >= MinTargetSize && leaveBox.Height >= MinTargetSize).Should().BeTrue(
			$"Leave hit target should meet the WCAG 2.2 24x24 minimum (measured {leaveBox.Width:F1}x{leaveBox.Height:F1}px)");

		// Promote sits directly left of the destructive Remove in the same row -
		// their hit targets must stay clearly separated, not just non-overlapping.
		double gap = removeBox.X - (promoteBox.X + promoteBox.Width);
		(gap >= 8).Should().BeTrue(
			$"Promote and Remove hit targets should stay clearly separated to avoid a mis-tap between a role change and a destructive action (measured {gap:F1}px)");
	}

	[Test]
	public async Task Organizer_CanInviteMemberWithOrganizerRole_ViaRoleSelector()
	{
		// #1050: CreateInvitation now carries an intended role, defaulting to
		// Member (the previous behavior always granted Organizer regardless of
		// intent). Verifies the role selector lets an organizer explicitly
		// invite someone as an Organizer instead, shown on the pending
		// invitation.
		//
		// Uses a throwaway org (not olaf's pinned/seeded one) so that inviting
		// vera here can never collide with the invitation
		// Organisator_InviteMemberFromDashboard_SendsInvitationInsteadOf403
		// above sends: organization_invitation rows aren't cleared between
		// tests, so a second invite into the same org would 409 with
		// OrganizationInvitation.AlreadyInvited depending on run order.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual1050 InviteRole", pinnedOrgId!.Value);

		await Page.GetByTestId("org-tab-members").ClickAsync();

		await Page.Locator("#member-search").FillAsync("vera");

		var inviteButton = Page.GetByRole(AriaRole.Button, new() { Name = "Invite" });
		await Expect(inviteButton).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.Locator("#invite-role").SelectOptionAsync("Organizer");
		await inviteButton.First.ClickAsync();

		await Expect(Page.GetByText("Invitation sent.")).ToBeVisibleAsync();

		var pendingSection = Page.Locator("li", new() { HasTextString = "vera" }).First;
		await Expect(pendingSection.GetByText("Organizer", new() { Exact = true }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	[Test]
	public async Task Organizer_CanDismissPendingInvitation_RevokingItBeforeAcceptance()
	{
		// Regression for #1040: a pending invitation had no dismiss control at
		// all (only Declined/Expired rows did), and the backend explicitly
		// rejected dismissing one - so an organizer who invited the wrong
		// person had no way to revoke it before that person could accept and
		// gain full Organizer access. Verifies the same "Dismiss" control
		// already available on Declined/Expired rows now also appears - and
		// works - for a still-Pending one, and that the removal survives a
		// reload (persisted, not just optimistic local state).
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Throwaway org, not olaf's pinned/seeded one - other tests in this
		// shared session already invite vera into that org, and
		// organization_invitation rows aren't cleared between tests, so
		// reusing it here could 409 with AlreadyInvited depending on run order.
		await CreateOrganizationAsync("Visual1040 DismissPending", pinnedOrgId!.Value);

		await Page.GetByTestId("org-tab-members").ClickAsync();

		await Page.Locator("#member-search").FillAsync("vera");
		var inviteButton = Page.GetByRole(AriaRole.Button, new() { Name = "Invite" });
		await Expect(inviteButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await inviteButton.First.ClickAsync();

		await Expect(Page.GetByText("Invitation sent.")).ToBeVisibleAsync();
		await Expect(Page.GetByText("Pending invitations")).ToBeVisibleAsync();

		var dismissButton = Page.GetByRole(AriaRole.Button, new() { Name = "Dismiss" });
		await Expect(dismissButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await dismissButton.ClickAsync();

		await Expect(Page.GetByText("Could not dismiss invitation.")).Not.ToBeVisibleAsync();
		await Expect(Page.GetByText("Pending invitations")).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.ReloadAsync();
		await Expect(Page.Locator("#member-search")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByText("Pending invitations")).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task SoleMember_CanDeleteOrganization_FromSettingsPage()
	{
		// #580: the new "Delete organization" action, enabled only for the
		// sole remaining member, must actually delete the org and go home.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgName = await CreateOrganizationAsync("Visual580 Delete", pinnedOrgId!.Value);
		var orgId = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard").Groups[1].Value;

		// #771: reach Settings via the dashboard's Settings widget link, not a tab.
		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();

		// #1792 dropped the button's Title Case so it doesn't clash with the
		// panel heading right above it.
		var deleteButton = Page.GetByRole(AriaRole.Button, new() { Name = "Delete organization" });
		await Expect(deleteButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(deleteButton).ToBeEnabledAsync();
		await deleteButton.ClickAsync();

		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog.GetByText(orgName)).ToBeVisibleAsync();

		await dialog.GetByRole(AriaRole.Button, new() { Name = "Yes, delete" }).ClickAsync();

		await Page.WaitForURLAsync($"{origin}/", new() { Timeout = 10_000 });

		// Regression for #1331: the redirect alone proves nothing about whether
		// DELETE actually deleted anything - a swallowed exception or a rolled-
		// back transaction would redirect home just the same. Assert the org is
		// actually gone via the backend directly: its public profile 404s, and
		// it no longer appears in the public directory a volunteer would browse.
		var backend = Fixture.GetEndpoint("backend");
		using var http = new HttpClient { BaseAddress = backend };

		var profileResponse = await http.GetAsync($"/v1/organizations/{orgId}/profile");
		profileResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);

		var directoryResponse = await http.GetAsync(
			$"/v1/organizations/directory?pageNumber=1&pageSize=10&search={Uri.EscapeDataString(orgName)}");
		directoryResponse.EnsureSuccessStatusCode();
		var directory = await directoryResponse.Content.ReadFromJsonAsync<JsonElement>();
		directory.GetProperty("totalItems").GetInt32().Should().Be(0,
			"a deleted organization must not still be browsable in the public directory");
	}

	[Test]
	public async Task DangerZoneHint_BranchesOnMemberCount_TheWayTheDeleteButtonAlreadyDid()
	{
		// Regression for #1789: the danger zone's hint was one static string
		// ("...Remove other members first.") passed unconditionally as
		// DangerZonePanel's description, while only the button's `disabled`
		// prop branched on member count. So the sole member - the one person
		// who *can* delete - was told to remove members who are not there,
		// next to an enabled Delete button.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual1789 DangerZoneHint", pinnedOrgId!.Value);
		var organizationId = Guid.Parse(Regex.Match(Page.Url, @"/app/([^/]+)/dashboard").Groups[1].Value);

		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();

		var deleteButton = Page.GetByRole(AriaRole.Button, new() { Name = "Delete organization" });
		await Expect(deleteButton).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Sole member: enabled button, and copy that agrees with it.
		await Expect(deleteButton).ToBeEnabledAsync();
		await Expect(Page.GetByText("You are this organization's sole remaining member, so you can delete it."))
			.ToBeVisibleAsync();
		await Expect(Page.GetByText("Remove other members first.")).ToHaveCountAsync(0);

		// A second plain member (same escape hatch as the two-member members
		// page test above, since accepting an invitation would grant Organizer
		// too) makes the original sentence true again - and it must come back.
		var vera = await Fixture.SignInAsync("vera", "vera123");
		await Fixture.AddPlainMemberDirectlyAsync(organizationId, vera.UserId);

		// OrgAppLayout only refetches org details on organizationId change, so
		// without a reload the page keeps its pre-membership snapshot.
		await Page.ReloadAsync();

		await Expect(deleteButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(deleteButton).ToBeDisabledAsync();
		await Expect(Page.GetByText(
			"Only the organization's sole remaining member can delete it. Remove other members first."))
			.ToBeVisibleAsync();
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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

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

		// Wait for the create itself to finish before asserting anything about
		// where it navigated. CreateOrganizationModal only closes on success
		// (onSuccess then onClose, after the POST resolves); a failed create
		// keeps the dialog open with an inline error instead.
		//
		// This is a diagnostic split, not a fix for a known race: this test
		// failed once in CI (run 31158818536) with the switcher still showing
		// the previous org, and the single assertion below could not tell
		// "create failed", "create was still in flight" and "create succeeded
		// but navigation didn't happen" apart - all three render identically.
		// Splitting the wait means the next occurrence says which one it was.
		// The root cause is still unknown; four separate analyses failed to
		// find a mechanism that survived scrutiny, so nothing here claims to
		// remove it.
		await Expect(createDialog).Not.ToBeVisibleAsync(new() { Timeout = 30_000 });

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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

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

		// Name/street were filled in above, so Cancel must ask for confirmation
		// instead of silently discarding them (#1238).
		await createDialog.GetByTestId("modal-cancel").ClickAsync();
		var discardBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Discard changes" });
		await Expect(discardBtn).ToBeVisibleAsync();
		await discardBtn.ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync();
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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgName = await CreateOrganizationAsync("Visual772 OpenCount", pinnedOrgId!.Value);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await createBtn.First.ClickAsync();

		await Page.WaitForSelectorAsync("[role='dialog']", new() { Timeout = 5000 });

		// Step 1: title/description.
		await Page.Locator("#opportunity-title").FillAsync("Visual772 Opportunity");
		await Page.Locator("#opportunity-description").FillAsync(
			"Coverage for the organization directory's open-opportunity count.");

		// Step 2: remote, so no address fields are required.
		await Page.GetByTestId("wizard-stepper-2").ClickAsync();
		await Page.Locator("#opportunity-remote").CheckAsync();

		// Step 3: IndividualContact (Express interest) - unlike ScheduledSlots, this
		// type can publish with no time slots, keeping this test focused on
		// the directory count rather than the slot-creation flow.
		await Page.GetByTestId("wizard-stepper-3").ClickAsync();
		await Page.Locator("label:has(input[name='participationType'][value='IndividualContact'])")
			.ClickAsync();

		await Page.GetByTestId("wizard-stepper-4").ClickAsync();
		// Individual-contact opportunities need an application deadline before
		// they can be published (einsatzbereit#1086).
		await Page.Locator("#create-valid-until").FillAsync(DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"));
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
	public async Task Directory_ShowsTwoLetterMonogram_MatchingEveryOtherSurface()
	{
		// Regression for #1916: OrganizationsPage rendered org.name.charAt(0) - a
		// single letter, indistinguishable between organizations that share a
		// first word - while every other surface (header org switcher,
		// opportunity cards, org profile page) already renders getInitials' two-
		// letter monogram. Reuses the exact two organization names the issue
		// itself cited, already covered by initials.test.ts's assertions that
		// "Lindenauer Nachbarschaftshilfe e.V." -> "LN" and
		// "Lindenauer Tierschutzverein e.V." -> "LT" - the random suffix below is
		// appended to "Lindenauer" itself (no new word) so those two-letter
		// results still hold.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var olaf = await Fixture.SignInAsync("olaf", "olaf123");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olaf.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var neighborhoodName = $"Lindenauer{suffix} Nachbarschaftshilfe e.V.";
		var animalShelterName = $"Lindenauer{suffix} Tierschutzverein e.V.";

		foreach (var name in new[] { neighborhoodName, animalShelterName })
			(await http.PostAsJsonAsync("/v1/organizations", new { name })).EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/organizations");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.Locator("#organizations-search").FillAsync($"Lindenauer{suffix}");

		var neighborhoodCard = Page.Locator("li").Filter(new() { HasTextString = neighborhoodName });
		var animalShelterCard = Page.Locator("li").Filter(new() { HasTextString = animalShelterName });
		await Expect(neighborhoodCard).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(animalShelterCard).ToBeVisibleAsync();

		(await neighborhoodCard.Locator("span").First.TextContentAsync()).Should().Be("LN");
		(await animalShelterCard.Locator("span").First.TextContentAsync()).Should().Be("LT");
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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual766 LeftAlign", pinnedOrgId!.Value);

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = match.Groups[1].Value;

		await Page.GotoAsync($"{origin}/organizations/{organizationId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// #766 wanted this column not stranded as a narrow strip in a wide page.
		// #1755 gave the page a PageHeaderBand, which anchors the layout and
		// centres its own title at max-w-5xl - so the content is centred to match
		// it rather than flush left, and the invariant that matters is now that
		// the two share a left edge. The org app Settings tab, which has no band,
		// still asserts the flush-left arrangement below.
		await AssertMaxWidthContentCenteredAsync("Organization profile page");

		var edgeDelta = await Page.EvaluateAsync<double>(
			"""
			() => {
				const h1 = document.querySelector('main h1');
				const column = document.querySelector('main [data-content-wrapper]');
				return Math.abs(h1.getBoundingClientRect().left
					- column.getBoundingClientRect().left);
			}
			""");
		edgeDelta.Should().BeLessThan(2,
			"the band title and the content column below it must share a left edge");
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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual766 Settings", pinnedOrgId!.Value);

		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();
		// #1792: the panel heading is per-surface now ("Delete organization"
		// here, "Delete account" on /profile/settings) instead of a shared
		// "Danger zone".
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Delete organization" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		await AssertMaxWidthContentLeftAlignedAsync("Organization settings page");
	}

	[Test]
	public async Task MembersPage_ContentIsLeftAlignedWithinMain()
	{
		// Regression for #766: OrgMembersPage's content wrapper had
		// `mx-auto`, independently centering it against the whole page.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual766 Members", pinnedOrgId!.Value);

		// #834: the widget link's accessible name is "1 member" (singular) for
		// a fresh single-member org, so match "member" rather than "members".
		await Page.GetByTestId("org-tab-members").ClickAsync();
		await Expect(Page.Locator("#member-search")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await AssertMaxWidthContentLeftAlignedAsync("Organization members page");
	}

	private async Task<string> CreateOrganizationAsync(string namePrefix, Guid pinnedOrgId)
	{
		// New orgs are created via the org switcher's "Create organization" entry
		// - reachable from within any org the caller already organizes (olaf's
		// seed data always has at least one).
		var orgName = $"{namePrefix} {Guid.NewGuid():N}";
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId);
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
