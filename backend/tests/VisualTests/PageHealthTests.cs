using Microsoft.Playwright;

namespace VisualTests;

// Stays E2E (#2162, reconciling #2159's classification): this boots the real
// frontend against the real backend and fails on any console error or failed
// request - a real-stack integration smoke check that a mocked-API RTL test
// cannot reproduce, since a mocked call never fails the way a real
// misconfiguration (a broken script tag, a CORS error, a 404 static asset)
// does. Cheap (one page load) for what it catches.
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
