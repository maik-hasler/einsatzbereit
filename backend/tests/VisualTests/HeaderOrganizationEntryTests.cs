using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// #1785: the org app is an organizer's main workspace, and it used to be
/// reachable only by opening the account dropdown and then a second disclosure
/// inside it - the header carried 5 hrefs before that click and 13 after, and
/// none of the org ones were visible until both were open. The organization is
/// a primary destination now, labelled with its own name, on both breakpoints,
/// gated on membership (GetOrganizations returns member organizations, not
/// organizer ones) - and the account menu is back to personal items only.
///
/// It takes the "for organizations" slot rather than adding a fifth label: that
/// slot pitches the landing page's section to people who have no organization
/// yet, and the desktop nav - which since #1811 renders only from `lg` up,
/// because the German labels do not fit a tablet row at all - has no width to
/// spare for a fifth entry even there. The width case below keeps that honest.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class HeaderOrganizationEntryTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int MobileWidth = 390;

	private const int MobileHeight = 844;

	// Tailwind's `lg`, inclusive: since #1811 this is the narrowest width that
	// renders the desktop nav at all, and therefore the tightest fit this entry
	// ever has to survive - see HeaderNavBreakpointTests for the anonymous
	// half of the same guarantee.
	private const int DesktopWidth = 1024;

	[Test]
	public async Task MobileMenu_Member_ShowsTheOrgAppAmongThePrimaryDestinations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// FastSignInAsync verifies auth via the desktop "User menu" button,
		// CSS-hidden below md - sign in first, shrink after.
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);

		var banner = Page.GetByRole(AriaRole.Banner);
		await banner.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First
			.ClickAsync(new() { Timeout = 10_000 });

		// Among the primary destinations at the top of the panel, not under the
		// account items and not behind a disclosure.
		var entry = banner.GetByTestId("mobile-nav-organization");
		await Expect(entry).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(entry).ToHaveAttributeAsync("href", $"/app/{pinnedOrgId!.Value}/dashboard");

		await entry.ClickAsync();
		await Page.WaitForURLAsync($"{origin}/app/{pinnedOrgId.Value}/dashboard", new() { Timeout = 15_000 });
	}

	[Test]
	public async Task DesktopHeader_AtTheDesktopBreakpoint_TheEntryFitsWithoutOverflowingTheRow()
	{
		// This entry is the widest thing the nav carries (~210px against the
		// ~137px "Fuer Organisationen" it takes the place of), and #1811 left
		// the labels `whitespace-nowrap` - so the next shape of #1793 would not
		// be a wrapped label but a horizontally scrolling page. 1024px is where
		// that would show first: a signed-in row has ~163px of slack there,
		// which is why this entry replaces a label instead of joining them.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.SetViewportSizeAsync(DesktopWidth, 1024);

		var entry = Page.GetByTestId("nav-organization");
		await Expect(entry).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var overflow = await Page.EvaluateAsync<int>(
			"() => document.documentElement.scrollWidth - document.documentElement.clientWidth");
		overflow.Should().BeLessThanOrEqualTo(0,
			"the organization entry must not push the page into horizontal scroll");

		// Measured against "Hilfe", a single short word that cannot wrap at any
		// width - the same self-calibrating reference HeaderNavBreakpointTests
		// uses, rather than a hardcoded pixel height that drifts with the type
		// scale. The entry is taller than a plain label by its 20px avatar, so
		// this only has to rule out a second line.
		var reference = await Page.GetByTestId("nav-help").BoundingBoxAsync();
		var box = await entry.BoundingBoxAsync();
		reference.Should().NotBeNull();
		box.Should().NotBeNull();
		box!.Height.Should().BeLessThan(reference!.Height * 2,
			"a long organization name must truncate, not wrap the entry onto a second line");

		// ...and still show more than a sliver of the name - the failure mode
		// #809/#1117 hit in the org switcher. Only a name long enough to be
		// truncated at all can show that.
		var label = (await entry.InnerTextAsync()).Trim();
		if (label.Length < 20)
			Skip.Test("seed data changed - the resolved organization's name is too short to truncate");

		box.Width.Should().BeGreaterThan(60, "the name must stay readable, not collapse to its first letter");
	}
}
