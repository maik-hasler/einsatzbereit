using AwesomeAssertions;
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
	public async Task HomePage_TogglesBetweenListAndMapView()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var mapToggle = Page.GetByTestId("view-toggle-map");
		await Expect(mapToggle).ToBeVisibleAsync();
		await mapToggle.ClickAsync();

		await Expect(Page.GetByTestId("opportunity-map")).ToBeVisibleAsync();
		await Expect(Page.Locator(".leaflet-container")).ToBeVisibleAsync();

		Page.Url.Should().Contain("view=map");

		var listToggle = Page.GetByTestId("view-toggle-list");
		await listToggle.ClickAsync();
		await Expect(Page.GetByTestId("opportunity-map")).Not.ToBeVisibleAsync();
	}
}
