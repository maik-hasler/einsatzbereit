using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OfflineStateTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OpportunityList_WhenTheConnectionReturns_RefetchesWithoutBeingAsked()
	{
		var origin = await WarmOpportunitiesRouteThenLeaveAsync();

		await Context.SetOfflineAsync(true);
		try
		{
			await GoToOpportunitiesAsync(origin);
			await Expect(Page.GetByTestId("opportunities-offline"))
				.ToBeVisibleAsync(new() { Timeout = 20_000 });
		}
		finally
		{
			await Context.SetOfflineAsync(false);
		}

		await Expect(Page.GetByTestId("opportunities-offline"))
			.Not.ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(Page.GetByTestId("opportunities-error"))
			.Not.ToBeVisibleAsync(new() { Timeout = 20_000 });

		await Expect(Page.GetByTestId("opportunity-date-line").First)
			.ToBeVisibleAsync(new() { Timeout = 20_000 });
	}
}
