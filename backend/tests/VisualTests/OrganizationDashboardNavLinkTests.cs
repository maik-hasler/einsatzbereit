using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// The mobile burger menu reaches every org dashboard tab (issue #775), not just
/// the org app's landing tab, so a phone user isn't left with the desktop org
/// switcher and the homepage hero CTA. Gated the same way as the admin-only
/// "Administration" entry (see AdministrationNavLinkTests) and resolved via the
/// same active-org-cookie-then-alphabetical logic HomePage uses.
///
/// #1785 promoted those links out of the account section: the organization is one
/// of the panel's primary destinations now, labelled with its own name and linking
/// to the dashboard, with the remaining ORG_TABS sections listed under it - no
/// disclosure to expand first. The account menu's own copy of them is gone (see
/// HeaderOrganizationEntryTests, which covers the promoted entry itself).
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrganizationDashboardNavLinkTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int MobileWidth = 375;
	private const int MobileHeight = 812;

	[Test]
	public async Task MobileMenu_UserWithOrg_ListsEveryOrgSection_AndNavigatesToThem()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		// Registered before FastSignInAsync's own navigation - Page.WaitForResponseAsync
		// must start listening before the request fires, not after - matching the
		// pattern already established in this suite (see CheckInPinOrganizerSetTests.cs).
		// Anchors the live DB query below to the instant the frontend's own
		// GET /v1/organizations actually resolves, instead of to <main> becoming
		// visible: AppLayoutInner renders <main> unconditionally, uncorrelated
		// with that fetch, so a query fired right after <main> is visible could
		// still land before, during, or well after Header's own fetch actually
		// completes.
		var organizationsResponseTask = Page.WaitForResponseAsync(
			r => r.Url.EndsWith("/v1/organizations") && r.Request.Method == "GET");

		// Olaf organizes an org in seed data (see HomePageOrgCtaTests).
		// FastSignInAsync verifies auth by waiting for the desktop "User menu"
		// button, which is CSS-hidden below the md breakpoint - so sign in
		// before shrinking to a mobile viewport, not after.
		//
		// pinActiveOrg: false - this test's own subject is the active-org-cookie
		// -then-alphabetical resolution order (see this class's doc comment), so
		// it deliberately stays on the unpinned path every other FastSignInAsync
		// call site now skips, to keep that resolution order under real coverage.
		await AuthHelper.FastSignInAsync(
			Page, Fixture, frontend, "olaf", "olaf123", pinActiveOrg: false);
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Queried immediately after the frontend's own GET /v1/organizations has
		// actually resolved (not just after <main> is visible) - see the
		// assertion below for why this needs to be a live query at all, and this
		// placement keeps it anchored to the same instant the frontend itself
		// resolved, instead of racing several more UI interactions' worth of
		// concurrently-running tests.
		await organizationsResponseTask;
		var expectedOrgId = await Fixture.GetCurrentFirstOrganizerOrganizationIdAsync(AspireFixture.OlafId);

		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		// Scope every mobile-menu lookup below to the <header> landmark
		// (implicit ARIA "banner" role) - the dashboard's own widgets also
		// link to some of these same destinations (e.g. Settings' member-count
		// link), so an unscoped GetByRole lookup could match either.
		var banner = Page.GetByRole(AriaRole.Banner);

		await banner.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		// The whole point of leaving this sign-in unpinned: confirm the
		// cookie-then-alphabetical fallback actually landed on whichever org was
		// genuinely alphabetically first for olaf at sign-in time (queried above) -
		// not just on *some* org that happens to satisfy a /app/<id>/ URL regex,
		// and not against a value pinned back at fixture boot
		// (AspireFixture.GetPinnedOrganizerOrganizationId): that snapshot is only
		// valid at the instant the fixture starts, before any other test has
		// created a single org. AchievementsTests, for one, permanently adds two
		// more Organizer orgs for olaf with no cleanup, sorting ahead of the
		// seeded one alphabetically - so whether the boot-time snapshot still
		// matched reality here depended on test scheduling, not on whether the
		// resolution logic under test actually worked. Every href below is
		// asserted against that live-resolved id.
		expectedOrgId.Should().NotBeNull("olaf organizes a seeded org, so the fallback should always resolve one for him");
		var orgId = expectedOrgId!.Value;

		// #1785: no disclosure to expand first - the organization leads the
		// group and links to the dashboard, its sections are listed under it.
		var entry = banner.GetByTestId("mobile-nav-organization");
		await Expect(entry).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(entry).ToHaveAttributeAsync("href", $"/app/{orgId}/dashboard");

		// Exact match - the homepage's hero/footer "Find opportunities" and
		// "Browse opportunities" links are still in the DOM behind the mobile
		// menu overlay and would otherwise ambiguously match too (Playwright's
		// default name matching is a case-insensitive substring match).
		await Expect(banner.GetByRole(AriaRole.Link, new() { Name = "Opportunities", Exact = true }))
			.ToHaveAttributeAsync("href", $"/app/{orgId}/dashboard/opportunities");

		// Regression coverage for #1680: the org-wide engagement queue
		// ("Sign-ups") previously had exactly one entry point (the dashboard's
		// To-Do widget) - it must stay reachable from ORG_TABS like every other
		// section. Exact match again: the account section's own "My sign-ups"
		// would otherwise match as well.
		await Expect(banner.GetByRole(AriaRole.Link, new() { Name = "Sign-ups", Exact = true }))
			.ToHaveAttributeAsync("href", $"/app/{orgId}/dashboard/engagements");

		// Matched on href rather than by accessible name: #1755 gave the mobile
		// menu an account section whose "Settings" entry (/profile/settings)
		// shares this one's exact label, so a name lookup resolves to two links.
		await Expect(banner.Locator($"a[href='/app/{orgId}/dashboard/settings']"))
			.ToHaveCountAsync(1);

		// One of them actually navigated, not just rendered with a right-looking
		// href - and Members is also the section whose own link the dashboard's
		// Settings widget shares, hence the banner scoping above.
		await banner.GetByRole(AriaRole.Link, new() { Name = "Members", Exact = true })
			.ClickAsync(new() { Timeout = 10_000 });
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard/members"), new() { Timeout = 15_000 });

		var resolvedOrgId = Guid.Parse(Regex.Match(Page.Url, @"/app/([^/]+)/dashboard").Groups[1].Value);
		resolvedOrgId.Should().Be(orgId);
	}

	[Test]
	public async Task MobileMenu_UserWithoutOrgs_HasNoOrganizationEntry()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		// admin has no organization memberships in seed data.
		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "admin", "admin123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Administration" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		// No org entry and no org sections - and the "for organizations" pitch
		// this entry would have taken the slot of is still there for them.
		await Expect(Page.GetByTestId("mobile-nav-organization")).ToHaveCountAsync(0);
		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Members", Exact = true }))
			.ToHaveCountAsync(0);
		await Expect(Page.GetByTestId("mobile-nav-forOrganizations")).ToBeVisibleAsync();
	}
}
