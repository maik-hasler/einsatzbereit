using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// #2080: the org switcher pill truncated a long organization name from the
/// end at a fixed ~200px width. Olaf organizes "Lindenauer
/// Nachbarschaftshilfe e.V." and "Lindenauer Tierschutzverein e.V." - names
/// sharing both a leading word and a trailing "e.V." - so the old
/// end-truncation could cut both down to the same visible "Lindenauer...",
/// with nothing telling them apart even though everything an organizer does
/// is attributed to whichever org this pill names.
///
/// Fixed in OrganizationSwitcher.tsx by widening the pill to ~340px
/// (`max-w-85`) and replacing the single CSS `truncate` span with a
/// head/tail split (`splitForMiddleTruncation` in middleTruncateSplit.ts):
/// the head can grow the browser's own end-ellipsis when squeezed, the tail
/// never does. That keeps the org name's DOM text exactly the real name at
/// all times (a screen reader or a `.textContent()` read still gets the
/// whole thing) while only the *rendered* width adapts - and puts the word
/// that actually differs, right after the shared "Lindenauer " prefix, in
/// the half of the string that survives however much of the head an actual
/// render can show.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrganizationSwitcherTruncationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task Switcher_NamePillIsWidenedTo340px()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var nameSpan = Page.GetByTestId("org-switcher-current-name");
		await Expect(nameSpan).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var maxWidth = await nameSpan.EvaluateAsync<string>("el => getComputedStyle(el).maxWidth");
		maxWidth.Should().Be("340px",
			"the pill was widened from its old 200px ceiling so a realistic org name has "
			+ "room to show before anything needs to truncate at all");
	}

	[Test]
	public async Task Switcher_HeadAndTailReconstructTheExactOrgName()
	{
		// Narrowed to mobile width, where the name span has the least room -
		// the case most likely to force the head span into actually
		// truncating (see AuthHelper/OrgAppMobileResponsiveTests for why this
		// viewport is the tight one).
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
		await Page.SetViewportSizeAsync(375, 812);

		var nameSpan = Page.GetByTestId("org-switcher-current-name");
		var head = Page.GetByTestId("org-switcher-current-name-head");
		var tail = Page.GetByTestId("org-switcher-current-name-tail");
		await Expect(nameSpan).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var fullText = await nameSpan.InnerTextAsync();
		var headText = await head.InnerTextAsync();
		var tailText = await tail.InnerTextAsync();
		(headText + tailText).Should().Be(fullText,
			"the head/tail split must reconstruct the exact org name - only the rendering "
			+ "may be clipped, the DOM text must never become a fixed-length approximation");

		// Only the head is allowed to grow the browser's own ellipsis - if the
		// tail carried it too, this would just be the old end-truncation
		// moved one span over rather than an actual middle-ellipsis.
		var tailTextOverflow = await tail.EvaluateAsync<string>("el => getComputedStyle(el).textOverflow");
		tailTextOverflow.Should().NotBe("ellipsis");
	}

	[Test]
	public async Task Switcher_DivergingWordSurvivesInTheHeadForOrgsSharingAPrefix()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
		await Page.SetViewportSizeAsync(375, 812);

		var head = Page.GetByTestId("org-switcher-current-name-head");
		await Expect(head).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var nameSpan = Page.GetByTestId("org-switcher-current-name");
		var startingName = await nameSpan.InnerTextAsync();
		if (startingName != "Lindenauer Nachbarschaftshilfe e.V."
			&& startingName != "Lindenauer Tierschutzverein e.V.")
		{
			Skip.Test("seed data changed - nothing to compare against");
		}
		var headForStartingOrg = await head.InnerTextAsync();

		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }).ClickAsync();
		var otherOrgName = startingName == "Lindenauer Nachbarschaftshilfe e.V."
			? "Lindenauer Tierschutzverein e.V."
			: "Lindenauer Nachbarschaftshilfe e.V.";
		var otherOrgRow = Page.GetByTestId("org-switch-row").Filter(new() { HasText = otherOrgName });

		// #1708: the switcher panel's rows can render a beat after the click
		// that opens it - wait for the row itself rather than racing its mount.
		try
		{
			await otherOrgRow.WaitForAsync(new() { Timeout = 10_000 });
		}
		catch (TimeoutException)
		{
			Skip.Test("seed data changed - nothing to compare against");
		}
		await otherOrgRow.ClickAsync();
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });

		await Expect(nameSpan).ToHaveTextAsync(otherOrgName, new() { Timeout = 15_000 });
		var headForOtherOrg = await head.InnerTextAsync();

		headForOtherOrg.Should().NotBe(headForStartingOrg,
			"the two org names share both a leading word and a trailing \"e.V.\" - if the "
			+ "head rendered identically for both, the fix regressed back to showing only "
			+ "the shared prefix (#2080)");
	}
}
