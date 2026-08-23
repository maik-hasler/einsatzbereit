using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

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
		(await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = orgName })).EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/organizations");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await Page.Locator("#organizations-search").FillAsync(orgName);

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = orgName, Level = 3 }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

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

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"HeadingA11y Org {suffix}" });
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

		var footerHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Platform" });
		await footerHeading.ScrollIntoViewIfNeededAsync();
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Platform", Level = 3 }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Platform", Level = 2 }))
			.Not.ToBeVisibleAsync();
	}
}
