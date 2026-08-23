using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class PageHealthTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task HomePage_HasNoConsoleErrorsOrFailedRequests()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);

		var consoleErrors = new List<string>();
		var failedResponses = new List<string>();

		Page.Console += (_, msg) =>
		{
			if (msg.Type == "error")
				consoleErrors.Add(msg.Text);
		};
		Page.Response += (_, response) =>
		{
			if (response.Status >= 400 && !response.Url.EndsWith("/favicon.ico"))
				failedResponses.Add($"{response.Status} {response.Url}");
		};

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		if (consoleErrors.Count > 0)
			throw new Exception(
				$"Home page logged {consoleErrors.Count} console error(s):\n"
				+ string.Join("\n", consoleErrors));

		if (failedResponses.Count > 0)
			throw new Exception(
				$"Home page had {failedResponses.Count} failed request(s):\n"
				+ string.Join("\n", failedResponses));
	}
}
