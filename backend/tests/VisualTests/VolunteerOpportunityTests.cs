using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class VolunteerOpportunityTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task HomePage_RendersOpportunitiesList()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator("main")).ToBeVisibleAsync();
	}

	[Test]
	public async Task SearchFilter_UpdatesUrlWithSearchParam()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByPlaceholder("Search…").FillAsync("volunteer");

		await Expect(Page).ToHaveURLAsync(new Regex(@"\?.*search=volunteer"));
	}

	[Test]
	public async Task CityFilter_UpdatesUrlWithCityParam()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByPlaceholder("City…").FillAsync("Berlin");

		await Expect(Page).ToHaveURLAsync(new Regex(@"\?.*city=Berlin"));
	}

	[Test]
	public async Task OccurrenceFilter_UpdatesUrlWithOccurrenceParam()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Combobox).First.SelectOptionAsync("OneTime");

		await Expect(Page).ToHaveURLAsync(new Regex(@"\?.*occurrence=OneTime"));
	}

	[Test]
	public async Task ParticipationTypeFilter_UpdatesUrlWithParticipationTypeParam()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Combobox).Nth(1).SelectOptionAsync("Waitlist");

		await Expect(Page).ToHaveURLAsync(new Regex(@"\?.*participationType=Waitlist"));
	}

	[Test]
	public async Task UrlSearchParam_PreFillsSearchInput()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/?search=testquery");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByPlaceholder("Search…")).ToHaveValueAsync("testquery");
	}

	[Test]
	public async Task UrlCityParam_PreFillsCityInput()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/?city=Hamburg");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.GetByPlaceholder("City…")).ToHaveValueAsync("Hamburg");
	}

	[Test]
	public async Task SearchFilter_WithNoResults_HidesLoadMoreButton()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByPlaceholder("Search…").FillAsync("zzz_no_match_xyz_abc_999");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(
			Page.GetByRole(AriaRole.Button, new() { Name = "Load more" })
		).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task MultipleFilters_AllReflectedInUrl()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByPlaceholder("Search…").FillAsync("help");
		await Page.GetByPlaceholder("City…").FillAsync("Munich");

		await Expect(Page).ToHaveURLAsync(new Regex(@"\?.*search=help"));
		await Expect(Page).ToHaveURLAsync(new Regex(@"\?.*city=Munich"));
	}

	[Test]
	public async Task ClearingSearchFilter_RemovesParamFromUrl()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/?search=hello");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByPlaceholder("Search…").ClearAsync();

		await Expect(Page).Not.ToHaveURLAsync(new Regex(@"\?.*search="));
	}
}
