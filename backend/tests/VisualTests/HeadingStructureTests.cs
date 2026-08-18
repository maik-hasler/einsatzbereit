using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #2071: /organizations rendered every card's name in a plain
/// &lt;strong&gt;, with no heading anywhere in its main content, while
/// /opportunities gave every result card a fixed &lt;h2&gt; with nothing above it
/// naming the region - on a page that pages in ~9 cards at once, that read as a
/// run of indistinguishable level-2 headings once the footer's own CTA and
/// three link-column headings (also fixed &lt;h2&gt;s) followed right after.
///
/// The fix: both directories get a visually-hidden results-region &lt;h2&gt;, the
/// /opportunities cards drop to &lt;h3&gt; underneath it, and the footer demotes its
/// own headings to &lt;h3&gt; specifically on that route (see Footer's headingLevel
/// prop and AppLayout) so they read as subordinate to the grid instead of more
/// of the same level-2 run.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class HeadingStructureTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OrganizationsPage_CardNameIsAHeading_UnderADistinctResultsRegionHeading()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var olaf = await Fixture.SignInAsync("olaf", "olaf123");
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olaf.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgName = $"HeadingA11y Org {suffix}";
		(await http.PostAsJsonAsync("/v1/organizations", new { name = orgName })).EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/organizations");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.Locator("#organizations-search").FillAsync(orgName);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = orgName, Level = 2 }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		// The card heading's parent: sr-only, so it costs sighted users nothing
		// (the visible result count above already states this in prose), but it
		// gives the run of per-card headings below a name in the outline.
		var resultsHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Search results", Level = 2 });
		await Expect(resultsHeading).ToHaveCountAsync(1);
		var box = await resultsHeading.BoundingBoxAsync();
		box.Should().NotBeNull();
		box!.Height.Should().BeLessThan(4, "the results-region heading must stay sr-only");
	}

	[Test]
	public async Task OpportunitiesPage_CardsAreLevel3_AndFooterHeadingsDemoteOnThisRouteOnly()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var olaf = await Fixture.SignInAsync("olaf", "olaf123");
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olaf.AccessToken}");

		var suffix = Guid.NewGuid().ToString("N");
		var tag = $"heading2071-{suffix}";

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"HeadingA11y Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"HeadingA11y Opportunity {suffix}";
		(await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = oppTitle,
			descriptionDe = "Seeded by HeadingStructureTests.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
			tags = new[] { tag },
		})).EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/opportunities?tag={tag}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = oppTitle, Level = 3 }))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		var resultsHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Search results", Level = 2 });
		await Expect(resultsHeading).ToHaveCountAsync(1);
		var box = await resultsHeading.BoundingBoxAsync();
		box.Should().NotBeNull();
		box!.Height.Should().BeLessThan(4, "the results-region heading must stay sr-only");

		// The footer's own headings sit right below the result grid on this
		// specific route - demoted to h3 so they read as subordinate to it
		// instead of continuing the same level-2 run the (now level-3) cards
		// broke out of. Scrolled into view first: the footer sits well below
		// the fold behind a tall result grid.
		var footerHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Platform" });
		await footerHeading.ScrollIntoViewIfNeededAsync();
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Platform", Level = 3 }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Platform", Level = 2 }))
			.Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task OrganizationProfilePage_FooterHeadingsStayLevel2_UnaffectedByOpportunitiesRouteDemotion()
	{
		// Footer's headingLevel demotion is scoped to /opportunities in
		// AppLayout - every other route keeps the footer's headings at their
		// default h2, unchanged by #2071's fix.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/organizations");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var footerHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Platform" });
		await footerHeading.ScrollIntoViewIfNeededAsync();
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Platform", Level = 2 }))
			.ToBeVisibleAsync();
	}
}
