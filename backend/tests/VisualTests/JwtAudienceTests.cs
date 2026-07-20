using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class JwtAudienceTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task MyEngagements_AuthenticatedVera_LoadsWithoutAuthError()
	{
		// Regression: before PR #476 the backend had ValidateAudience = false.
		// Now tokens must include aud=backend (added via Keycloak audience mapper).
		// A 401 response here means the mapper is missing or validation is broken.
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend").GetLeftPart(UriPartial.Authority);

		var authErrors = new List<string>();
		Page.Response += (_, resp) =>
		{
			if (resp.Url.StartsWith(backend, StringComparison.Ordinal)
				&& (resp.Status == 401 || resp.Status == 403))
				authErrors.Add($"{resp.Status} {resp.Url}");
		};

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		await Page.GotoAsync(
			$"{frontend.GetLeftPart(UriPartial.Authority)}/my-engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator("h1").First).ToBeVisibleAsync();

		// The Page.Response listener above is the authoritative check for this
		// regression (a real 401/403 from the backend) - a page-wide GetByText
		// scan for "401" is a fragile proxy on top of it: this suite's shared,
		// PerTestSession fixture accumulates test data across the whole run, and
		// other tests (e.g. EngagementCheckInStatusCodeTests) name orgs/
		// opportunities with a random GUID suffix that can coincidentally
		// contain "401", tripping this exact assertion with no real auth error
		// present.
		if (authErrors.Count > 0)
			throw new Exception(
				$"JWT audience validation rejected {authErrors.Count} request(s): "
				+ string.Join(", ", authErrors));
	}
}
