using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

// MembersPage_ActionButtons_MeetMinimumTouchTargetSize below needs vera's
// global Keycloak organisator role deterministically cleared: it locates the
// row's role control by the name "Promote ... to organizer", which reads
// "Demote ... to member" if she is already an organisator. So the whole class
// opts into fixture.ResetAsync() and a keyed [NotInParallel], keyed so only
// classes sharing "visualtests-db", not the whole assembly, are excluded
// while this one resets that role.
//
// einsatzbereit#2148 waves 10-11 moved the other cases that depended on the
// reset (the last-organizer guard, then promote/demote) down to
// OrgMembersPage.test.tsx. That touch-target case is now the only thing
// holding this class's serialisation in place, and it measures bounding boxes
// at a 375px viewport, so it cannot follow them.

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
		// The dashboard's Members tab must call CreateInvitation, not the
		// admin-only AddMember endpoint, which returns 403/401 for an organizer.
		// The invitee shows up under Pending Invitations with no error banner.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// A throwaway org, not whichever seeded one olaf is pinned to: vera is a
		// baseline member of one of the two, where she never surfaces as an
		// invitable candidate ("No users found."). Which one is pinned comes from
		// resolveActiveOrg's alphabetical fallback, which a seed-data rename flips.
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
	public async Task MembersPage_ActionButtons_MeetMinimumTouchTargetSize()
	{
		// The member-row actions must clear WCAG 2.2 SC 2.5.8's 24x24 CSS px
		// minimum at a 375px viewport, and keep "Promote" clear of the destructive
		// "Remove" beside it - bare text-xs buttons measure ~16px tall and land
		// ~11px apart, a real mis-tap risk on a touch screen.
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

		// Resize down after navigating - the org app's section rail is reached
		// through the default desktop viewport, same as every other test here.
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
	public async Task SoleMember_CanDeleteOrganization_FromSettingsPage()
	{
		// The "Delete organization" action, enabled only for the sole remaining
		// member, must actually delete the org and go home.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgName = await CreateOrganizationAsync("Visual580 Delete", pinnedOrgId!.Value);
		var orgId = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard").Groups[1].Value;

		// Reach Settings via the dashboard's Settings widget link, not a tab.
		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();

		// The button is sentence case so it does not clash with the panel heading
		// right above it.
		var deleteButton = Page.GetByRole(AriaRole.Button, new() { Name = "Delete organization" });
		await Expect(deleteButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(deleteButton).ToBeEnabledAsync();
		await deleteButton.ClickAsync();

		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog.GetByText(orgName)).ToBeVisibleAsync();

		await dialog.GetByRole(AriaRole.Button, new() { Name = "Yes, delete" }).ClickAsync();

		await Page.WaitForURLAsync($"{origin}/", new() { Timeout = 10_000 });

		// The redirect alone proves nothing - a swallowed exception or a rolled-back
		// transaction would redirect home just the same. Assert the org is gone via
		// the backend: its public profile 404s, and it no longer appears in the
		// public directory a volunteer would browse.
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
		// DangerZonePanel's description must branch on member count, not just the
		// button's `disabled` prop - otherwise the sole member, the one person who
		// *can* delete, is told to remove members who are not there, next to an
		// enabled Delete button.
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
	public async Task Directory_ShowsTwoLetterMonogram_MatchingEveryOtherSurface()
	{
		// OrganizationsPage must render getInitials' two-letter monogram like every
		// other surface (header org switcher, opportunity cards, org profile page),
		// not org.name.charAt(0), which is indistinguishable between organizations
		// sharing a first word. Uses two names initials.test.ts already covers
		// ("Lindenauer Nachbarschaftshilfe e.V." -> "LN",
		// "Lindenauer Tierschutzverein e.V." -> "LT"); the random suffix below is
		// appended to "Lindenauer" itself, adding no word, so those results hold.
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
			(await PostJsonWithRetryAsync(http, "/v1/organizations", new { name })).EnsureSuccessStatusCode();

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
		// OrganizationProfileView's content wrapper must not carry `mx-auto`: it
		// would centre against the whole page instead of sitting flush under the
		// left-aligned heading above it, leaving a dead column on wide viewports.
		// Left-aligning rather than centring the heading to match, for consistency
		// with the rest of the org app shell (Opportunities tab, dashboard,
		// breadcrumb bar), none of which centres a content block.
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

		// This page's PageHeaderBand anchors the layout and centres its own title at
		// max-w-5xl, so the content is centred to match it rather than flush left -
		// the invariant is that the two share a left edge. The org app Settings tab,
		// which has no band, asserts the flush-left arrangement below.
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
		// OrgSettingsPage wraps its content in its own `mx-auto max-w-2xl` div,
		// independent of OrganizationProfileView's wrapper, so it can strand the
		// same dead column separately.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual766 Settings", pinnedOrgId!.Value);

		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();
		// The panel heading is per-surface ("Delete organization" here, "Delete
		// account" on /profile/settings), not a shared "Danger zone".
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Delete organization" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		await AssertMaxWidthContentLeftAlignedAsync("Organization settings page");
	}

	[Test]
	public async Task MembersPage_ContentIsLeftAlignedWithinMain()
	{
		// OrgMembersPage's content wrapper centres independently of the two above.
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual766 Members", pinnedOrgId!.Value);

		// The widget link's accessible name is "1 member" (singular) for a fresh
		// single-member org, so match "member" rather than "members".
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
