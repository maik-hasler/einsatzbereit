using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

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
		var frontend = Fixture.GetEndpoint("frontend");
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
		await Page.SetViewportSizeAsync(375, 812);

		var nameSpan = Page.GetByTestId("org-switcher-current-name");
		var head = Page.GetByTestId("org-switcher-current-name-head");
		var tail = Page.GetByTestId("org-switcher-current-name-tail");
		await Expect(nameSpan).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var fullText = await nameSpan.TextContentAsync() ?? "";
		var headText = await head.TextContentAsync() ?? "";
		var tailText = await tail.TextContentAsync() ?? "";
		(headText + tailText).Should().Be(fullText,
			"the head/tail split must reconstruct the exact org name - only the rendering "
			+ "may be clipped, the DOM text must never become a fixed-length approximation");

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
